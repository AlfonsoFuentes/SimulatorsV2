//namespace GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers
//{
//    public enum OperatorEngagementType
//    {
//        Infinite,           // Opción 1: Siempre disponible
//        FullBatch,          // Opción 2: Se toma al inicio y se suelta al final
//        StartOnly,          // Opción 3: Se toma al inicio y se suelta en el primer paso automático
//        StartOnDefinedTime  // Opción 4: Se libera tras un tiempo fijo definido en BD
//    }
//    public class BatchMixer : EquipmentManufacture
//    {
//        // --- PROPIEDADES FÍSICAS ---
//        public double Capacity { get; private set; }
//        public double CurrentMass { get; private set; }
//        public double DischargeRate { get; private set; } = 5.0; // Kg/s (Velocidad de vaciado)

//        // --- REFERENCIAS DE PROCESO ---
//        public ProductDefinition? LastMaterialProcessed { get; private set; }
//        public ProductDefinition? CurrentMaterial { get; private set; }
//        public BatchWipTank? CurrentWipTank { get; private set; } // El destino

//        // --- VARIABLES DE RECETA ---
//        private int _currentStepIndex = 0;
//        private double _massAccumulatedInStep = 0;
//        private double _targetMassForStep = 0;
//        private double _stepTimeRemaining = 0;
//        private OperatorEngagementType EngagementType;
//        // --- RECURSOS ---
//        private Pump? _currentFillingPump;
//        private Operator? _batchOperator; // El operario asignado (si es modo 2 o 3)
//        private Pump? _washingPump;
//        private Pump? _OutletPump;
//        public double WashingTimeRemaining { get; private set; }
//        public double OperatorStdSetupTime { get; private set; }
//        private double _operatorLockTimer = 0; // Contador regresivo interno
//        private List<Pump> _inletPumps = new List<Pump>();
//        public BatchMixer(Guid id, string name, ProccesEquipmentType type, FocusFactory factory)
//            : base(id, name, type, factory)
//        {

//        }

//        public override Dictionary<string, ReportField> GetReportData()
//        {
//            var data = base.GetReportData();
//            data.Add("Inlet State", new ReportField(_inboundState?.StateName ?? "Idle"));
//            data.Add("Outlet State", new ReportField(_outboundState?.StateName ?? "Idle"));
//            data.Add("Level", new ReportField($"{CurrentMass:F1} / {Capacity:F1} Kg"));

//            if (CurrentMaterial != null)
//                data.Add("Product", new ReportField(CurrentMaterial.Name));

//            if (_batchOperator != null)
//                data.Add("Operator", new ReportField(_batchOperator.Name));

//            if (_inboundState is MixerDischarging && CurrentWipTank != null)
//                data.Add("Target WIP", new ReportField(CurrentWipTank.Name));

//            return data;
//        }
//        public override void CheckInitialStatus(DateTime InitialDate)
//        {
//            _batchOperator = Inputs.OfType<Operator>().FirstOrDefault() ?? null;
//            _OutletPump = Outputs.OfType<Pump>().FirstOrDefault() ?? null;
//            DischargeRate = _OutletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
//            EngagementType = Context?.OperatorEngagementType ?? OperatorEngagementType.Infinite;
//            OperatorStdSetupTime = Context?.TimeOperatorOcupy.GetValue(TimeUnits.Second) ?? 0;
//            _washingPump = Inputs.OfType<Pump>().FirstOrDefault(x => x.IsForWashing) ?? null;
//            _inletPumps = Inputs.OfType<Pump>().Where(x => !x.IsForWashing).ToList() ?? new();
//            TransitionInBound(new MixerIdle(this));
//            TransitionOutbound(new MixerOutletIdle(this));
//        }
//        // =================================================================================
//        // 1. INICIALIZACIÓN (El Trigger)
//        // =================================================================================
//        public void ReceiveRequirementFromWIP(BatchWipTank wip)
//        {
//            if (wip == null) return;
//            CurrentWipTank = wip;
//            CurrentMaterial = wip.CurrentMaterial;

//            // A. VALIDACIÓN: Revisar si hay bombas para los ingredientes
//            if (!CheckIngredientsAvailability())
//            {
//                TransitionInBound(new MixerErrorState(this, "Missing Ingredient Pumps"));
//                return;
//            }

