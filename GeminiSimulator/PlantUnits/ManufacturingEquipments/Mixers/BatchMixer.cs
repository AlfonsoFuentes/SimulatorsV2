using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Main;
using GeminiSimulator.Materials;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Operators;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps;
using GeminiSimulator.PlantUnits.Tanks;
using QWENShared.Enums;
using UnitSystem;




namespace GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers
{
    public enum MixerStateCategory
    {
        Batching,           // Producción efectiva y micro-paradas de AU
        Washing,          // Problemas de entrada (WIP vacío)
        StarvedByWashoutPump,       // Cambios de formato, lavados, averías
        StarvedByOperator,         // Paradas programadas (almuerzos, reuniones)
        StarvedByFeederPump,   // Fuera de turno o sin programación
        Discharging,       // Descarga del lote
        StarvedByTankBusy,    // Esperando tanque de proceso
        StarvedByTankHighLevel, // Esperando operario al inicio del lote

    }
    public class BatchMixer : EquipmentManufacture
    {
        
        private readonly Dictionary<MixerStateCategory, double> _stateAccumulator = new();
        public Dictionary<MixerStateCategory, double> StateAcumulator=> _stateAccumulator;

        private OptimizationEngine _ai = null!;

        public void SetOptimizationEngine(OptimizationEngine ai)
        {
            _ai = ai;
        }

        // --- ESTADO FÍSICO ---
        public double Capacity { get; private set; }
        public double CurrentMass { get; set; }
        public double DischargeRate { get; private set; }
        public double DischargeTimeInSeconds => DischargeRate == 0 ? 100000 : Capacity / DischargeRate;

        // --- CONTEXTO DE PRODUCCIÓN ---
        public ProductDefinition? LastMaterialProcessed { get; set; }
        public ProductDefinition? CurrentMaterial { get; set; }
        public ProcessTank? CurrentWipTank { get; set; }

        // --- VARIABLES DE CONTROL (State Data) ---
        public Queue<ProcessTank> WipsQueue { get; set; } = new();
        Queue<RecipeStep> MaterialSteps { get; set; } = new();
        public Pump? OutletPump { get; set; }
        // --- RECURSOS ---
        public Pump? CurrentFillingPump { get; set; }
        public Operator? BatchOperator { get; set; }
        public Pump? WashingPump { get; set; }

        public List<Pump> InletPumps { get; set; } = new List<Pump>();

        // --- CONFIGURACIÓN ---
        public OperatorEngagementType EngagementType { get; private set; }
        public double OperatorStdSetupTime { get; private set; }
        public RecipeStep? CurrentStep { get; private set; }
        public int NetBatchTimeInSeconds { get; set; } = 0;
        public int NetStarvedTimeInSeconds { get; set; } = 0;
        public int TotalBatchTimeInSeconds => NetBatchTimeInSeconds + NetStarvedTimeInSeconds;
        public BatchMixer(Guid id, string name, ProcessEquipmentType type, FocusFactory factory)
            : base(id, name, type, factory)
        {
            foreach (MixerStateCategory cat in Enum.GetValues(typeof(MixerStateCategory)))
            {
                _stateAccumulator[cat] = 0;
            }
        }
        public void AccumulateTime(MixerStateCategory category)
        {
            _stateAccumulator[category] += 1;
        }
        public IReadOnlyDictionary<MixerStateCategory, double> FullReport => _stateAccumulator;
        public override Dictionary<string, ReportField> GetReportData()
        {
            var data = base.GetReportData();
            // Solo reportamos el estado único que gobierna todo
            data.Add("Status", new ReportField(_inboundState?.StateName ?? "Idle"));
            data.Add("Step Status", new ReportField(_inboundState?.SubStateName ?? "Idle"));
            data.Add("Current Level", new ReportField($"{CurrentMass:F1} / {Capacity:F1} Kg"));

            try
            {
                if (BatchOperator != null) data.Add("Operator", new ReportField(BatchOperator.Name));
                if (CurrentWipTank != null) data.Add("Producing to", new ReportField(CurrentWipTank.Name));
                if (CurrentMaterial != null)
                {
                    data.Add("Recipe", new ReportField(CurrentMaterial.Name));
                    data.Add("Net Batch Time", new ReportField($"{NetBatchTimeInSeconds}, s"));
                    data.Add("Net Starved Time", new ReportField($"{NetStarvedTimeInSeconds}, s"));
                    data.Add("Total BatchTime", new ReportField($"{TotalBatchTimeInSeconds}, s"));
                }

                if (WipsQueue.Count > 0)
                {
                    data.Add("Queue", new ReportField(""));
                    foreach (var wip in WipsQueue)
                    {
                        data.Add($" - {wip.Name}", new ReportField(wip.CurrentMaterial?.Name ?? "No Material"));
                    }
                }
            }
            catch (Exception ex)
            {
                data.Add("Report Error", new ReportField(ex.Message));
            }



            return data;
        }

