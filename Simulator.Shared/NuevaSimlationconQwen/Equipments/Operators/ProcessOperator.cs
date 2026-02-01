using Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Pumps;
using Simulator.Shared.NuevaSimlationconQwen.ManufacturingOrders;
using Simulator.Shared.NuevaSimlationconQwen.Reports;

namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Operators
{
    public class ProcessOperator : ManufactureFeeder, ILiveReportable
    {
        public List<OperatorWorkTask> Agenda { get; } = new();
        // Propiedades específicas
        public List<ProcessMixer> OutletMixers => OutletEquipments.OfType<ProcessMixer>().ToList();

        // 👇 Define si es para lavado o no
        public override bool IsForWashout { get; set; } = false;

        public override void ValidateOutletInitialState(DateTime currentdate)
        {
            OutletState = new FeederAvailableState(this);
        }
        public Amount GetTotalWorkloadTime()
        {
            Amount accumulatedWait = new Amount(0, TimeUnits.Minute);

            // 1. Trabajo físico actual (en cualquier mixer)
            if (this.OcuppiedBy is ProcessMixer activeMixer && activeMixer.CurrentManufactureOrder != null)
            {
                var currentJob = activeMixer.CurrentManufactureOrder.PendingBatchTime;
                accumulatedWait += currentJob + GetOperatorDownTimeDelay(accumulatedWait, currentJob);
            }

            // 2. Agenda de reservas (Mixer A, B, C...)
            foreach (var task in Agenda)
            {
                accumulatedWait += task.EstimatedDuration + GetOperatorDownTimeDelay(accumulatedWait, task.EstimatedDuration);
            }
            return accumulatedWait;
        }

        public Amount GetOperatorDownTimeDelay(Amount waitToStart, Amount workDuration)
        {
          

            double extraMinutes = 0;
            DateTime now = CurrentDate; // Usar el reloj de la simulación

            // Proyectamos cuándo empezaría el operario y cuándo terminaría
            DateTime startProjected = now.AddMinutes(waitToStart.GetValue(TimeUnits.Minute));
            DateTime endProjected = startProjected.AddMinutes(workDuration.GetValue(TimeUnits.Minute));

            TimeSpan projectStart = startProjected.TimeOfDay;
            TimeSpan projectEnd = endProjected.TimeOfDay;

            foreach (var breakTime in PlannedDownTimes)
            {
                // 1. Si la parada empieza durante el trabajo
                if (breakTime.Start >= projectStart && breakTime.Start < projectEnd)
                {
                    extraMinutes += (breakTime.End - breakTime.Start).TotalMinutes;
                    // Actualizamos el final proyectado porque el almuerzo "empuja" el trabajo
                    projectEnd = projectEnd.Add(breakTime.End - breakTime.Start);
                }
                // 2. Si ya estamos en la parada cuando el mixer queda libre
                else if (projectStart >= breakTime.Start && projectStart < breakTime.End)
                {
                    extraMinutes += (breakTime.End - projectStart).TotalMinutes;
                    projectEnd = projectEnd.Add(breakTime.End - projectStart);
                }
            }

            return new Amount(extraMinutes, TimeUnits.Minute);
        }
       
    }
    public record OperatorWorkTask(
    Guid MixerId,
    Guid OrderId,
    Amount EstimatedDuration // (Lavado + Batch)
);
}