//            // B. RECURSO HUMANO: Intentar adquirir Operario (Si es Modo 2 o 3)
//            if (!TryAcquireOperatorForStart())
//            {
//                TransitionInBound(new MixerWaitingForOperator(this));
//                return; // Rebotamos hasta que el operario esté libre
//            }

//            // C. LOGICA DE CAMBIO: Verificar Lavado
//            if (NeedsWashing())
//            {
//                SetupWashing();
//            }
//            else
//            {
//                StartRecipeExecution();
//            }
//        }

//        private bool CheckIngredientsAvailability()
//        {
//            if (CurrentMaterial?.Recipe == null) return true;
//            foreach (var step in CurrentMaterial.Recipe.Steps)
//            {
//                if (step.IsMaterialAddition)
//                {
//                    // Validamos si hay Bomba u Operario capaz de hacerlo
//                    bool hasPump = Inputs.OfType<Pump>().Any(p => p.Materials.Any(x => x.Id == step.IngredientId));
//                    bool hasOp = Inputs.OfType<Operator>().Any(op => op.Materials.Any(x => x.Id == step.IngredientId));
//                    if (!hasPump && !hasOp) return false;
//                }
//            }
//            return true;
//        }

//        // =================================================================================
//        // 2. GESTIÓN DE OPERARIO (Reglas 1, 2 y 3)
//        // =================================================================================
//        private bool TryAcquireOperatorForStart()
//        {

//            if (EngagementType == OperatorEngagementType.Infinite) return true;

//            // Intentar Reservar (Para opciones 2, 3 y 4)
//            _batchOperator.RequestAccess(this);
//            if (_batchOperator.IsOwnedBy(this))
//            {


//                // --- LOGICA NUEVA PARA OPCIÓN 4 ---
//                if (EngagementType == OperatorEngagementType.StartOnDefinedTime)
//                {
//                    // Cargamos el cronómetro con el valor de la BD
//                    _operatorLockTimer = OperatorStdSetupTime;
//                }

//                return true;
//            }
//            return false; // Ocupado
//        }
//        public void ManageOperatorLifecycle(bool isAutomaticStep)
//        {
//            if (_batchOperator == null) return;

//            // --- REGLA OPCIÓN 3: StartOnly ---
//            // Si el paso es automático (Bomba o Tiempo) -> Liberar inmediatamente
//            if (EngagementType == OperatorEngagementType.StartOnly && isAutomaticStep)
//            {
//                ReleaseOperator();
//                return;
//            }

//            // --- REGLA OPCIÓN 4: StartOnDefinedTime ---
//            // Descontamos tiempo independientemente de qué esté haciendo el mixer
//            if (EngagementType == OperatorEngagementType.StartOnDefinedTime)
//            {
//                _operatorLockTimer -= 1; // Asumiendo 1 tick = 1 seg

//                if (_operatorLockTimer <= 0)
//                {
//                    ReleaseOperator();
//                }
//            }
//        }

//        private void CheckOperatorRelease(bool isAutomaticStep)
//        {
//            if (_batchOperator == null) return;

//            // Regla 3: Si es StartOnly y llegamos a un paso automático -> Liberar
//            if (EngagementType == OperatorEngagementType.StartOnly && isAutomaticStep)
//            {
//                _batchOperator.ReleaseAccess(this);
//                _batchOperator = null;
//            }
//            // Regla 2: FullBatch NO se libera aquí, sino al final.
//        }

//        // =================================================================================
//        // 3. LOGICA DE LAVADO (Changeover)
//        // =================================================================================
//        private bool NeedsWashing()
//        {
//            if (LastMaterialProcessed == null) return false; // Arranque en frío
//            if (CurrentMaterial == null) return false;
//            return LastMaterialProcessed.Id != CurrentMaterial.Id; // Diferente producto
//        }

//        private void SetupWashing()
//        {
//            // Nota: Si el operario es StartOnly, lo mantenemos durante el lavado.
//            _washingPump = Inputs.OfType<Pump>().FirstOrDefault(p => p.IsForWashing);