        public override void CheckInitialStatus(DateTime InitialDate)
        {

            OperatorStdSetupTime = Context?.TimeOperatorOcupy.GetValue(TimeUnits.Second) ?? 0;
            BatchOperator = Inputs.OfType<Operator>().FirstOrDefault() ?? null;
            OutletPump = Outputs.OfType<Pump>().FirstOrDefault() ?? null;
            DischargeRate = OutletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
            EngagementType = Context?.OperatorEngagementType ?? OperatorEngagementType.Infinite;

            WashingPump = Inputs.OfType<Pump>().FirstOrDefault(x => x.IsForWashing) ?? null;
            InletPumps = Inputs.OfType<Pump>().Where(x => !x.IsForWashing).ToList() ?? new();
            // Inicio en Idle
            TransitionInBound(new MixerIdle(this));
        }
        public void ReceivePriorityRequirementBatch(ProcessTank wip)
        {
            if (wip == null || wip.CurrentMaterial == null) return;

            // Si el mixer no está haciendo nada, entra normal.
            if (_inboundState is MixerIdle)
            {
                ReceiveRequirementBatch(wip);
                return;
            }

            // TRUCO: Convertimos la Cola en Lista, insertamos de primero y volvemos a hacer Cola.
            // Esto desplaza a los otros WIPs que estaban esperando.
            var list = WipsQueue.ToList();

            // Verificamos que no esté ya en la cola para no duplicar
            if (!list.Contains(wip))
            {
                list.Insert(0, wip); // Posición 0 = El siguiente en ser atendido
                WipsQueue = new Queue<ProcessTank>(list);
            }
        }
        // =================================================================================
        // EL TRIGGER: Solo inicia la secuencia
        // =================================================================================
        public void ReceiveRequirementBatch(ProcessTank wip)
        {
            if (wip == null) return;

            if (wip.CurrentMaterial == null) return;
            if (_inboundState is not MixerIdle)
            {
                WipsQueue.Enqueue(wip);
                return;
            }


            SelectInitState(wip);




        }
       
        public int CounterOperatorRelease = 0;
        public double PendingOperatorRealse => OperatorStdSetupTime - CounterOperatorRelease;

        public int CurrentTotalSteps => CurrentMaterial?.Recipe?.Steps.Count ?? 0;
        public void SelectInitState(ProcessTank wip)
        {

            CurrentWipTank = wip;
            CurrentMaterial = wip.CurrentMaterial;
            CurrentFillingPump = null;
            if (CurrentMaterial == null) return;
            Capacity = this._productCapabilities[CurrentMaterial].GetValue(MassUnits.KiloGram);
            var steps = CurrentMaterial?.Recipe?.Steps.OrderBy(x => x.Order).ToList();
            if (steps!.Count == 0) return;
            CurrentWipTank.AddMassScheduledToReceive(Capacity);

            foreach (var step in steps)
            {
                MaterialSteps.Enqueue(step);
            }
            //1. seleccionamos el inicio por como opera el operario
            if (EngagementType == OperatorEngagementType.FullBatch || EngagementType == OperatorEngagementType.StartOnDefinedTime)
            {
                if (BatchOperator?.InboundState is OperatorNotAvailable|| BatchOperator?.InboundState is IOperatorPlanned)
                {
                    BatchOperator?.RequestAccess(this);  //Como el operario no esta disponible aqui lo pnemos en cola
                    TransitionInBound(new MixerStarvedByAtInitOperator(this));     //en esta caso si el operario esta ocupado con el tanque de al lado se va a esperar que lo desocupe
                    return;    //Devulve return porque encontro donde iniciar el batche no necesita revisar el inicio del batche
                }
                BatchOperator?.RequestAccess(this);  //Como el operario esta disponible aqui lo asignamos a este equipo para que el otro mixer no lo pueda usar en esta opcion   y sigue a lavar si se requiere

            }

            //verificamos si toca lavar o no    , si no toca lavar inicia el batche
            if (NeedsWashing())
            {
                if (WashingPump?.OutboundState is PumpAvailable)
                {
                    TransitionInBound(new MixerManagingWashing(this));
                    return;   //Devulve return porque encontro donde iniciar el batche=>en el lavado, no necesita revisar el inicio del batche
                }
                TransitionInBound(new MixerManagingWashingStarved(this));
                return;      //Devulve return porque encontro donde iniciar el batche=>esperando que la bomba se desocupe no necesita revisar el inicio del batche
            }
            CounterOperatorRelease = 0;
            //Inicia el batche por el primero de la lista de la receta
            SelectNextState();

        }

