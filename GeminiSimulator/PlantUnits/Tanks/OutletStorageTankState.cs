using GeminiSimulator.DesignPatterns;
using UnitSystem;

namespace GeminiSimulator.PlantUnits.Tanks
{
    public abstract class OutletStorageTankState : IUnitState
    {
        protected ProcessTank _tank = null!;
        public OutletStorageTankState(ProcessTank tank)
        {
            _tank = tank;
        }
        public abstract string StateName { get; }
        public virtual string SubStateName => string.Empty;
        public virtual void Calculate()
        {

            _tank.TotalSeconds++;
            if (_tank is InHouseTank inHouse)
            {
                inHouse.CalculateOutleFlows();
            }
        }

        public virtual void CheckTransitions() { }
    }
    public class TankAvailableState : OutletStorageTankState
    {
        public TankAvailableState(ProcessTank tank) : base(tank) { }
        public override string StateName => "Available";
        public override void Calculate() { base.Calculate(); }
        public override void CheckTransitions()
        {
            //if (_tank.IsOnPlannedBreak(_tank.CurrentDate))
            //{
            //    _tank.TransitionOutbound(new TankPlannedDowntimeState(_tank));
            //    return;
            //}

            if (_tank.CurrentLevel < _tank.CriticalMinLevel.GetValue(MassUnits.KiloGram))
            {
                _tank.TransitionOutbound(new TankLoLevelState(_tank));
                return;
            }

        }
    }
    public class TankLoLevelState : OutletStorageTankState
    {
        public TankLoLevelState(ProcessTank tank) : base(tank) { }
        public override string StateName => "Lo Level";
        public override void Calculate() { base.Calculate(); }
        public override void CheckTransitions()
        {

            if (_tank.CurrentLevel > _tank.CriticalMinLevel.GetValue(MassUnits.KiloGram))
            {
                //if (_tank.IsOnPlannedBreak(_tank.CurrentDate))
                //{
                //    _tank.TransitionOutbound(new TankPlannedDowntimeState(_tank));
                //    return;
                //}
                _tank.TransitionOutbound(new TankAvailableState(_tank));
            }

        }
    }
    //public class TankPlannedDowntimeState : OutletStorageTankState
    //{
    //    public TankPlannedDowntimeState(ProcessTank tank) : base(tank) { }
    //    public override string StateName => "Planned downtime";
    //    public override void Calculate() { base.Calculate(); }
    //    public override void CheckTransitions()
    //    {

    //        if (!_tank.IsOnPlannedBreak(_tank.CurrentDate))
    //        {
    //            if (_tank.CurrentLevel < _tank.CriticalMinLevel.GetValue(MassUnits.KiloGram))
    //            {
    //                _tank.TransitionOutbound(new TankLoLevelState(_tank));
    //                return;
    //            }
    //            _tank.TransitionOutbound(new TankAvailableState(_tank));


    //        }
    //    }
    //}
}
