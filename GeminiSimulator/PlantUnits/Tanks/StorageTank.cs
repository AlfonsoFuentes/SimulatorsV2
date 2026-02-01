using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Materials;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.PlantUnits.Tanks
{
    public class StorageTank : PlantUnit
    {
        // --- NIVELES (Configuración Física/Operativa) ---

        // Capacidad Geométrica total (hasta el borde)
        public Amount PhysicalCapacity { get; private set; }

        // Nivel Máximo de Operación (High Level) - Esta es la capacidad "real" de uso
        public Amount WorkingCapacity { get; private set; }

        // Nivel Mínimo de Operación (Low Level) - Para evitar cavitación de bombas, etc.
        public Amount MinWorkingLevel { get; private set; }

        // Nivel Crítico Bajo (Low-Low) - Parada de emergencia
        public Amount CriticalMinLevel { get; private set; }

        // --- ESTADO INICIAL ---
        public Amount InitialLevel { get; private set; }

        // --- RESTRICCIONES ---
        public bool IsDedicated { get; private set; } // ¿Solo acepta un tipo de fluido?
        public MaterialType AllowedMaterialType { get; private set; } // ¿Qué tipo de material guarda?

        // CONSTRUCTOR (Muchos parámetros, pero necesarios para un tanque completo)
        public StorageTank(
            Guid id,
            string name,
            ProccesEquipmentType type,
            FocusFactory factory,
            Amount physicalCapacity,
            Amount workingCapacity,
            Amount minLevel,
            Amount criticalMinLevel,
            Amount initialLevel,
            bool isDedicated,
            MaterialType allowedType)
            : base(id, name, type, factory)
        {
            PhysicalCapacity = physicalCapacity;
            WorkingCapacity = workingCapacity;
            MinWorkingLevel = minLevel;
            CriticalMinLevel = criticalMinLevel;
            InitialLevel = initialLevel;
            IsDedicated = isDedicated;
            AllowedMaterialType = allowedType;
        }

        // --- POLIMORFISMO ---

        /// <summary>
        /// Para un Tanque, la capacidad disponible es su Nivel Máximo Operativo (WorkingCapacity).
        /// Ignoramos el parámetro 'product' porque el volumen del tanque es fijo 
        /// (a menos que quieras simular densidad, pero por ahora asumimos consistencia).
        /// </summary>
        public override Amount GetCapacity(ProductDefinition? product = null)
        {
            return WorkingCapacity;
        }



    }
    public abstract class ProcessTank : StorageTank
    {
        public virtual Amount AverageOutleFlow => new Amount(0, MassFlowUnits.Kg_sg);
        public virtual Amount PendingTimeToEmptyVessel =>
           new Amount(0, TimeUnits.Second);
        public ProductDefinition? CurrentMaterial { get; private set; }
        // Constructor de copia: Pasa los datos del StorageTank "plano" al especializado
        protected List<Pump> _outletPumps = new();
        protected ProcessTank(StorageTank baseTank)
            : base(baseTank.Id, baseTank.Name, baseTank.Type, baseTank.FocusFactory,
                   baseTank.PhysicalCapacity, baseTank.WorkingCapacity, baseTank.MinWorkingLevel,
                   baseTank.CriticalMinLevel, baseTank.InitialLevel, baseTank.IsDedicated,
                   baseTank.AllowedMaterialType)
        {
            foreach (var input in baseTank.Inputs) this.AddInlet(input.Id);
            foreach (var output in baseTank.Outputs) this.AddOutlet(output.Id);
            foreach (var material in baseTank.Materials) this.SetProductCapability(material, new Amount(0, UnitMeasure.None));
            if (Materials.Count == 1)
            {
                CurrentMaterial = Materials[0];
            }

        }
        public double MassScheduledToReceive { get; private set; }
        public void AddMassScheduledToReceive(double mass)
        {
            MassScheduledToReceive += mass;
        }
        public double InletFlow { get; private set; } = 0;
        // En ProcessTank.cs

        public virtual void SetInletFlow(double flow)
        {
            // 1. Recibimos el flujo físico
            InletFlow = flow;
            CurrentLevel += flow;

            // 2. CORRECCIÓN CRÍTICA:
            // A medida que entra lo físico, descontamos de lo "prometido".
            // Es una transferencia de contabilidad: Virtual -> Real.
            MassScheduledToReceive -= flow;

            // 3. Seguro contra números negativos (Floating point errors)
            // A veces por 0.00001 se va a negativo y puede confundir al scheduler.
            if (MassScheduledToReceive < 0) MassScheduledToReceive = 0;

            // 4. Reset del flujo instantáneo para el siguiente ciclo
            InletFlow = 0;
        }
        public double OutletFlow { get; private set; } = 0;
        public virtual void SetOutletFlow(double flow)
        {
            OutletFlow = flow;
            CurrentLevel -= flow;
            OutletFlow = 0;
            TotalMassProduced += flow;
        }
        public double CurrentLevel { get; private set; }
        public void SetCurrentMaterial(ProductDefinition material)
        {

            CurrentMaterial = material;
        }
        public override Dictionary<string, ReportField> GetReportData()
        {
            var data = base.GetReportData(); // Trae el "Name"

            // 1. Caso: Línea no programada
            data.Add("Level", new ReportField($"{CurrentLevel:F1} Kg"));
            data.Add("Inlet State", new ReportField(_inboundState?.StateName ?? string.Empty));
            data.Add("Outlet State", new ReportField(_outboundState?.StateName ?? string.Empty));

            foreach (var pump in Outputs.OfType<Pump>().OrderBy(x => x.Name))
            {
                data.Add(pump.Name, new ReportField(pump.OutboundState?.StateName ?? string.Empty, "", true));

                foreach (var queue in pump.RequestQueue)
                {
                    data.Add($"{queue.Name}", new ReportField(""));

                }

            }

            return data;
        }

        public override void CheckInitialStatus(DateTime InitialDate)
        {
            _outletPumps = Outputs.OfType<Pump>().ToList();
            CurrentLevel = InitialLevel.GetValue(MassUnits.KiloGram);
           
        }
        protected double TotalMassProduced { get;  set; }

        public override void Update()
        {


        }
        public override void Notify()
        {
            foreach (var pump in _outletPumps)
            {
                pump.Update();
            }
        }
        public void TransitionOutbound(OutletStorageTankState newState)
           => TransitionOutboundInternal(newState);
        public void TransitionInBound(TankInletState newState)
       => TransitionInBoundInternal(newState);


        private PlantUnit? _currentOwner;
        public PlantUnit? CurrentOwner => _currentOwner;
        private Queue<PlantUnit> _requestQueue = new Queue<PlantUnit>();
        public bool IsOwnedBy(PlantUnit unit) => _currentOwner == unit;
        public virtual void RequestAccess(PlantUnit requester)
        {
            if (_currentOwner == requester) return;

            if (_currentOwner == null && _requestQueue.Count == 0)
            {
                _currentOwner = requester;
                TransitionInBound(new TankReceiving(this));
                return;
            }

            if (!_requestQueue.Contains(requester))
            {
                _requestQueue.Enqueue(requester);
            }
        }

        public void ReleaseAccess(PlantUnit requester)
        {
            if (_currentOwner == requester)
            {

                _currentOwner = null;
                ProcessNextInQueue();
            }
            else
            {
                _requestQueue = new Queue<PlantUnit>(_requestQueue.Where(x => x != requester));
            }
        }

        private void ProcessNextInQueue()
        {
            if (_requestQueue.Count > 0)
            {
                var nextCandidate = _requestQueue.Dequeue();
                if (DoesCandidateStillNeedOperator(nextCandidate))
                {
                    _currentOwner = nextCandidate;
                    TransitionInBound(new TankReceiving(this));
                    return;
                }
                else
                {
                    ProcessNextInQueue();
                }
            }
            TransitionInBound(new TankAvailable(this));
        }
        private bool DoesCandidateStillNeedOperator(PlantUnit candidate)
        {
            if (candidate is BatchMixer mixer)
            {
                return mixer.InboundState is MixerDischargingStarvedByTankBusy;
            }
            // Aquí agregarás el caso del Mixer: "|| candidate is Mixer ..."
            return false;
        }

    }
    public class RawMaterialTank : ProcessTank
    {

        public RawMaterialTank(StorageTank baseTank) : base(baseTank)
        {



        }
        public void TransitionInbound(InletRawMaterialTankState newState)
        => TransitionInBoundInternal(newState);
        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
            TransitionInbound(new NormalCapacityState(this));
            if (IsOnPlannedBreak(InitialDate))
            {
                TransitionOutbound(new TankPlannedDowntimeState(this));
                return;
            }
            if (CurrentLevel < CriticalMinLevel.GetValue(MassUnits.KiloGram))
            {
                TransitionOutbound(new TankLoLevelState(this));
                return;
            }
            TransitionOutbound(new TankAvailableState(this));
        }
        public void CalculateOutleFlows()
        {
            double totalOutflow = 0;

            foreach (var pump in _outletPumps)
            {
                if (pump.CurrentFlowRate > 0)
                {
                    totalOutflow += pump.CurrentFlowRate;
                }
            }
            SetOutletFlow(totalOutflow);

        }

    }
    public class InHouseTank : ProcessTank
    {
        public InHouseTank(StorageTank baseTank) : base(baseTank) { }
      
        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
            TransitionInBound(new TankAvailable(this));
            if (IsOnPlannedBreak(InitialDate))
            {
                TransitionOutbound(new TankLoLevelState(this));
                return;
            }
            if (CurrentLevel < CriticalMinLevel.GetValue(MassUnits.KiloGram))
            {
                TransitionOutbound(new TankLoLevelState(this));
                return;
            }
            TransitionOutbound(new TankAvailableState(this));
 
        }
        public void CalculateOutleFlows()
        {
            double totalOutflow = 0;

            foreach (var pump in _outletPumps)
            {
                if (pump.CurrentFlowRate > 0)
                {
                    totalOutflow += pump.CurrentFlowRate;
                }
            }
            SetOutletFlow(totalOutflow);

        }

        private double ProductionRatePerSecond
        {
            get
            {
                if (TotalSeconds == 0) return 0;
                return TotalMassProduced / TotalSeconds;
            }
        }
        private double TotalMassInProcess => MassScheduledToReceive + CurrentLevel;

        public Amount MassInProcess => new Amount(TotalMassInProcess, MassUnits.KiloGram);

        public override Amount AverageOutleFlow => new Amount(ProductionRatePerSecond, MassFlowUnits.Kg_sg);
        double PendingTimeToEmptyVesselSeconds =>
            ProductionRatePerSecond == 0 ? 0 : TotalMassInProcess / ProductionRatePerSecond;

        public override Amount PendingTimeToEmptyVessel =>
            new Amount(PendingTimeToEmptyVesselSeconds, TimeUnits.Second);

       
        public void CountersToZero()
        {
            TotalMassProduced = 0;
            TotalSeconds = 0;

        }
    }


}