        public void SelectNextState()
        {
            if (MaterialSteps.Count == 0)
            {
                if (_ai != null && CurrentMaterial != null)
                {
                    _ai.RegisterBatchCompletion(this.Id, CurrentMaterial.Id, TotalBatchTimeInSeconds);

                    // Opcional: Log para ver que está aprendiendo
                    Console.WriteLine($"[AI LEARNING] Mixer {Name} finished {CurrentMaterial.Name} in {TotalBatchTimeInSeconds}s");
                }
                //significa que ya termino el batche libera el operario en el caso 1...
                if (BatchOperator?.CurrentOwner==this)
                {
                    BatchOperator?.ReleaseAccess(this);
                }
                if (CurrentWipTank?.InboundState is TankAvailable)
                {
                    CurrentWipTank?.RequestAccess(this);
                    TransitionInBound(new MixerDischarging(this));
                    return;
                }
                CurrentWipTank?.RequestAccess(this);
                TransitionInBound(new MixerDischargingStarvedByTankBusy(this));
                return;


            }
            CurrentStep = MaterialSteps.Dequeue();
            if (CurrentStep.OperationType == BackBoneStepType.Add)
            {
                var feederpumps = InletPumps.Where(x => x.Materials.Any(x => x.Id == CurrentStep.IngredientId));
                var freePump = feederpumps.FirstOrDefault(x => x.OutboundState is PumpAvailable);

                if (freePump != null)
                {
                    CurrentFillingPump = freePump;
                    CurrentFillingPump.RequestAccess(this);
                    TransitionInBound(new MixerFillingWithPump(this));
                    return;
                }
                else
                {
                    // 2. PRIORIDAD: BOMBAS OCUPADAS (ELEGIR LA MEJOR)
                    // <--- AQUI LA INTELIGENCIA: Ordenamos por tiempo de espera estimado
                    var bestBusyPumps = feederpumps
                        .Where(x => x.OutboundState is PumpOutletInUse)
                        .OrderBy(p => p.QueueCount).ToList();
                    var bestBusyPump = bestBusyPumps.FirstOrDefault();
                    if (bestBusyPump != null)
                    {
                        CurrentFillingPump = bestBusyPump;
                        CurrentFillingPump.RequestAccess(this); // Nos ponemos en cola
                        TransitionInBound(new MixerFillingStarvedWithPump(this));
                        return;
                    }
                }
                //Como no fue adicionada por bomba es adioncaod por operario
                TransitionInBound(new MixerFillingManual(this));


            }
            else
            {
                TransitionInBound(new MixerProcessingTime(this));
            }
        }
        public double CurrentStepPendingMass { get; set; }

        public RecipeStep GetStep(int order) => CurrentMaterial?.Recipe?.Steps?.FirstOrDefault(x => x.Order == order) ?? null!;

        public bool NeedsWashing()
        {
            if (LastMaterialProcessed == null) return false;
            if (CurrentMaterial == null) return false;
            return LastMaterialProcessed.Id != CurrentMaterial.Id;
        }

        public void CheckOperatorStatus()
        {
            if (EngagementType == OperatorEngagementType.StartOnDefinedTime)
            {
                if (BatchOperator?.CurrentOwner == this)
                {
                    if (PendingOperatorRealse <= 0)
                    {
                        BatchOperator?.ReleaseAccess(this);
                        CounterOperatorRelease = 0;
                    }

                }

            }
        }
        public void CalculateOperatorStatus()
        {
            if (EngagementType == OperatorEngagementType.StartOnDefinedTime)  //Como operario fue liberado cuenta el tiempo de uso para liberarlo
            {
                if (BatchOperator?.CurrentOwner == this)
                {
                    CounterOperatorRelease++;
                }

            }
        }
        // Helper para exponer la transición protegida
        public void TransitionInBound(MixerInletState newState) => TransitionInBoundInternal(newState);
    }

}
