using System;
using System.Collections.Generic;
using System.Text;

namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers
{
    /// <summary>
    /// Estado cuando el Mixer tiene una orden pero el operario está con el vecino.
    /// </summary>
    public class MixerInletWaitingForOperatorState : MixerInletState, IMixerStarved
    {
        public MixerInletWaitingForOperatorState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = "Waiting for Operator Availability";

            // TryCaptureOperator meterá al mixer en la cola FIFO del operario si está ocupado
            AddTransition<MixerInletReviewForWashingAtInitBatchState>(mixer => mixer.TryCaptureOperator());
        }
        public override void Report(DateTime currentdate)
        {
            // Se registra como tiempo de espera de operario al inicio del lote
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.StarvedByOperator);
        }
    }

    /// <summary>
    /// Estado cuando el operario capturado entra en su parada programada (Ej: Almuerzo).
    /// </summary>
    public class MixerInletStarvedByOperatorBreakState : MixerInletState, IMixerStarved
    {
        public MixerInletStarvedByOperatorBreakState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = "Starved: Operator on Scheduled Break";

            // Regresa a la selección de pasos solo cuando el operario termina su parada programada
            AddTransition<MixerInletSelectNextStepState>(mixer => !mixer.IsCapturedOperatorOnBreak());
        }
        public override void Report(DateTime currentdate)
        {
            // Se registra como tiempo muerto por operario durante el proceso
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.StarvedByOperator);
        }
    }
}
