using GeminiSimulator.DesignPatterns;
using UnitSystem;

namespace GeminiSimulator.PlantUnits.Tanks
{
    public abstract class InletRawMaterialTankState : IUnitState
    {
        protected RawMaterialTank _tank = null!;
        public InletRawMaterialTankState(RawMaterialTank tank)
        {
            _tank = tank;
        }
        public abstract string StateName { get; }
        public virtual string SubStateName => string.Empty;

        public virtual void Calculate()
        {
            _tank.CalculateOutleFlows();

        }

        public virtual void CheckTransitions() { }
    }
    public class NormalCapacityState : InletRawMaterialTankState
    {
        public NormalCapacityState(RawMaterialTank tank) : base(tank) { }
        public override string StateName => "Normal Capacity";
        public override void Calculate() { base.Calculate(); }
        public override void CheckTransitions()
        {

            if (_tank.CurrentLevel <= _tank.CriticalMinLevel.GetValue(MassUnits.KiloGram))
            {
                var inlet = _tank.WorkingCapacity.GetValue(MassUnits.KiloGram) - _tank.CurrentLevel;
                _tank.SetInletFlow(inlet);
            }


        }
    }
    
}
