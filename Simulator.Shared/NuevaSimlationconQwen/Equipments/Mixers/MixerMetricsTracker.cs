namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers
{  // En Simulator.Shared.NuevaSimlationconQwen.Metrics
    public enum MixerTimeType
    {
        Batching,
        Washing,

        StarvedByFeeder,
        StarvedByOperator,
        StarvedByWashoutPump,
        PlannedDowntime,
        NoBatchAssigned,
        TransferToWIP,
        StarvedByTransferToWIP,
        ConnectingToWIP,
      
        Other
    }
    
}
