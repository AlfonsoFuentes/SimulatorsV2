

using QWENShared.DTOS.Base;
using QWENShared.DTOS.Conectors;
using QWENShared.DTOS.EquipmentPlannedDownTimes;
using QWENShared.DTOS.MaterialEquipments;
using QWENShared.DTOS.Materials;
using QWENShared.Enums;

namespace QWENShared.DTOS.BaseEquipments
{

    
   
    public class BaseEquipmentDTO : Dto
    {
        public string Name { get; set; } = string.Empty;
        public override string ToString()
        {
            return Name;
        }
        public BaseEquipmentDTO() { }
        public Guid MainProcessId { get; set; }
        public bool CopyData { get; set; } = true;

        public virtual ProccesEquipmentType EquipmentType { get; set; } = ProccesEquipmentType.None;
        public FocusFactory FocusFactory { get; set; } = FocusFactory.None;

        public List<MaterialDTO> Materials => MaterialEquipments == null || MaterialEquipments.Count == 0 ? new() : MaterialEquipments.Select(x => x.Material!).ToList();
        public List<EquipmentPlannedDownTimeDTO> PlannedDownTimes { get; set; } = new();

        public List<InletConnectorDTO> InletConnectors { get; set; } = new();
        public List<OutletConnectorDTO> OutletConnectors { get; set; } = new();

        public List<MaterialEquipmentDTO> MaterialEquipments { get; set; } = new();
    }
    //public class BaseEquipmentList : IResponseAll
    //{
    //    public List<BaseEquipmentDTO?> Items { get; set; } = new();
    //}
    //public class BaseEquipmentGetAll : IGetAll
    //{
    //    public string EndPointName => StaticClass.BaseEquipments.EndPoint.GetAll;
    //    public Guid MainProcessId { set; get; }
    //    public ProccesEquipmentType ProccesEquipmentType { set; get; }= ProccesEquipmentType.None;
    //}
    
}
