using QWENShared.Enums;

namespace GeminiSimulator.PlantUnits.ManufacturingEquipments
{
    public abstract class EquipmentManufacture : PlantUnit
    {
        protected EquipmentManufacture(Guid id, string name, ProccesEquipmentType type, FocusFactory focusFactory) : base(id, name, type, focusFactory)
        {
        }
    }
}
