

using QWENShared.DTOS.BaseEquipments;
using QWENShared.DTOS.SKULines;
using QWENShared.DTOS.SKUs;
using QWENShared.Enums;
using System.Text.Json.Serialization;

namespace QWENShared.DTOS.Lines
{
    public class LineDTO : BaseEquipmentDTO
    {
       

        public List<SKULineDTO> LineSKUs { get; set; } = new();
        public List<SKUDTO> SKUs => LineSKUs == null || LineSKUs.Count == 0 ? new() : LineSKUs.Select(x => x.SKU!).ToList();
        double _TimeToReviewAUValue;
        string _TimeToReviewAUUnitName = TimeUnits.Minute.Name;
        public double TimeToReviewAUValue
        {
            get => _TimeToReviewAUValue;
            set
            {
                _TimeToReviewAUValue = value;
                if (TimeToReviewAU != null)
                    TimeToReviewAU=new Amount(_TimeToReviewAUValue, _TimeToReviewAUUnitName);
            }
        }
        public string TimeToReviewAUUnitName
        {
            get => _TimeToReviewAUUnitName;
            set
            {
                _TimeToReviewAUUnitName = value;
                if (TimeToReviewAU != null)
                    TimeToReviewAU=new Amount(_TimeToReviewAUValue, _TimeToReviewAUUnitName);
            }
        }
        public void ChangeTimeToReviewAU()
        {
            _TimeToReviewAUValue = TimeToReviewAU.GetValue(TimeToReviewAU.Unit);
            _TimeToReviewAUUnitName = TimeToReviewAU.UnitName;
        }
        [JsonIgnore]
        public Amount TimeToReviewAU { get; set; } = new(TimeUnits.Minute);

        public PackageType PackageType { get; set; } = PackageType.None;

    }
   
}
