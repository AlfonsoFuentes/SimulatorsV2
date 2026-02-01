using QWENShared.DTOS.LinePlanneds;
using QWENShared.DTOS.MixerPlanneds;
using QWENShared.DTOS.PlannedSKUs;
using QWENShared.DTOS.SimulationPlanneds;
using Simulator.Server.Databases.Entities.HC;
using Simulator.Server.EndPoints.HCs.EquipmentPlannedDownTimes;
using Simulator.Server.EndPoints.HCs.MixerPlanneds;
using Simulator.Server.EndPoints.HCs.PlannedSKUs;

namespace Simulator.Server.EndPoints.HCs.SimulationPlanneds.GetProcessAndData
{
    public static class ReadSimulationPlannedss
    {
        public static async Task ReadPlannedLines(this CompletedSimulationPlannedDTO planned, IServerCrudService service)
        {
            LinePlannedDTO dto = new() { SimulationPlannedId = planned.Id };

            var rows = await service.GetAllAsync<LinePlanned>(dto, parentId: $"{dto.SimulationPlannedId}");

          

            if (rows != null && rows.Count > 0)
            {
                planned.PlannedLines = rows.Select(x => x.MapToDto<LinePlannedDTO>()).ToList();
                if (planned.PlannedLines != null)
                {
                    foreach (var row in planned.PlannedLines)
                    {
                        await row.ReadPlannedSKU(service);
                    }
                }

            }


        }
        public static async Task ReadPlannedMixers(this CompletedSimulationPlannedDTO planned, IServerCrudService service)
        {
            MixerPlannedDTO dto = new MixerPlannedDTO()
            {
                SimulationPlannedId = planned.Id
            };
            var rows = await service.GetAllAsync<MixerPlanned>(dto, parentId: $"{dto.SimulationPlannedId}");
           
          

            if (rows != null && rows.Count > 0)
            {
                planned.PlannedMixers = rows.Select(x => x.MapToDto<MixerPlannedDTO>()).ToList();
            }





        }
        public static async Task ReadPlannedSKU(this LinePlannedDTO plannedLine, IServerCrudService service)
        {
            PlannedSKUDTO dto = new PlannedSKUDTO()
            {
                LinePlannedId = plannedLine.Id
            };
            var rows = await service.GetAllAsync<PlannedSKU>(dto, parentId: $"{dto.LinePlannedId}");
           

            if (rows != null && rows.Count > 0)
            {
                plannedLine.PlannedSKUDTOs = rows.OrderBy(x => x.Order).Select(x => x.MapToDto<PlannedSKUDTO>()).ToList();
            }







        }

    }
}