//            if (_washingPump == null)
//            {
//                // Si no hay bomba de lavado, pasamos directo (o error, según rigor)
//                StartRecipeExecution();
//                return;
//            }

//            // Calculamos tiempo (Matriz simulada)
//            WashingTimeRemaining = 300; // 5 min default
//            TransitionInBound(new MixerWaitingForWashPump(this));
//        }

//        public void ExecuteWashingProcess()
//        {
//            if (_washingPump == null) return;

//            _washingPump.RequestAccess(this);
//            if (!_washingPump.IsOwnedBy(this)) return; // Esperar cola

//            // Tenemos bomba -> Lavando
//            if (!(_inboundState is MixerWashing)) TransitionInBound(new MixerWashing(this));

//            double maxFlow = _washingPump.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg);
//            _washingPump.SetCommandedFlow(maxFlow, this);

//            WashingTimeRemaining -= 1;

//            if (WashingTimeRemaining <= 0)
//            {
//                // Fin Lavado
//                _washingPump.SetCommandedFlow(0, this);
//                _washingPump.ReleaseAccess(this);
//                _washingPump = null;

//                // Memoria limpia (simbólicamente)
//                StartRecipeExecution();
//            }
//        }

//        // =================================================================================
//        // 4. MOTOR DE RECETA (Secuencial)
//        // =================================================================================
//        private void StartRecipeExecution()
//        {
//            _currentStepIndex = 0;
//            CurrentMass = 0;
//            // Estado Externo: Avisamos al WIP que estamos ocupados
//            TransitionOutbound(new MixerOutletPreparingBatch(this));
//            PrepareStep();
//        }

//        private void PrepareStep()
//        {
//            // Limpieza vars paso anterior
//            _massAccumulatedInStep = 0;
//            _currentFillingPump = null;
//            _batchOperator = null; // Limpiamos referencia local (no el _batchOperator global)
//            _stepTimeRemaining = 0;

//            // Verificar Fin de Receta
//            if (CurrentMaterial?.Recipe == null || _currentStepIndex >= CurrentMaterial.Recipe.Steps.Count)
//            {
//                FinishCooking();
//                return;
//            }

//            var step = CurrentMaterial.Recipe.Steps[_currentStepIndex];

//            // A. PASO DE TIEMPO
//            if (step.IngredientId == null || step.IngredientId == Guid.Empty)
//            {
//                // Paso automático -> Liberar Operario si es StartOnly
//                CheckOperatorRelease(isAutomaticStep: true);

//                _stepTimeRemaining = step.Duration.GetValue(TimeUnits.Second);
//                TransitionInBound(new MixerProcessingTime(this));
//                return;
//            }

//            // B. PASO DE MATERIAL
//            _targetMassForStep = Capacity * (step.TargetPercentage / 100.0);

//            // B.1 ¿Bomba?
//            bool handledByPump = Inputs.OfType<Pump>().Any(p => p.Materials.Any(x => x.Id == step.IngredientId));
//            if (handledByPump)
//            {
//                // Paso automático -> Liberar Operario si es StartOnly
//                CheckOperatorRelease(isAutomaticStep: true);
//                TransitionInBound(new MixerFillingWithPump(this));
//                return;
//            }

//            // B.2 ¿Operario? (Manual Add)
//            bool handledByOperator = Inputs.OfType<Operator>().Any(op => op.Materials.Any(x => x.Id == step.IngredientId));
//            if (handledByOperator)
//            {
//                // Paso manual -> Mantenemos operario
//                TransitionInBound(new MixerFillingWithOperator(this));
//                return;
//            }
//        }

//        // --- LÓGICA DE EJECUCIÓN (Llamada por los estados) ---

//        public void ExecuteTimeStep()
//        {
//            _stepTimeRemaining -= 1;
//            if (_stepTimeRemaining <= 0)
//            {
//                _currentStepIndex++;
//                PrepareStep();
//            }
//        }

//        public void ExecuteFillingWithPump()
//        {
//            var step = CurrentMaterial!.Recipe!.Steps[_currentStepIndex];

//            // 1. Selección Inteligente (Libre > Cola Corta)
//            if (_currentFillingPump == null)
//            {
//                _currentFillingPump = GetBestPumpForMaterial(step.IngredientId);
//                if (_currentFillingPump == null) return;
//            }

