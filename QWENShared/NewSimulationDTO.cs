using QWENShared.DTOS.BaseEquipments;
using QWENShared.DTOS.Conectors;
using QWENShared.DTOS.ContinuousSystems;
using QWENShared.DTOS.Lines;
using QWENShared.DTOS.MaterialEquipments;
using QWENShared.DTOS.Mixers;
using QWENShared.DTOS.Operators;
using QWENShared.DTOS.Pumps;
using QWENShared.DTOS.SKULines;
using QWENShared.DTOS.SKUs;
using QWENShared.DTOS.StreamJoiners;
using QWENShared.DTOS.Tanks;
using QWENShared.DTOS.Washouts;

namespace Simulator.Shared.Simulations
{
    public class NewSimulationDTO : Dto
    {
        public string Name { get; set; } = string.Empty;
        public FocusFactory FocusFactory { get; set; } = FocusFactory.None;
        public List<MaterialDTO> Materials { get; set; } = new();
        public List<SKUDTO> SKUs { get; set; } = new();
        public List<WashoutDTO> WashouTimes { get; set; } = new();
        public List<LineDTO> Lines { get; set; } = new();
        public List<SKULineDTO> SKULines { get; set; } = new();
        public List<TankDTO> Tanks { get; set; } = new();
        public List<MixerDTO> Mixers { get; set; } = new();
        public List<PumpDTO> Pumps { get; set; } = new();
        public List<ContinuousSystemDTO> Skids { get; set; } = new();
        public List<OperatorDTO> Operators { get; set; } = new();
        public List<MaterialEquipmentRecord> MaterialEquipments { get; set; } = new();
        public List<ConnectorRecord> Connectors { get; set; } = new();
        public List<BaseEquipmentDTO> AllEquipments => [.. Lines, .. Tanks, .. Mixers, .. Pumps, .. Skids, .. Operators, .. StreamJoiners];
        public List<StreamJoinerDTO> StreamJoiners { get; set; } = new();

    }
}
