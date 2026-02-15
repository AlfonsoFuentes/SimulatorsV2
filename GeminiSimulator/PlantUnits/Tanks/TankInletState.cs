using GeminiSimulator.DesignPatterns;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using UnitSystem;

//namespace GeminiSimulator.PlantUnits.Tanks
//{
//    public abstract class TankInletState : IUnitState
//    {
//        protected ProcessTank _tank = null!;
//        public abstract string StateName { get; }
//        public virtual string SubStateName => string.Empty;
//        public TankInletState(ProcessTank tank)
//        {
//            _tank = tank;
//        }



//        public virtual void Calculate() { }

//        public abstract void CheckTransitions();

//    }
//    public class TankAvailable : TankInletState
//    {
//        public TankAvailable(ProcessTank tank) : base(tank) { }

//        public override string StateName => $"Available";

//        public override void Calculate()
//        {
          
//        }
//        public override void CheckTransitions()
//        {

//        }
//    }
//    public class TankReceiving : TankInletState
//    {
//        public TankReceiving(ProcessTank tank) : base(tank) { }

//        public override string StateName => $"Receiving {_tank.CurrentOwner?.Name ?? string.Empty}";

//        public override void Calculate()
//        {

//        }
//        public override void CheckTransitions()
//        {
//            if (_tank.CurrentLevel >= _tank.WorkingCapacity.GetValue(MassUnits.KiloGram))
//            {
//                _tank.TransitionInBound(new TankInletStarvedHighLevel(_tank));
//            }
//        }
//    }

//    public class TankInletStarvedHighLevel : TankInletState
//    {
//        public TankInletStarvedHighLevel(ProcessTank tank) : base(tank) { }

//        public override string StateName => $"Receiving from SKID";

//        public override void Calculate()
//        {

//        }
//        public override void CheckTransitions()
//        {
//            if (_tank.CurrentOwner is BatchMixer _mixer)
//            {

//                var available = _tank.WorkingCapacity.GetValue(MassUnits.KiloGram) - _tank.CurrentLevel;
//                if (available >= _mixer.CurrentMass)
//                {
//                    _tank.TransitionInBound(new TankReceiving(_tank));
//                }
//            }

//        }
//    }

//}
