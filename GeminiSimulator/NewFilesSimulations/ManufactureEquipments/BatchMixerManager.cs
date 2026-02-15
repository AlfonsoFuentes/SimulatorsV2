using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{
    public enum MixerStateCategory
    {
        Producing,          // Mezclando / Agitando
        WaitingIngredients, // Esperando carga de materia prima
        Mixing,            // Alcanzando temperatura
        Analisys,
        ConnectToWip,            // Enfriamiento

        StarvedByPump,      // Bloqueado por falta de bomba
        StarvedByWashingPump,      // Bloqueado por falta de bomba

    }
    public abstract class NewStepLink
    {
        public string BatchName { get; set; } = "";
        public virtual string ResourceName => "N/A";
        protected readonly NewBatchManager _manager;
        public StepTimeManager TimeManager { get; private set; }
        //public BatchOrder ParentOrder { get; set; }
        public NewStepLink? PreviousStep { get; set; }
        public NewStepLink? NextStep { get; set; }

        public DateTime CurrentDate => _manager.CurrentDate;
        public string MixerName => _manager.MixerName;

        // Propiedades redirigidas para el reporte
        public double TheoricalDurationSeconds => TimeManager.TheoreticalDuration;
        public double AccumulatedStarvation => TimeManager.StarvationSeconds;
        public double RealDurationSeconds => TimeManager.WorkedSeconds;
        public DateTime PlannedInit => TimeManager.StartDate;
        public DateTime PlannedEnd => TimeManager.ExpectedEndDate;

        protected NewStepLink(NewBatchManager manager, double duration)
        {
            _manager = manager;
            //ParentOrder = order;
            TimeManager = new StepTimeManager(this, duration);
        }

        // Delega la responsabilidad al StepTimeManager
        public void SetFixedStart(DateTime date) => TimeManager.SetFixedStart(date);

        public void CalculateStep(DateTime currentDate)
        {
            TimeManager.AddWork(1);
            CalculateInternal(currentDate);

            if (CheckStepComplete())
            {
                TimeManager.CloseStep(currentDate);
                _manager.SelectNextStep();
            }
        }

        public abstract void CalculateInternal(DateTime currentDate);
        public abstract bool CheckStepComplete();
        public abstract bool PrepareStep();
        public abstract string GetStatusMessage();
        public abstract string GetSubStatusMessage();
        public void AddStarvation()
        {
            TimeManager.AddStarvation();
        }
    }


    public class StepOperator : NewStepLink
    {
        private NewMixer _mixer => _manager.Mixer;
        private readonly string _taskName;
        public override string ResourceName => _mixer.AssignedOperator?.Name ?? "N/A";
        public StepOperator(NewBatchManager manager, string taskName, double duration)
            : base(manager, duration)
        {

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


            // 2. Si ya lo tenemos capturado de un paso anterior
            if (_mixer.AssignedOperator?.CurrentOwner == _mixer)
            {
                return false; // SKIP: No hay nada que esperar
            }

            // 3. Si el operario existe pero no es nuestro, entramos en espera
            if (_mixer.AssignedOperator != null)
            {
                if (!_mixer.AssignedOperator!.RequestAccess(_mixer))
                {
                    _mixer.AssignedOperator.Reserve(_mixer);

                    // IMPORTANTE: Transicionamos el estado global del Mixer.
                    // Esto detiene el Calculate() del Manager.
                    _mixer.TransitionGlobalState(
                        new GlobalState_MasterWaiting(_mixer, _mixer.AssignedOperator)
                    );
                    return true;
                }



            }

            // Si no hay operario asignado al mixer en absoluto, saltamos
            return false;
        }
    }

    public class StepWashing : NewStepLink
    {
        NewMixer _mixer => _manager.Mixer;
        double TargetTime = 0;
        double currentTime = 0;
        public override string ResourceName => _mixer.WashingPump?.Name ?? "No Wash Pump";
        double PendTime => TargetTime - currentTime;
        public StepWashing(NewBatchManager manager, double duration) : base(manager, duration)
        {

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
        NewMixer _mixer => _manager.Mixer;
        double TargetTime = 0;
        double currentTime = 0;
        double PendTime => TargetTime - currentTime;
        RecipeStep _step;
        int _totalSteps = 0;
        public override string ResourceName => $"{StepType}";
        public BackBoneStepType StepType => _step.OperationType;
        public StepTiming(NewBatchManager manager, RecipeStep step, int totalsteps, double duration) : base(manager, duration)
        {

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
        NewMixer _mixer => _manager.Mixer;
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
        public StepMass(NewBatchManager manager, RecipeStep step, int totalsteps, double BatchSize, double duration) : base(manager, duration)
        {

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
            double currentLevel = _mixer.CurrentLevel.GetValue(MassUnits.KiloGram);
            _mixer.SetCurrentLevel(currentLevel + currentFlow);
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
                return true;
            }
            //CASO B: la adicion es manual ejecuta el paso manual
            var operatorsManageMaterial = _mixer.AssignedOperator?.SupportedProducts.Any(prod => prod.Id == ingredientId) ?? false;
            if (!operatorsManageMaterial)
            {
                Console.WriteLine($"Error en mixer {_mixer.Name} no maneja {_step.IngredientName}");
            }

            return true;
        }

    }
    public class StepDischargeToVessel : NewStepLink
    {
        NewMixer _mixer => _manager.Mixer;
        double TargetTime = 0;
        double currentTime = 0;
        double PendTime => TargetTime - currentTime;
        public override string ResourceName => OuletPump?.Name ?? "N/A";

        double TargetMass = 0;
        double currentMass = 0;
        double PendingMass => TargetMass - currentMass;
        NewPump? OuletPump => _mixer.OutletPump;
        double pumpFlow => OuletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
        NewRecipedInletTank _DestinationVessel;
        public StepDischargeToVessel(NewBatchManager manager, NewRecipedInletTank DestinationVessel, double duration) : base(manager, duration)
        {
            _DestinationVessel = DestinationVessel;

            TargetTime = TheoricalDurationSeconds;

        }


        public override void CalculateInternal(DateTime currentDate)
        {
            currentTime += 1;
            double currentFlow = PendingMass > pumpFlow ? pumpFlow : PendingMass;

            OuletPump?.SetCurrentFlow(currentFlow);
            _DestinationVessel?.SetInletFlow(currentFlow);
            double currentlevel = _mixer.CurrentLevel.GetValue(MassUnits.KiloGram);
            _mixer.SetCurrentLevel(currentlevel - currentFlow);
            currentMass += currentFlow;

        }

        public override bool CheckStepComplete()
        {
            if (_mixer.CurrentLevel.GetValue(MassUnits.KiloGram) <= 0.001)
            {
                OuletPump?.SetCurrentFlow(0);
                _DestinationVessel?.SetInletFlow(0);
                _DestinationVessel?.ReleaseAccess(_mixer);
                _mixer.LastMaterialProcessed = _mixer.CurrentMaterial;
                _mixer.SetCurrentLevel(0);
                _mixer.CurrentMaterial = null;
                _DestinationVessel!.ReceiveStopDischargFromMixer();
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
            if (_mixer.AssignedOperator?.CurrentOwner == _mixer)
            {
                _mixer.AssignedOperator?.ReleaseAccess(_mixer);
            }
            if (_DestinationVessel == null) return false;
            TargetMass = _mixer.CurrentLevel.GetValue(MassUnits.KiloGram);

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

        public string MixerName => _mixer.Name;
        public NewMixer Mixer => _mixer;
        public DateTime CurrentDate => _mixer.CurrentDate;
        private readonly NewMixer _mixer;

        public NewStepLink? _CurrentStep { get; private set; }
        public Queue<BatchOrder> BatchQueue { get; set; } = new();
        BatchOrder? _CurrentBatch;

        public Queue<NewStepLink> ActivePipeline => _CurrentBatch?.ActivePipeline ?? new();
        public Queue<NewStepLink> BatchRecordHistory => _CurrentBatch?.BatchRecordHistory ?? new();
        public Queue<BatchOrder> BatchRecord { get; set; } = new();
        TransferBatchToMixerCalculation _TransferBatchToMixerCalculation;
        OperatorEngagementType _OperatorEngagementType;
        Amount _OperatorTimeDisabled = null!;
        public void SetTransferBatchToMixerCalculation(TransferBatchToMixerCalculation transferBatchToMixerCalculation)
        {
            _TransferBatchToMixerCalculation = transferBatchToMixerCalculation;
        }
        public NewBatchManager(NewMixer mixer, TransferBatchToMixerCalculation TransferBatchToMixerCalculation, OperatorEngagementType OperatorEngagementType, Amount OperatorTimeDisabled)
        {
            _mixer = mixer;
            _TransferBatchToMixerCalculation = TransferBatchToMixerCalculation;
            _OperatorEngagementType = OperatorEngagementType;
            _OperatorTimeDisabled = OperatorTimeDisabled;
        }




        public void Calculate()
        {
            if (_CurrentStep != null)
            {
                _CurrentStep.CalculateStep(_mixer.CurrentDate);


            }
        }

        int BatchOrder = 0;
        public void AddOrder(NewRecipedInletTank destinationVessel, ProductDefinition? lastMaterial)
        {
            // 1. Validaciones de integridad
            if (!ReviewIntegrityDataToCreateOrder(_mixer, destinationVessel, destinationVessel.CurrentMaterial!)) return;

            var product = destinationVessel.CurrentMaterial!;
            BatchOrder++;
            // 2. Creamos la entidad de la Orden
            var newOrder = new BatchOrder(_mixer, destinationVessel);

            // --- EL GANCHO DE TIEMPO (Crucial) ---
            // Buscamos el último eslabón de la cadena actual:
            // Puede estar al final de la cola de espera o ser el último del lote que está corriendo.
            var globalLastOrder = BatchQueue.LastOrDefault() ?? _CurrentBatch;
            // Devuelve el NewStepLink que tiene la fecha de fin más lejana
            NewStepLink? globalTail = globalLastOrder?.Steps.MaxBy(s => s.PlannedEnd);

            // 3. Función interna para construir la cadena
            void EnqueueStep(NewStepLink newStep)
            {
                if (globalTail != null)
                {
                    // Creamos el puente físico entre pasos (incluso de órdenes distintas)
                    globalTail.NextStep = newStep;
                    newStep.PreviousStep = globalTail;
                }
                newStep.BatchName = $"{_mixer.Name}-{BatchOrder}";
                // Registramos en ambas estructuras de la orden
                newOrder.Steps.Add(newStep);             // Para métricas (BCT)
                newOrder.ActivePipeline.Enqueue(newStep); // Para ejecución (SelectNextStep)

                // El nuevo paso se convierte en la nueva "cola" para el siguiente
                globalTail = newStep;
            }

            // --- 4. CONSTRUCCIÓN DE LA SECUENCIA FÍSICA ---

            // A. Setup (Operario)
            if (_OperatorEngagementType != OperatorEngagementType.Infinite)
            {
                EnqueueStep(new StepOperator(this, "Setup Batch", 0));
            }

            // B. Lavado (Si aplica)
            if (_mixer.NeedsWashing(lastMaterial, product))
            {
                double washTime = _mixer.WashoutRules
                    .GetMixerWashout(lastMaterial!.Category, product.Category)
                    .GetValue(TimeUnits.Second);
                EnqueueStep(new StepWashing(this, washTime));
            }

            // C. Pasos de Receta
            int totalRecipeSteps = product.RecipeSteps.Count;
            foreach (var recipeStep in product.RecipeSteps.OrderBy(x => x.Order))
            {
                double duration = recipeStep.Duration.GetValue(TimeUnits.Second);

                if (recipeStep.OperationType == QWENShared.Enums.BackBoneStepType.Add)
                {
                    EnqueueStep(new StepMass(this, recipeStep, totalRecipeSteps, newOrder.TargetBatchSize, duration));
                }
                else
                {
                    if (recipeStep.OperationType != BackBoneStepType.Connect_Mixer_WIP)
                    {
                        EnqueueStep(new StepTiming(this, recipeStep, totalRecipeSteps, duration));
                    }
                    else if (_TransferBatchToMixerCalculation == TransferBatchToMixerCalculation.Manual)
                    {
                        if (destinationVessel is NewWipTank wipTank)
                        {
                            if (wipTank.ProcesedBatchQueue.Any())
                            {
                                var lastBatchinWIP = wipTank.ProcesedBatchQueue.Last();
                                if (lastBatchinWIP != null && lastBatchinWIP.Product != product)
                                {
                                    EnqueueStep(new StepTiming(this, recipeStep, totalRecipeSteps, duration));
                                }
                            }
                            else
                            {
                                EnqueueStep(new StepTiming(this, recipeStep, totalRecipeSteps, duration));
                            }

                        }
                        else
                        {
                            EnqueueStep(new StepTiming(this, recipeStep, totalRecipeSteps, duration));
                        }
                    }

                }
            }

            // D. Descarga
            double transferTime = _mixer._DischargeRate == 0 ? 0 : newOrder.TargetBatchSize / _mixer._DischargeRate;
            EnqueueStep(new StepDischargeToVessel(this, destinationVessel, transferTime));

            // 5. Finalización: Metemos la orden a la cola global
            BatchQueue.Enqueue(newOrder);
            BatchRecord.Enqueue(newOrder);
            destinationVessel.MixerQueue.Enqueue(_mixer);
        }

        public void SelectNextStep()
        {
            // 1. Buscamos el siguiente paso en la cola del lote actual
            if (ActivePipeline.TryDequeue(out var nextActivity))
            {
                // El paso nace y se va directo al historial para el reporte
                _CurrentStep = nextActivity;
                BatchRecordHistory.Enqueue(_CurrentStep);

                // LA VERDAD ABSOLUTA: El inicio es ahora
                DateTime now = _mixer.CurrentDate;
                _CurrentStep.SetFixedStart(now);

                // Intentamos prepararlo
                if (!_CurrentStep.PrepareStep())
                {
                    // Si no es necesario (ej: lavado), llamamos de nuevo 
                    // para sacar el siguiente de la cola.
                    SelectNextStep();
                }
            }
            else
            {
                // 2. Si no hay más pasos, buscamos el siguiente LOTE
                if (BatchQueue.TryDequeue(out var nextBatch))
                {
                    _CurrentBatch = BatchQueue.Dequeue();
                    if (_CurrentBatch.Vessel is NewWipTank wipTank)
                    {
                        wipTank.ProcesedBatchQueue.Enqueue(_CurrentBatch);

                    }
                    if (_CurrentBatch.Vessel.MixerQueue.TryPeek(out var mixer))
                    {
                        if (mixer == _mixer)
                        {
                            _CurrentBatch.Vessel.MixerQueue.Dequeue();
                        }
                    }
                    _CurrentBatch.Vessel.AssignedMixer = _mixer;
                    _mixer.CurrentMaterial = _CurrentBatch.Product;
                    _mixer.DestinationVessel = _CurrentBatch.Vessel;
                    SelectNextStep();
                }
                else
                {
                    // 3. Fin de toda la programación
                    _mixer.ReceiveStopCommand();
                    _CurrentBatch = null;
                    _CurrentStep = null!;
                }
            }
        }
        public void StartPipeline()
        {
            if (BatchQueue.TryPeek(out var firstBatch))
            {
                _CurrentBatch = BatchQueue.Dequeue();

                if (_CurrentBatch.Vessel is NewWipTank wipTank)
                {
                    wipTank.ProcesedBatchQueue.Enqueue(_CurrentBatch);

                }
                if (_CurrentBatch.Vessel.MixerQueue.TryPeek(out var mixer))
                {
                    if (mixer == _mixer)
                    {
                        _CurrentBatch.Vessel.MixerQueue.Dequeue();
                    }
                }


                _CurrentBatch.Vessel.AssignedMixer = _mixer;
                _mixer.CurrentMaterial = _CurrentBatch.Product;
                _mixer.DestinationVessel = _CurrentBatch.Vessel;
                SelectNextStep();
            }
        }

        public bool ReviewIntegrityDataToCreateOrder(NewMixer mixer, NewRecipedInletTank Vessel, ProductDefinition backbone)
        {
            if (_mixer == null || Vessel == null || backbone == null)
                return false;
            if (_mixer.ProductCapabilities.TryGetValue(backbone, out var productCapabilities))
            {
                return true;
            }
            Console.WriteLine($"Data in Mixer {mixer.Name} not found");
            return false;
        }
        public BatchOrder? CurrentBatch => _CurrentBatch;

        public NewStepLink? CurrentStep => _CurrentStep;
        /// <summary>
        /// 1. ¿Cuándo terminará el mixer de usarse? (Fecha exacta)
        /// Mira el último paso del último lote en la cola. Si no hay nada, usa el lote actual.
        /// </summary>
        public DateTime MixerProjectedFreeTime =>
            BatchQueue.LastOrDefault()?.PlannedEndBatch
            ?? _CurrentBatch?.PlannedEndBatch
            ?? _mixer.CurrentDate;

        public DateTime CurrentBatchProjectedEndDate =>
            _CurrentBatch?.PlannedEndBatch
           ?? _mixer.CurrentDate;
        public DateTime CurrentBatchTheoricalEndDate =>
           _CurrentBatch?.PlannedTheoricalEndBatch
          ?? _mixer.CurrentDate;

        /// <summary>
        /// 2. Tiempo restante total para que el Mixer quede libre (TimeSpan)
        /// </summary>
        TimeSpan _TotalTimeUntilFree => MixerProjectedFreeTime - _mixer.CurrentDate;
        public Amount TotalTimeUntilFree => new Amount(_TotalTimeUntilFree.TotalSeconds, TimeUnits.Second);
        /// <summary>
        /// 3. Tiempo de Trabajo Real del Lote Actual (BCT_Current)
        /// Muestra cuánto tiempo de "motor" lleva consumido el batch en curso.
        /// </summary>
        public Amount CurrentBatchRealTime => _CurrentBatch?.BCT_Current ?? new Amount(0, TimeUnits.Second);

        /// <summary>
        /// 4. Tiempo Perdido (Starved) del Lote Actual
        /// Muestra cuánto tiempo lleva el batch detenido por falta de recursos (bombas/operarios).
        /// </summary>
        public Amount CurrentBatchStarvedTime => _CurrentBatch?.TimeStarved ?? new Amount(0, TimeUnits.Second);

        /// <summary>
        /// 5. Tiempo Total Esperado para el Lote Actual (BCT_Expected)
        /// Es la suma del Trabajo + Hambre + lo que falta por procesar.
        /// </summary>
        public Amount CurrentBatchExpectedTotal => _CurrentBatch?.BCT_Expected ?? new Amount(0, TimeUnits.Second);

        public ProductDefinition? LastProduct => BatchQueue.LastOrDefault()?.Product ?? _mixer.CurrentMaterial;
        public void AddStarvation()
        {
            _CurrentStep?.AddStarvation();
        }
        public string StatusMessage => _CurrentStep?.GetStatusMessage() ?? string.Empty;
        public string SubStatusMessage => _CurrentStep?.GetSubStatusMessage() ?? string.Empty;
        public double TotalRealDurationSeconds => BatchRecordHistory.Sum(x => x.RealDurationSeconds);
        public double TotalStarvation => BatchRecordHistory.Sum(x => x.AccumulatedStarvation);
        public double TotalTimeMixer => TotalRealDurationSeconds + TotalStarvation;
        public double AU_Performance => _mixer.SecondsTotal == 0 ? 0 : TotalRealDurationSeconds / _mixer.SecondsTotal * 100;
        public double CurrentBatchProgress
        {
            get
            {
                if (_CurrentBatch == null) return 0;

                double total = _CurrentBatch.BCT_Expected.GetValue(TimeUnits.Second);
                double worked = _CurrentBatch.BCT_Current.GetValue(TimeUnits.Second);

                return total <= 0 ? 0 : (worked / total) * 100;
            }
        }

    }

    public class BatchOrder
    {
        public NewMixer Mixer => _mixer;
        NewMixer _mixer;
        NewRecipedInletTank _Vessel;

        public NewRecipedInletTank Vessel => _Vessel;
        public ProductDefinition Product { get; private set; }
        public BatchOrder(NewMixer mixer, NewRecipedInletTank Vessel)
        {
            _mixer = mixer;
            _Vessel = Vessel;
            Product = Vessel.CurrentMaterial!;
            StartBatchDate = _mixer.CurrentDate;
            TargetBatchSize = _mixer.ProductCapabilities[Product].GetValue(MassUnits.KiloGram);
        }
        // --- 1. IDENTIDAD Y CONTEXTO ---
        public Guid OrderId { get; set; } = Guid.NewGuid();
        public string MixerName => _mixer.Name;
        public string WipName => _Vessel.Name;


        public string ProductName => Product?.Name ?? "N/A";
        public double TargetBatchSize { get; private set; }
        public DateTime StartBatchDate { get; private set; } = DateTime.Now;

        // --- 2. LA PROPIEDAD DE LOS PASOS ---
        // Esta lista contiene la secuencia física de la receta.
        public List<NewStepLink> Steps { get; set; } = new();

        // --- 3. MÉTRICAS CALCULADAS (En tiempo real) ---
        // Estas propiedades no guardan datos, los calculan "al vuelo" consultando sus pasos.

        // Suma de lo que la receta dice que debe durar.
        double BatchTeorico => Steps.Sum(s =>
           Math.Max(s.TheoricalDurationSeconds, s.RealDurationSeconds));
        public Amount BCT_Theorical => new Amount(BatchTeorico, TimeUnits.Second);

        // Suma de todos los segundos de "hambre" de todos los pasos.
        double TotalStarvation => Steps.Sum(s => s.AccumulatedStarvation);
        public Amount TimeStarved => new Amount(TotalStarvation, TimeUnits.Second);

        // Suma de los segundos que el equipo ha estado TRABAJANDO físicamente.
        double TotalWorkedSeconds => Steps.Sum(s => s.RealDurationSeconds);
        public Amount BCT_Current => new Amount(TotalWorkedSeconds, TimeUnits.Second);

        double TotalSeconds => Steps.Sum(s => s.RealDurationSeconds + s.AccumulatedStarvation);
        double CalculatedBatchReal => BatchTeorico + TotalStarvation;

        public Amount BCT_Expected => new Amount(CalculatedBatchReal, TimeUnits.Second);

        // --- 4. INDICADORES DE EFICIENCIA ---

        // $$OEE = \frac{BatchTeorico}{CalculatedBatchReal} \times 100$$
        public double AU_Performance
        {
            get
            {
                if (TotalSeconds <= 0) return 100;
                double oee = (TotalWorkedSeconds / TotalSeconds) * 100;
                return oee > 100 ? 100 : oee; // Capado al 100% si vamos más rápido
            }
        }
        public DateTime PlannedEndBatch => Steps.MaxBy(s => s.PlannedEnd)?.PlannedEnd ?? _mixer.CurrentDate;
        public DateTime PlannedTheoricalEndBatch => StartBatchDate.AddSeconds(BatchTeorico);
        // Indica cuánto tiempo falta para terminar esta orden específica.
        TimeSpan _TimeToComplete
        {
            get
            {
                var lastStep = Steps.LastOrDefault();
                if (lastStep == null) return TimeSpan.Zero;

                // Usamos la fecha esperada del último paso menos el ahora del simulador.
                var diff = lastStep.PlannedEnd - lastStep.CurrentDate;
                return diff.TotalSeconds > 0 ? diff : TimeSpan.Zero;
            }
        }

        public Queue<NewStepLink> ActivePipeline { get; set; } = new Queue<NewStepLink>();
        public Queue<NewStepLink> BatchRecordHistory { get; set; } = new Queue<NewStepLink>();

    }

}
