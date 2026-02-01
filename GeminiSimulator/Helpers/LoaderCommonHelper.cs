using GeminiSimulator.PlantUnits;
using QWENShared.DTOS.BaseEquipments;

namespace GeminiSimulator.Helpers
{
    public static class PlantUnitLoaderExtensions
    {
        // El 'this' hace la magia: permite llamar a unit.LoadCommonFrom(dto)
        public static void LoadCommonFrom(this PlantUnit unit, BaseEquipmentDTO dto)
        {
           

            // 2. Paradas Programadas
            if (dto.PlannedDownTimes != null)
            {
                foreach (var pdt in dto.PlannedDownTimes)
                {
                    if (pdt.StartTime.HasValue && pdt.EndTime.HasValue)
                        unit.AddPlannedDownTime(pdt.StartTime.Value, pdt.EndTime.Value);
                }
            }

          
        }
    }
}
