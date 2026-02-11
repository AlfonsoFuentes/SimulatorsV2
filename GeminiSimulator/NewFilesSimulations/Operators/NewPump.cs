using GeminiSimulator.NewFilesSimulations.BaseClasss;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Operators
{
    public class NewPump : NewPlantUnit
    {
        public Amount NominalFlowRate { get; private set; }
        public bool IsForWashing { get; private set; }

        private double _currentFlow { get; set; } = 0;
        public Amount CurrentFlow => new Amount(_currentFlow, MassFlowUnits.Kg_sg);

        public NewPump(Guid id, string name, ProcessEquipmentType type, FocusFactory factory, Amount nominalFlowRate, bool _isforwashing)
            : base(id, name, type, factory)
        {
            NominalFlowRate = nominalFlowRate;
            IsForWashing = _isforwashing;
        }

        public void SetCurrentFlow(double flow)
        {
            _currentFlow = flow;
        }

        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);

            // Arranca quieta

        }



    }


}