//            // 2. Cola de Bomba
//            _currentFillingPump.RequestAccess(this);
//            if (!_currentFillingPump.IsOwnedBy(this)) return;

//            // 3. Llenado Max Flow
//            double maxFlow = _currentFillingPump.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg);

//            // Ajuste fino para no pasarnos
//            double remaining = _targetMassForStep - _massAccumulatedInStep;
//            double command = remaining < maxFlow ? remaining : maxFlow;

//            _currentFillingPump.SetCommandedFlow(command, this);

//            double actualFlow = _currentFillingPump.CurrentFlowRate;
//            _massAccumulatedInStep += actualFlow;
//            CurrentMass += actualFlow;

//            if (_massAccumulatedInStep >= _targetMassForStep)
//            {
//                // Fin de paso
//                _currentFillingPump.SetCommandedFlow(0, this);
//                _currentFillingPump.ReleaseAccess(this);
//                _currentFillingPump = null;
//                _currentStepIndex++;
//                PrepareStep();
//            }
//        }

//        public void ExecuteFillingWithOperator()
//        {
//            // Nota: Si ya tenemos el _batchOperator (Modo 2 o 3), usamos ese.
//            // Si es Modo 1 (Infinito) o no hay _batchOperator, simulamos.

//            // Velocidad manual hardcodeada
//            double manualRate = 2.0;

//            _massAccumulatedInStep += manualRate;
//            CurrentMass += manualRate;

//            if (_massAccumulatedInStep >= _targetMassForStep)
//            {
//                _currentStepIndex++;
//                PrepareStep();
//            }
//        }

//        // =================================================================================
//        // 5. FINALIZACIÓN Y DESCARGA
//        // =================================================================================
//        private void FinishCooking()
//        {
//            // Regla 2: Si el operario era FullBatch, lo liberamos YA.
//            if (_batchOperator != null)
//            {
//                _batchOperator.ReleaseAccess(this);
//                _batchOperator = null;
//            }

//            TransitionInBound(new MixerDischarging(this));
//            TransitionOutbound(new MixerOutletTransferring(this));
//        }

//        public void ExecuteDischarging()
//        {
//            if (CurrentWipTank == null) return;

//            // A. Verificar espacio en WIP (Pausa si está lleno)
//            double wipSpace = CurrentWipTank.WorkingCapacity.GetValue(MassUnits.KiloGram) - CurrentWipTank.CurrentLevel;
//            if (wipSpace <= 0.1) return; // Esperar hueco (Pausa)

//            // B. Transferir
//            double flow = Math.Min(DischargeRate, wipSpace);
//            flow = Math.Min(flow, CurrentMass);

//            CurrentMass -= flow;
//            CurrentWipTank.CurrentLevel += flow;

//            // C. Fin Batch
//            if (CurrentMass <= 0.01)
//            {
//                CurrentMass = 0;

//                // Actualizamos memoria de lavado
//                LastMaterialProcessed = CurrentMaterial;
//                CurrentMaterial = null;
//                CurrentWipTank = null; // Romper vínculo

//                TransitionInBound(new MixerIdle(this));
//                TransitionOutbound(new MixerOutletIdle(this));
//            }
//        }

//        // --- HELPERS ---
//        private Pump? GetBestPumpForMaterial(Guid? ingredientId)
//        {
//            if (ingredientId == null) return null;
//            var candidates = Inputs.OfType<Pump>().Where(p => p.CanProcess(ingredientId.Value)).ToList();
//            if (!candidates.Any()) return null;

//            // Prioridad 1: Libre
//            var freePump = candidates.FirstOrDefault(p => p.IsFree);
//            if (freePump != null) return freePump;

//            // Prioridad 2: Menos cola
//            return candidates.OrderBy(p => p.QueueSize).First();
//        }

//        public void TransitionInBound(MixerInletState newState) => TransitionInBoundInternal(newState);
//        public void TransitionOutbound(MixerOutletState newState) => TransitionOutboundInternal(newState);

//        // Check Status e Initial Status

//    }
//    public abstract class MixerInletState : IUnitState
//    {
//        protected BatchMixer _mixer; public abstract string StateName { get; }
//        public MixerInletState(BatchMixer m) => _mixer = m;
//        public virtual void Calculate() { }
//        public virtual void CheckTransitions() { }
//    }

