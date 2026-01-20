using Simulator.Shared.Enums.HCEnums.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static Simulator.Shared.StaticClasses.StaticClass;

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
