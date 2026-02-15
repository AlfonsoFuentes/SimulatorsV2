

using QWENShared.DTOS.Lines;
using QWENShared.DTOS.PlannedSKUs;
using QWENShared.DTOS.PreferedMixers;
using QWENShared.Enums;
using System.Text.Json.Serialization;

namespace QWENShared.DTOS.LinePlanneds
{
    public class LinePlannedDTO :Dto
    {
        public string Name => LineDTO == null ? string.Empty : LineDTO.Name;
       

        public Guid SimulationPlannedId { get; set; }
        public Guid MainProcesId { get; set; }
        public PackageType PackageType => LineDTO == null ? PackageType.None : LineDTO.PackageType;
        public LineDTO LineDTO { get; set; } = null!;
        public Guid LineId => LineDTO == null ? Guid.Empty : LineDTO.Id;
        public string LineName => LineDTO == null ? string.Empty : LineDTO.Name;
        public List<PlannedSKUDTO> PlannedSKUDTOs { get; set; } = new();
        public List<PreferedMixerDTO> PreferedMixerDTOs { get; set; } = new();
        public ShiftType ShiftType { get; set; }

        

    }
   
}