//    public class MixerIdle : MixerInletState
//    {
//        public MixerIdle(BatchMixer m) : base(m) { }
//        public override string StateName => "Idle";
//    }

//    public class MixerWaitingForOperator : MixerInletState
//    {
//        public MixerWaitingForOperator(BatchMixer m) : base(m) { }
//        public override string StateName => "Waiting for Operator";
//        public override void Calculate()
//        {
//            // Reintenta adquirir operario en cada tick
//            if (_mixer.CurrentWipTank != null) _mixer.ReceiveRequirementFromWIP(_mixer.CurrentWipTank);
//        }
//    }

//    public class MixerErrorState : MixerInletState
//    {
//        private string _msg;
//        public MixerErrorState(BatchMixer m, string msg) : base(m) { _msg = msg; }
//        public override string StateName => $"Error: {_msg}";
//    }

//    // --- ESTADOS DE LAVADO ---
//    public class MixerWaitingForWashPump : MixerInletState
//    {
//        public MixerWaitingForWashPump(BatchMixer m) : base(m) { }
//        public override string StateName => "Queued for CIP Pump";
//        public override void Calculate() { _mixer.ExecuteWashingProcess(); }
//    }

//    public class MixerWashing : MixerInletState
//    {
//        public MixerWashing(BatchMixer m) : base(m) { }
//        public override string StateName => "Washing (CIP)";
//        public override void Calculate() { _mixer.ExecuteWashingProcess(); }
//    }

//    // --- ESTADOS DE RECETA ---
//    public class MixerProcessingTime : MixerInletState
//    {
//        public MixerProcessingTime(BatchMixer m) : base(m) { }
//        public override string StateName => "Processing (Time)";
//        public override void Calculate() { _mixer.ExecuteTimeStep(); }
//    }

//    public class MixerFillingWithPump : MixerInletState
//    {
//        public MixerFillingWithPump(BatchMixer m) : base(m) { }
//        public override string StateName => "Adding (Auto)";
//        public override void Calculate() { _mixer.ExecuteFillingWithPump(); }
//    }

//    public class MixerFillingWithOperator : MixerInletState
//    {
//        public MixerFillingWithOperator(BatchMixer m) : base(m) { }
//        public override string StateName => "Adding (Manual)";
//        public override void Calculate() { _mixer.ExecuteFillingWithOperator(); }
//    }

//    // --- ESTADO FINAL ---
//    public class MixerDischarging : MixerInletState
//    {
//        public MixerDischarging(BatchMixer m) : base(m) { }
//        public override string StateName => "Discharging";
//        public override void Calculate() { _mixer.ExecuteDischarging(); }
//    }

//    // ================================================================
//    // ESTADOS DE SALIDA (SEMÁFORO WIP)
//    // ================================================================
//    public abstract class MixerOutletState : IUnitState
//    {
//        protected BatchMixer _mixer; public abstract string StateName { get; }
//        public MixerOutletState(BatchMixer m) => _mixer = m;
//        public virtual void Calculate() { }
//        public virtual void CheckTransitions() { }
//    }

//    public class MixerOutletIdle : MixerOutletState
//    {
//        public MixerOutletIdle(BatchMixer m) : base(m) { }
//        public override string StateName => "Idle";
//    }

//    public class MixerOutletPreparingBatch : MixerOutletState
//    {
//        public MixerOutletPreparingBatch(BatchMixer m) : base(m) { }
//        // WIP sabe: "Ocupado, pero sin producto aún"
//        public override string StateName => "Preparing Batch";
//    }

//    public class MixerOutletTransferring : MixerOutletState
//    {
//        public MixerOutletTransferring(BatchMixer m) : base(m) { }
//        // WIP sabe: "Recibiendo producto"
//        public override string StateName => "Transferring";
//    }
//}

namespace GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers
{
    public enum OperatorEngagementType
    {
           Infinite,           // Opción 1: Siempre disponible
           FullBatch,          // Opción 2: Se toma al inicio y se suelta al final
           StartOnDefinedTime  // Opción 4: Se libera tras un tiempo fijo definido en BD
}
}
