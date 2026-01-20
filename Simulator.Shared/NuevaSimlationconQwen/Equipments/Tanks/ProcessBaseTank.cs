using Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Operators;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Pumps;
using Simulator.Shared.NuevaSimlationconQwen.ManufacturingOrders;
using Simulator.Shared.NuevaSimlationconQwen.Materials;
using Simulator.Shared.NuevaSimlationconQwen.Reports;

namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Tanks
{
    public interface IProcessTank : IEquipment
    {
        Amount Capacity { get; set; }
        Amount HiLevel { get; set; }
        Amount LoLevel { get; set; }
        Amount LoLolevel { get; set; }
        Amount InitialLevel { get; set; }
        Amount CurrentLevel { get; set; }
        List<ProcessPump> OutletPumps { get; }
        Amount OutletFlows { get; }
        string MaterialName { get; }
        IMaterial LastMaterial { get; set; }
        Amount AverageFlowRate { get; }
        bool IsTankHigherThenHiLevel();
        bool IsTankInLoLoLevel();
        void CalculateOutletLevel();

    }
    public interface IRequestTansferTank : IProcessTank
    {
        public void ReceiveTransferRequestFromMixer(TransferFromMixertoWIPOrder order);
    }
    public abstract class ProcessBaseTank : Equipment, IProcessTank, ILiveReportable
    {
        public Amount Capacity { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount HiLevel { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount LoLevel { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount LoLolevel { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount InitialLevel { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount CurrentLevel { get; set; } = new Amount(0, MassUnits.KiloGram);

        public List<ProcessPump> OutletPumps => OutletEquipments.OfType<ProcessPump>().ToList();

        public Amount OutletFlows => new Amount(OutletPumps.Sum(x => x.ActualFlow.GetValue(MassFlowUnits.Kg_sg)), MassFlowUnits.Kg_sg);
        public string MaterialName => Material?.CommonName ?? "No Material";
        public Amount MassDeliveredBySecond { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount AcumulatedMassDelivered { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount RunTime { get; set; } = new Amount(0, TimeUnits.Second);
        public Amount AverageFlowRate =>
            RunTime.GetValue(TimeUnits.Second) > 0 ?
            new Amount(AcumulatedMassDelivered.GetValue(MassUnits.KiloGram) / RunTime.GetValue(TimeUnits.Minute), MassFlowUnits.Kg_min) :
            new Amount(0, MassFlowUnits.Kg_sg);
        public IMaterial LastMaterial { get; set; } = null!;

        public virtual bool IsTankHigherThenHiLevel()
        {
            if (Name.Contains("Agua"))
            {
            }
            if (CurrentLevel > HiLevel)
            {
                return true;
            }
            return false;
        }

        public bool IsTankInLoLoLevel()
        {
            if (Name.Contains("Agua"))
            {

            }
            if (CurrentLevel < LoLolevel)
            {
                return true;
            }

            return false;
        }
        public bool IsTankInLoLevel()
        {
            if (Name.Contains("Agua"))
            {

            }
            if (CurrentLevel < LoLevel)
            {
                return true;
            }

            return false;
        }
        public bool IsTankAvailable()
        {
            if (Name.Contains("Agua"))
            {

            }
            if (CurrentLevel > LoLolevel)
            {
                return true;
            }

            return false;
        }

        public virtual void CalculateOutletLevel()
        {
         
            MassDeliveredBySecond = OutletFlows * OneSecond;
            AcumulatedMassDelivered += MassDeliveredBySecond;
            CalculateRunTime();
            CurrentLevel -= MassDeliveredBySecond;

        }
        public virtual void CalculateRunTime()
        {
            RunTime += OneSecond;
        }
        protected Amount GetOperatorDownTimeDelay(ManufaturingEquipment mixer, Amount waitToStart, Amount workDuration)
        {
            var op = mixer.InletEquipments.OfType<ProcessOperator>().FirstOrDefault();
            if (op == null) return new Amount(0, TimeUnits.Minute);

            double extraMinutes = 0;
            DateTime now = CurrentDate; // Usar el reloj de la simulación

            // Proyectamos cuándo empezaría el operario y cuándo terminaría
            DateTime startProjected = now.AddMinutes(waitToStart.GetValue(TimeUnits.Minute));
            DateTime endProjected = startProjected.AddMinutes(workDuration.GetValue(TimeUnits.Minute));

            TimeSpan projectStart = startProjected.TimeOfDay;
            TimeSpan projectEnd = endProjected.TimeOfDay;

            foreach (var breakTime in op.PlannedDownTimes)
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


}
