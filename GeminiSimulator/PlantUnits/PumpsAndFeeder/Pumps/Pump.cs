//namespace GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps
//{
//    public class Pump : PlantUnit
//    {
//        public Amount NominalFlowRate { get; private set; }
//        public bool IsForWashing { get; private set; }

//        public double CurrentFlowRate { get; private set; } = 0;


//        // <--- AQUI AGREGAR ESTO: Hacemos público el dueño (lectura) para que el Mixer pueda preguntarle "quién te tiene"
//        public PlantUnit? CurrentOwner => _currentOwner;
//        private PlantUnit? _currentOwner;

//        private Queue<PlantUnit> _requestQueue = new Queue<PlantUnit>();
//        public ProcessTank? _sourceTank;
//        public Queue<PlantUnit> RequestQueue => _requestQueue;
//        public int QueueCount => _requestQueue.Count;
//        public Pump(Guid id, string name, ProcessEquipmentType type, FocusFactory factory, Amount nominalFlow, bool isForWashing)
//            : base(id, name, type, factory)
//        {
//            NominalFlowRate = nominalFlow;
//            IsForWashing = isForWashing;
//        }



//        public void SetCommandedFlow(double flow, PlantUnit owner)
//        {
//            if (_currentOwner == owner)
//            {

//                CurrentFlowRate = flow;
//                _sourceTank?.SetOutletFlow(flow);

//            }
//        }

//        // =========================================================================
//        // 1. INICIALIZACIÓN (Estado Inicial)
//        // =========================================================================
//        public override void CheckInitialStatus(DateTime InitialDate)
//        {
//            _sourceTank = Inputs.OfType<ProcessTank>().FirstOrDefault();

//            // <--- AQUI CORREGIDO: Arranca Disponible (Verde)
//            TransitionInBound(new PumpInletReady(this));
//            TransitionOutbound(new PumpAvailable(this));
//        }

//        PumpOutletState? laststate = null!;
//        public override void Update()
//        {
//            // Validar Fuente

//            if (_sourceTank?.OutboundState is TankAvailableState)
//            {
//                TransitionInBound(new PumpInletReady(this));
//                if(laststate!=null)
//                TransitionOutbound(laststate!);
//            }
//            else
//            {
//                laststate = OutboundState as PumpOutletState;
//                TransitionInBound(new PumpInletStarved(this));
//                TransitionOutbound(new PumpOutletNotAvailable(this));
//            }



//        }
//        public override void Notify()
//        {
//            _sourceTank?.Update();
//            _currentOwner?.Update();
//        }

//        // =========================================================================
//        // 2. GESTIÓN DE RESERVA (La lógica del Semáforo)
//        // =========================================================================

//        public void RequestAccess(PlantUnit requester)
//        {
//            if (_currentOwner == requester) return;

//            if (_currentOwner == null && _requestQueue.Count == 0)
//            {
//                _currentOwner = requester;
//                // <--- AQUI CORREGIDO: Si se asigna, pasa a NO DISPONIBLE inmediatamente
//                TransitionOutbound(new PumpOutletInUse(this));
//                return;
//            }

//            if (!_requestQueue.Contains(requester))
//            {
//                _requestQueue.Enqueue(requester);
//            }
//        }

//        public void ReleaseAccess(PlantUnit requester)
//        {
//            if (_currentOwner == requester)
//            {
//                CurrentFlowRate = 0;
//                _currentOwner = null;

//                // Procesar siguiente en cola
//                ProcessNextInQueue();

//                // <--- AQUI CORREGIDO: Solo pasa a DISPONIBLE si después de revisar la cola nadie la tomó
//                if (_currentOwner == null)
//                {
//                    TransitionOutbound(new PumpAvailable(this));
//                }
//            }
//            else
//            {
//                _requestQueue = new Queue<PlantUnit>(_requestQueue.Where(x => x != requester));
//            }
//        }

//        private void ProcessNextInQueue()
//        {
//            if (_requestQueue.Count > 0)
//            {
//                var nextCandidate = _requestQueue.Dequeue();

//                if (DoesCandidateStillNeedPump(nextCandidate))
//                {
//                    _currentOwner = nextCandidate;
//                    // <--- AQUI IMPORTANTE: Sigue NO DISPONIBLE porque cambió de dueño, no se liberó
//                    TransitionOutbound(new PumpOutletInUse(this));
//                }
//                else
//                {
//                    ProcessNextInQueue(); // Recursivo hasta encontrar uno o vaciar
//                }
//            }
//        }

//        private bool DoesCandidateStillNeedPump(PlantUnit candidate)
//        {
//            if (candidate is ContinuousSystem skid)
//            {
//                return skid.InboundState is SkidConsumingRecipe ||
//                       skid.InboundState is SkidStoppedByInlet ||
//                       skid.InboundState is SkidWaitingForResources;
//            }
//            // Soporte para Mixer usando interfaz o tipo concreto
//            if (candidate is BatchMixer mixer)
//            {
//                // El mixer necesita la bomba si está llenando o esperando en estado Starved
//                // (Asumiendo que implementas IBatchStarvedByPump en los estados de espera)
//                return mixer.InboundState is IBatchStarvedByPump || mixer.InboundState is MixerFillingWithPump;
//            }
//            return false;
//        }

//        public void TransitionInBound(PumpInletState newState) => TransitionInBoundInternal(newState);
//        public void TransitionOutbound(PumpOutletState newState) => TransitionOutboundInternal(newState);
//    }

//    // ... (Tus clases de Estado PumpOutletState, PumpInletState, etc. siguen igual) ...
//    public abstract class PumpOutletState : IUnitState
//    {
//        protected Pump _pump;
//        public virtual string SubStateName => string.Empty;
//        public abstract string StateName { get; }
//        public PumpOutletState(Pump p) => _pump = p;
//        public virtual void Calculate() { }
//        public virtual void CheckTransitions() { }
//    }
//    public class PumpAvailable : PumpOutletState
//    {
//        public PumpAvailable(Pump p) : base(p) { }
//        public override string StateName => "Available";
//    }
//    public class PumpOutletNotAvailable : PumpOutletState
//    {
//        public PumpOutletNotAvailable(Pump p) : base(p) { }
//        public override string StateName => $"Not Available by Inlet";
//    }
//    public class PumpOutletInUse : PumpOutletState
//    {
//        public PumpOutletInUse(Pump p) : base(p) { }
//        public override string StateName => $"In use by {_pump.CurrentOwner?.Name ?? string.Empty} ";
//    }
//    public abstract class PumpInletState : IUnitState
//    {
//        protected Pump _pump;
//        public virtual string SubStateName => string.Empty;
//        public abstract string StateName { get; }
//        public PumpInletState(Pump p) => _pump = p;
//        public virtual void Calculate() { }
//        public virtual void CheckTransitions() { }
//    }
//    public class PumpInletReady : PumpInletState
//    {
//        public PumpInletReady(Pump p) : base(p) { }
//        public override string StateName => "Ready";
//    }
//    public class PumpInletStarved : PumpInletState
//    {
//        public PumpInletStarved(Pump p) : base(p) { }
//        public override string StateName => "Starved";
//    }
//}
