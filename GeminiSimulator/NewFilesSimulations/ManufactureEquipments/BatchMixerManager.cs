using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{

    public abstract class NewStepLink
    {
        protected readonly NewBatchManager _manager;
        protected DateTime? _fixedPlannedInit;
        public BatchOrder ParentOrder { get; set; }
        public NewStepLink? PreviousStep { get; set; } // Set público para armar la cadena
        public NewStepLink? NextStep { get; set; }
        public virtual string ResourceName => "N/A";
        public double DurationSeconds { get; }
        public double AccumulatedStarvation { get; set; }
        public double RealDurationSeconds => DurationSeconds + AccumulatedStarvation;
        public string MixerName => _manager.MixerName;

        protected NewStepLink(NewBatchManager manager, BatchOrder order, double duration)
        {
            _manager = manager;
            DurationSeconds = duration;
            ParentOrder = order;
        }


        public void SetFixedStart(DateTime date) => _fixedPlannedInit = date;

        // LÓGICA DE EMPUJE (Chain of Responsibility)
        public virtual DateTime PlannedInit
        {
            get
            {
                if (_fixedPlannedInit.HasValue) return _fixedPlannedInit.Value;

                // Si no hay anterior, usamos la fecha actual del simulador
                DateTime baseDate = PreviousStep?.PlannedEnd ?? _manager._mixer.CurrentDate;
                return CalculateAvailability(baseDate);
            }
        }

        public DateTime PlannedEnd => PlannedInit.AddSeconds(RealDurationSeconds);

        protected virtual DateTime CalculateAvailability(DateTime baseDate) => baseDate;

        public abstract string GetStatusMessage();
        public abstract string GetSubStatusMessage();

        // TEMPLATE METHOD
        public void CalculateStep(DateTime currentDate)
        {

            CalculateInternal(currentDate);

            if (CheckStepComplete())
            {
                SetNextStep();
            }
        }

        public abstract void CalculateInternal(DateTime currentDate);
        public abstract bool CheckStepComplete();
        public abstract bool PrepareStep();

        public void SetNextStep()
        {
            _manager.SelectNextStep();
        }
    }
    public class StepOperator : NewStepLink
    {
        private readonly NewMixer _mixer;
        private readonly string _taskName;

        public StepOperator(NewBatchManager manager, NewMixer mixer, BatchOrder order, string taskName, double duration)
            : base(manager, order, duration)
        {
            _mixer = mixer;
            _taskName = taskName;
        }

        public override void CalculateInternal(DateTime currentDate)
        {
            // No hay física aquí, solo el paso del tiempo si quieres medir 
            // cuánto tarda el operario en reaccionar.
        }

        public override bool CheckStepComplete()
        {
            // El paso se completa en el milisegundo en que el operario toma posesión
            return _mixer.AssignedOperator?.CurrentOwner == _mixer;
        }

        public override string GetStatusMessage()
        {
            if (_mixer.AssignedOperator?.CurrentOwner != _mixer)
            {
                return $"WAITING FOR OPERATOR: {_taskName}";
            }
            return $"OPERATOR ASSIGNED: {_taskName}";
        }
        public override string GetSubStatusMessage() => string.Empty;

        public override bool PrepareStep()
        {
            // 1. Si el compromiso es infinito, no necesitamos este "paso de captura"
            if (_mixer._OperatorOperationType == OperatorEngagementType.Infinite)
            {
                return false; // SKIP: Ya tenemos operario para siempre
            }

            // 2. Si ya lo tenemos capturado de un paso anterior
            if (_mixer.AssignedOperator?.CurrentOwner == _mixer)
            {
                return false; // SKIP: No hay nada que esperar
            }

            // 3. Si el operario existe pero no es nuestro, entramos en espera
            if (_mixer.AssignedOperator != null)
            {
                _mixer.AssignedOperator.Reserve(_mixer);

                // IMPORTANTE: Transicionamos el estado global del Mixer.
                // Esto detiene el Calculate() del Manager.
                _mixer.TransitionGlobalState(
                    new GlobalState_MasterWaiting(_mixer, _mixer.AssignedOperator)
                );

                // Devolvemos TRUE: "Este paso es válido y debe quedarse como CurrentStep"
                return true;
            }

            // Si no hay operario asignado al mixer en absoluto, saltamos
            return false;
        }
    }

    public class StepWashing : NewStepLink
    {
        NewMixer _mixer;
        double TargetTime = 0;
        double currentTime = 0;
        public override string ResourceName => _mixer.WashingPump?.Name ?? "No Wash Pump";
        double PendTime => TargetTime - currentTime;
        public StepWashing(NewBatchManager manager, NewMixer mixer, BatchOrder order, double duration) : base(manager, order, duration)
        {
            _mixer = mixer;
        }


        public override void CalculateInternal(DateTime currentDate)
        {
            currentTime += 1;
            _mixer.WashingPump?.SetRemainingSeconds(PendTime);
        }

        public override bool CheckStepComplete()
        {
            if (PendTime <= 0)
            {
                _mixer.WashingPump?.SetRemainingSeconds(0);
                return true;
            }
            return false;

        }

        public override string GetStatusMessage()
        {
            return $"Washing {currentTime:F0} / {TargetTime:F0} s";
        }
        public override string GetSubStatusMessage() => string.Empty;
        public override bool PrepareStep()
        {
            if (_mixer.NeedsWashing(_mixer.LastMaterialProcessed, _mixer.CurrentMaterial))
            {
                if (!_mixer.WashingPump!.RequestAccess(_mixer))
                {
                    // A. ¡PIDO TURNO! (Vital para que me avisen luego)
                    _mixer.WashingPump.Reserve(_mixer);

                    // B. ME DUERMO (Estado Naranja)
                    // "Estoy esperando a mi recurso (La Bomba de Lavado)".
                    // La bomba me despertará (Operational) cuando termine con el otro.
                    _mixer.TransitionGlobalState(
                        new GlobalState_MasterWaiting(_mixer, _mixer.WashingPump)
                    );
                }
                TargetTime = _mixer.WashoutRules
                  .GetMixerWashout(_mixer.LastMaterialProcessed!.Category, _mixer.CurrentMaterial!.Category)
                  .GetValue(TimeUnits.Second);
                return true;
            }
            return false;
        }
    }
    public class StepTiming : NewStepLink
    {
        NewMixer _mixer;
        double TargetTime = 0;
        double currentTime = 0;
        double PendTime => TargetTime - currentTime;
        RecipeStep _step;
        int _totalSteps = 0;
        public StepTiming(NewBatchManager manager, NewMixer mixer, RecipeStep step, BatchOrder order, int totalsteps, double duration) : base(manager, order, duration)
        {
            _mixer = mixer;
            _step = step;
            _totalSteps = totalsteps;
        }


        public override void CalculateInternal(DateTime currentDate)
        {
            currentTime += 1;

        }

        public override bool CheckStepComplete()
        {
            if (PendTime <= 0)
            {

                return true;
            }
            return false;

        }

        public override string GetStatusMessage()
        {
            return $"{_step.Order}/{_totalSteps} - {_step.OperationType}";
        }
        public override string GetSubStatusMessage() => $"{currentTime:F0} / {TargetTime:F0} s";
        public override bool PrepareStep()
        {
            TargetTime = _step.Duration.GetValue(TimeUnits.Second);
            return true;
        }
    }

    public class StepMass : NewStepLink
    {
        public override string ResourceName => InletPump?.Name ?? "Manual / No Pump";
        NewMixer _mixer;
        double TargetTime = 0;
        double currentTime = 0;
        double PendTime => TargetTime - currentTime;
        RecipeStep _step;
        int _totalSteps = 0;
        double TargetMass = 0;
        double currentMass = 0;
        double PendingMass => TargetMass - currentMass;
        NewPump? InletPump;
        double pumpFlow => InletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? TargetMass;
        double batchSize = 0;
        public StepMass(NewBatchManager manager, NewMixer mixer, RecipeStep step, BatchOrder order, int totalsteps, double BatchSize, double duration) : base(manager, order, duration)
        {
            _mixer = mixer;
            _step = step;
            _totalSteps = totalsteps;
            TargetTime = duration;
            batchSize = BatchSize;
        }


        public override void CalculateInternal(DateTime currentDate)
        {
            currentTime += 1;
            double currentFlow = PendingMass > pumpFlow ? pumpFlow : PendingMass;
            InletPump?.SetCurrentFlow(currentFlow);
            currentMass += currentFlow;
            _mixer._currentLevel += currentFlow;
            InletPump?.SetRemainingSeconds(PendTime + AccumulatedStarvation);
        }

        public override bool CheckStepComplete()
        {
            if (PendingMass <= 0)
            {
                InletPump?.SetRemainingSeconds(0);
                InletPump?.SetCurrentFlow(0);
                InletPump?.ReleaseAccess(_mixer);

                return true;
            }
            return false;

        }

        public override string GetStatusMessage()
        {
            return $"{_step.Order}/{_totalSteps} - {_step.IngredientName} ";
        }
        public override string GetSubStatusMessage() => $"{currentMass:F0} / {TargetMass:F0} Kg";
        public override bool PrepareStep()
        {
            if (_step == null) return false;
            TargetTime = _step.Duration.GetValue(TimeUnits.Second);
            var ingredientId = _step?.IngredientId;

            TargetMass = batchSize * _step!.TargetPercentage / 100.0;
            // 1. BUSCAR BOMBAS VÁLIDAS
            var validPumps = _mixer.InletPumps
                .Where(p => p.SupportedProducts.Any(prod => prod.Id == ingredientId))
                .ToList();

            // CASO A: HAY BOMBAS COMPATIBLES (Automático)
            if (validPumps.Any())
            {
                var selectedPump = validPumps
                  .OrderByDescending(p => p.GlobalState.IsOperational && p.CaptureState is NewUnitAvailableToCapture)
                  .ThenBy(p => p.RemainingSeconds.GetValue(TimeUnits.Minute))
                  .First(); // Seguro llamar First() porque validPumps.Any() es true.

                InletPump = selectedPump;

                // 3. INTENCIÓN (Outlet State)


                // 4. REALIDAD (Global State)
                // Intentamos tomar posesión física.
                if (!InletPump.RequestAccess(_mixer))
                {
                    // FALLO: La bomba está ocupada.

                    // A. ¡PIDO TURNO! (Vital)
                    InletPump.Reserve(_mixer);


                    // B. ME DUERMO (MasterWaiting)
                    // "Estoy esperando a mi recurso (La Bomba)".
                    // Cuando la bomba se libere, ejecutará AssignResource(mí) y me despertará.
                    _mixer.TransitionGlobalState(
                        new GlobalState_MasterWaiting(_mixer, InletPump)
                    );
                }
            }
            //CASO B: la adicion es manual ejecuta el paso manual

            return true;
        }
    }
    public class StepDischargeToVessel : NewStepLink
    {
        NewMixer _mixer;
        double TargetTime = 0;
        double currentTime = 0;
        double PendTime => TargetTime - currentTime;


        double TargetMass = 0;
        double currentMass = 0;
        double PendingMass => TargetMass - currentMass;
        NewPump? OuletPump => _mixer.OutletPump;
        double pumpFlow => OuletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
        NewRecipedInletTank? _DestinationVessel;
        public StepDischargeToVessel(NewBatchManager manager, NewMixer mixer, BatchOrder order, double duration) : base(manager, order, duration)
        {
            _mixer = mixer;
            _DestinationVessel = mixer.DestinationVessel;
            TargetTime = DurationSeconds;

        }


        public override void CalculateInternal(DateTime currentDate)
        {
            currentTime += 1;
            double currentFlow = PendingMass > pumpFlow ? pumpFlow : PendingMass;

            OuletPump?.SetCurrentFlow(currentFlow);
            _mixer.DestinationVessel?.SetInletFlow(currentFlow);
            _mixer._currentLevel -= currentFlow;
            currentMass += currentFlow;

        }

        public override bool CheckStepComplete()
        {
            if (_mixer._currentLevel <= 0)
            {
                _DestinationVessel?.SetInletFlow(0);
                _DestinationVessel?.ReleaseAccess(_mixer);
                _mixer.LastMaterialProcessed = _mixer.CurrentMaterial;
                _mixer._currentLevel = 0;
                _mixer.CurrentMaterial = null;
                _mixer.RealeaseWipQueue();
                return true;
            }
            return false;

        }

        public override string GetStatusMessage()
        {
            return $"Discharging to - {_DestinationVessel?.Name ?? string.Empty} ";
        }
        public override string GetSubStatusMessage() => $"{currentMass:F0} / {TargetMass:F0} Kg";
        public override bool PrepareStep()
        {
            if (_mixer._OperatorOperationType == OperatorEngagementType.FullBatch)
            {
                _mixer.AssignedOperator?.ReleaseAccess(_mixer);
            }
            if (_DestinationVessel == null) return false;
            TargetMass = _mixer._currentLevel;

            if (!_DestinationVessel.RequestAccess(_mixer))
            {
                // A. ¡PIDO TURNO! (Vital)
                // Me anoto en la lista del tanque.
                _DestinationVessel.Reserve(_mixer);

                // B. ME DUERMO (MasterWaiting - Naranja)
                // "Estoy esperando a mi recurso (El Tanque de Destino)".
                // Cuando el tanque se vacíe o se libere, me llamará y me despertará.
                _mixer.TransitionGlobalState(
                    new GlobalState_MasterWaiting(_mixer, _DestinationVessel)
                );
            }

            return true;
        }
    }
    public class NewBatchManager
    {
        public NewMixer _mixer;
        public string MixerName => _mixer.Name;
        public NewStepLink? CurrentStep { get; private set; }

        public DateTime CurrentDate => _mixer.CurrentDate;
        public Queue<NewStepLink> ActivePipeline = new Queue<NewStepLink>();
        public Queue<NewStepLink> ExecutionHistory = new Queue<NewStepLink>();
        public NewBatchManager(NewMixer mixer)
        {
            _mixer = mixer;
        }

        public void SelectNextStep()
        {
            if (ActivePipeline.TryDequeue(out var completed))
            {
                ExecutionHistory.Enqueue(completed);

                // LA VERDAD ABSOLUTA: El momento exacto del relevo es AHORA.
                DateTime now = _mixer.CurrentDate;

                if (ActivePipeline.TryPeek(out var nextActivity))
                {
                    CurrentStep = nextActivity;
                    // Le decimos al siguiente: "El pasado ya pasó. Tu realidad empieza AHORA".
                    CurrentStep.SetFixedStart(now);
                    if (!CurrentStep.PrepareStep())
                    {
                        SelectNextStep();
                    }
                }
                else
                {
                    _mixer.ReceiveStopCommand();
                }
            }

        }


        public void Calculate()
        {
            if (CurrentStep != null)
            {
                CurrentStep.CalculateStep(_mixer.CurrentDate);

                // Actualizamos las métricas del lote en curso en cada tick
                // para que el usuario vea el "BatchReal" crecer en tiempo real.
                UpdateOrderMetrics(CurrentStep.ParentOrder);
            }
        }

        public string GetGlobalStatus() => CurrentStep?.GetStatusMessage() ?? "IDLE";
        public void AddOrder(ProductDefinition product, ProductDefinition? lastMaterial, NewRecipedInletTank DestinationVessel)
        {
            double batchSize = _mixer.ProductCapabilities[product].GetValue(MassUnits.KiloGram);
            var newOrderContext = new BatchOrder
            {
                BackBone = product,
                TargetBatchSize = batchSize
            };
            // 1. Obtener el último paso si existe para encadenar
            NewStepLink? lastStep = ActivePipeline.Count > 0 ? ActivePipeline.Last() : null;

            // 2. Definir una función local para automatizar el encadenamiento
            NewStepLink EnqueueStep(NewStepLink newStep)
            {
                if (lastStep != null)
                {
                    lastStep.NextStep = newStep;
                    newStep.PreviousStep = lastStep;
                }
                ActivePipeline.Enqueue(newStep);
                lastStep = newStep;
                return newStep;
            }

            // 3. CONSTRUCCIÓN DE LA SECUENCIA
            // A. El Operario toma el mando
            EnqueueStep(new StepOperator(this, _mixer, newOrderContext, "Setup Batch", 0));

            // B. Lavado (si aplica)
            double washoutTime = 0;
            if (_mixer.NeedsWashing(lastMaterial, product))
            {
                washoutTime = _mixer.WashoutRules
                   .GetMixerWashout(lastMaterial!.Category, product.Category)
                   .GetValue(TimeUnits.Second);
            }
            EnqueueStep(new StepWashing(this, _mixer, newOrderContext, washoutTime));

            // C. Receta
            int total = product.RecipeSteps.Count;
            foreach (var stepRecipe in product.RecipeSteps.OrderBy(x => x.Order))
            {
                if (stepRecipe.OperationType == QWENShared.Enums.BackBoneStepType.Add)
                {
                    EnqueueStep(new StepMass(this, _mixer, stepRecipe, newOrderContext, total, batchSize, stepRecipe.Duration.GetValue(TimeUnits.Second)));
                }
                else
                {
                    EnqueueStep(new StepTiming(this, _mixer, stepRecipe, newOrderContext, total, stepRecipe.Duration.GetValue(TimeUnits.Second)));
                }
            }

            // D. Descarga

            double transferTime = _mixer._DischargeRate == 0 ? 0 : batchSize / _mixer._DischargeRate;
            EnqueueStep(new StepDischargeToVessel(this, _mixer, newOrderContext, transferTime));

            // 4. ACTIVACIÓN INICIAL
            // Si no hay nada ejecutándose, arrancamos la cola

        }

        public void StartPipeline()
        {
            if (ActivePipeline.TryPeek(out var first))
            {
                CurrentStep = first;
                CurrentStep.SetFixedStart(_mixer.CurrentDate);
                if (!CurrentStep.PrepareStep())
                {
                    SelectNextStep(); // Saltar si el primero no es necesario
                }
            }
        }
        public DateTime MixerProjectedFreeTime
        {
            get
            {
                // Buscamos el último paso del último batch encolado.
                var lastStepInPipeline = ActivePipeline.LastOrDefault();

                if (lastStepInPipeline != null)
                {
                    return lastStepInPipeline.PlannedEnd;
                }

                // Si no hay batches, el mixer está libre YA (o en su fecha actual).
                return _mixer.CurrentDate;
            }
        }

        /// <summary>
        /// Indica cuánto tiempo falta para que el Mixer se libere de TODA su carga actual.
        /// </summary>
        public TimeSpan TotalTimeUntilFree => MixerProjectedFreeTime - _mixer.CurrentDate;
        public void UpdateOrderMetrics(BatchOrder order)
        {
            // 1. Buscamos todos los pasos de esta orden en ambas colas
            var allStepsForThisOrder = ExecutionHistory
                .Where(s => s.ParentOrder.OrderId == order.OrderId)
                .Concat(ActivePipeline.Where(s => s.ParentOrder.OrderId == order.OrderId));

            // 2. Calculamos los "Amounts" solicitados
            order.BatchTeorico = allStepsForThisOrder.Sum(s => s.DurationSeconds);
            order.BatchReal = allStepsForThisOrder.Sum(s => s.RealDurationSeconds);
        }
        public DateTime CurrentBatchProjectedEnd
        {
            get
            {
                if (CurrentStep == null) return _mixer.CurrentDate;

                // Buscamos en la cola el último paso que pertenece a la orden actual.
                var lastStepOfCurrentOrder = ActivePipeline
                    .Where(s => s.ParentOrder.OrderId == CurrentStep.ParentOrder.OrderId)
                    .LastOrDefault();

                return lastStepOfCurrentOrder?.PlannedEnd ?? _mixer.CurrentDate;
            }
        }

        /// <summary>
        /// 3. ÚLTIMO PRODUCTO PROGRAMADO:
        /// Mira hasta el fondo de la cola para decirte qué es lo último que se va a fabricar.
        /// </summary>
        public BatchOrder? LastProgrammedBatch => ActivePipeline.LastOrDefault()?.ParentOrder;
        public ProductDefinition? LastProduct => LastProgrammedBatch?.BackBone;
        public BatchOrder? CurrentBatch => CurrentStep?.ParentOrder;
    }
    public class BatchOrder
    {
        public ProductDefinition? BackBone { get; set; }
        public Guid OrderId { get; set; } = Guid.NewGuid();
        public string ProductName => BackBone?.Name ?? string.Empty;
        public double TargetBatchSize { get; set; }
        public DateTime CreatedAt { get; set; }

        // Podemos guardar aquí métricas específicas del lote
        public double TotalStarvation { get; set; }
        public double BatchTeorico { get; set; } // Suma de segundos teóricos
        public double BatchReal { get; set; }    // Suma de segundos reales (Teórico + Starved)

        // El porcentaje de eficiencia de tiempo del lote
        public double OEE_Performance => BatchTeorico > 0 ? (BatchTeorico / BatchReal) * 100 : 100;
    }
}
