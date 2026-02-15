using GeminiSimulator.DesignPatterns;
using UnitSystem;

//namespace GeminiSimulator.PlantUnits.Tanks
//{
//    public abstract class SkidWipTankInletState : IUnitState
//    {
//        protected ContinuousWipTank _tank = null!;
//        public abstract string StateName { get; }
//        public virtual string SubStateName => string.Empty;
//        public SkidWipTankInletState(ContinuousWipTank tank)
//        {
//            _tank = tank;
//        }



//        public virtual void Calculate() { /* Por defecto no hace nada */ }

//        public virtual void CheckTransitions() { }

//    }
   
//    public class ReceiveingFromSkid : SkidWipTankInletState
//    {
//        public ReceiveingFromSkid(ContinuousWipTank tank) : base(tank) { }

//        public override string StateName => $"Receiving from SKID";

//        public override void Calculate()
//        {
           
//        }
//        public override void CheckTransitions()
//        {
//            if (_tank.CurrentLevel >= _tank.WorkingCapacity.GetValue(MassUnits.KiloGram))
//            {
//                _tank.TransitionInBound(new WaitingLevelToStart(_tank));
//            }
//            if (!_tank.IsPendingToProduce)
//            {
//                _tank.DetachSKid();
//                _tank.TransitionInBound(new ProductNotNeeded(_tank));
//            }
//        }
//    }
//    public class WaitingLevelToStart : SkidWipTankInletState
//    {
//        public WaitingLevelToStart(ContinuousWipTank tank) : base(tank) { }

//        public override string StateName => $"Waiting Start SKID";

//        public override void CheckTransitions()
//        {
//            if (_tank.CurrentLevel <= _tank.MinWorkingLevel.GetValue(MassUnits.KiloGram))
//            {
//                _tank.TransitionInBound(new ReceiveingFromSkid(_tank));
//            }
//        }
//    }
//    public class ProductNotNeeded : SkidWipTankInletState
//    {
//        public ProductNotNeeded(ContinuousWipTank tank) : base(tank) { }

//        public override string StateName => $"Not need Product from SKID";


//    }
//}
