using GeminiSimulator.DesignPatterns;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using QWENShared.Enums;

namespace GeminiSimulator.PlantUnits.PumpsAndFeeder.Operators
{
    public class Operator : PlantUnit
    {
        public Operator(Guid id, string name, ProccesEquipmentType type, FocusFactory factory)
            : base(id, name, type, factory)
        {
        }

        public override void CheckInitialStatus(DateTime InitialDate)
        {
            _currentOwner = null!;
            TransitionInBound(new OperatorAvailable(this));
        }

        private PlantUnit? _currentOwner;
        public PlantUnit? CurrentOwner=> _currentOwner;
        private Queue<PlantUnit> _requestQueue = new Queue<PlantUnit>();
        public bool IsOwnedBy(PlantUnit unit) => _currentOwner == unit;
        public void RequestAccess(PlantUnit requester)
        {
            if (_currentOwner == requester) return;

            if (_currentOwner == null && _requestQueue.Count == 0)
            {
                _currentOwner = requester;
                TransitionInBound(new OperatorNotAvailable(this));
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
                    TransitionInBound(new OperatorNotAvailable(this));
                    return;
                }
                else
                {
                    ProcessNextInQueue();
                }
            }
            TransitionInBound(new OperatorAvailable(this));
        }
        private bool DoesCandidateStillNeedOperator(PlantUnit candidate)
        {
            if (candidate is BatchMixer mixer)
            {
                return mixer.InboundState is MixerStarvedByAtInitOperator;
            }
            // Aquí agregarás el caso del Mixer: "|| candidate is Mixer ..."
            return false;
        }
     
        public void TransitionInBound(OperatorState newState) => TransitionInBoundInternal(newState);
    }
    public abstract class OperatorState : IUnitState
    {
        public abstract string StateName { get; }
        public virtual string SubStateName => string.Empty;
        protected Operator _operator { get; }
        protected OperatorState(Operator _operator)
        {
            this._operator = _operator;
        }
        public virtual void Calculate()
        {

        }

        public virtual void CheckTransitions()
        {

        }
    }
    public class OperatorAvailable : OperatorState
    {
        public OperatorAvailable(Operator _operator) : base(_operator)
        {
        }

        public override string StateName => $"{_operator.Name} Available";
    }
    public class OperatorNotAvailable : OperatorState
    {
        public OperatorNotAvailable(Operator _operator) : base(_operator)
        {
        }

        public override string StateName => $"{_operator.Name} Occupied by {_operator.CurrentOwner?.Name}";
    }
}
