using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Operators
{
    public class NewOperator : NewPlantUnit
    {
        public Amount OperatorTimeDisabled { get; private set; } = new Amount(0, TimeUnits.Second);
        public OperatorEngagementType EngagementType { get; private set; } = OperatorEngagementType.Infinite;
        public void SetOperationOperatorTime(OperatorEngagementType type, Amount _TimeOperatorOcupy)
        {
            EngagementType = type;
            OperatorTimeDisabled = _TimeOperatorOcupy;
        }
        public NewOperator(Guid id, string name, ProcessEquipmentType type, FocusFactory factory) : base(id, name, type, factory)
        {
           
        }
        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
        }

        public override DateTime AvailableAt
        {
            get
            {
                // CASO 1: Si es Infinito, está libre "ahora"
                if (EngagementType == OperatorEngagementType.Infinite)
                    return CurrentDate;

                // CASO 2: Si tiene un dueño (un Mixer) y es FullBatch
                if (EngagementType == OperatorEngagementType.FullBatch && CurrentOwner is NewMixer mixer)
                {
                    // EL VÍNCULO DINÁMICO:
                    // Su disponibilidad es exactamente el fin proyectado del Mixer
                    // (El PlannedEnd de la última actividad en la cinta del Mixer)
                    return mixer.BatchManager.CurrentBatchProjectedEndDate;
                }

                // CASO 3: Si es StartOnDefinedTime, el valor ya se fijó en el TimedTaskState
                // O si no tiene dueño, está libre "ahora"
                return base.AvailableAt > CurrentDate ? base.AvailableAt : CurrentDate;
            }
        }
    }
   
}
