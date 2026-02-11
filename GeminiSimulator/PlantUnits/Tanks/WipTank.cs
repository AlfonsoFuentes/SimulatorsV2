using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Materials;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.PlantUnits.Lines.States;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Skids;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Operators;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps;
using System.Collections;
using UnitSystem;

namespace GeminiSimulator.PlantUnits.Tanks
{
    public class WipTank : ProcessTank
    {

        public PackagingLine? CurrentLine { get; set; }
        protected double MassPendingToSendToLine { get; set; }
        public double MassPendingToProduce { get; set; }





        public void CountersToZero()
        {
            TotalMassProduced = 0;
            TotalSeconds = 0;

        }

        public WipTank(StorageTank baseTank) : base(baseTank)
        {

        }

        public bool IsPendingToProduce => MassPendingToProduce > 0;

        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
            MassPendingToProduce = MassPendingToProduce - CurrentLevel > 0 ? MassPendingToProduce - CurrentLevel : 0;
            if (CurrentLevel <= CriticalMinLevel.GetValue(MassUnits.KiloGram))
            {
                TransitionOutbound(new TankLoLevelState(this));

            }
            else
            {
                TransitionOutbound(new TankAvailableState(this));
            }


        }
        public override void InitialNotify()
        {
            CurrentLine?.Update();
        }
        public override void Notify()
        {
            CurrentLine?.Update();
        }
        public virtual void ReceiveRequirementFromLine(PackagingLine line)
        {
            if (line == null) return;
            if (line?.CurrentOrder == null) return;
            CurrentLine = line;
            TotalMassProduced = 0;

            MassPendingToSendToLine = CurrentLine.CurrentOrder?.MassToPack ?? 0;
            MassPendingToProduce = CurrentLine.CurrentOrder?.MassToPack + CriticalMinLevel.GetValue(MassUnits.KiloGram) ?? 0;
            // Buscamos el SKU en el diccionario del contexto
            if (CurrentLine.CurrentOrder?.Material != null)
            {
                SetCurrentMaterial(line?.CurrentOrder?.Material ?? null!);


            }
        }


        double currentMasSentToLine = 0;
        public override void Update()
        {
            CurrentLine?.Update();

        }
        public override void SetOutletFlow(double flow)
        {
            base.SetOutletFlow(flow);
            currentMasSentToLine += flow;
            MassPendingToSendToLine -= flow;
            if (Name.Contains("9"))
            {

            }
            if (CurrentLine?.InboundState is LineInletAvailable)

            {
                TotalSeconds++;
            }



        }
        public override void SetInletFlow(double flow)
        {
            base.SetInletFlow(flow);
            MassPendingToProduce -= flow;

        }

        public void DetachLine(PackagingLine line)
        {
            if (CurrentLine == line)
            {

                CurrentLine = null;
                SetCurrentMaterial(null!);


            }



        }


        // Solo acepta estados de salida para la línea


        public override Dictionary<string, ReportField> GetReportData()
        {
            var data = base.GetReportData(); // Trae el "Name"

            // 1. Caso: Línea no programada

            if (CurrentMaterial != null)
                data.Add("Product", new ReportField($"{CurrentMaterial.Name}"));
            if (CurrentLine != null)
            {
                data.Add($"Line", new ReportField(CurrentLine.Name, "", true));
                data.Add($"Pending mass to send to {CurrentLine.Name}", new ReportField($"{MassPendingToSendToLine:F1} Kg"));
            }
            data.Add($"Pending mass to receive", new ReportField($"{MassPendingToProduce:F1} Kg"));
            data.Add("Mass sent to line", new ReportField($"{currentMasSentToLine:F0}, Kg"));
            //data.Add("Simulation time", new ReportField($"{TotalSeconds / 60:F2}, min"));
            data.Add("Mass in Process", new ReportField($"{MassInProcess.GetValue(MassUnits.KiloGram):F0}, Kg"));
            data.Add("Time to empty vessel", new ReportField($"{PendingTimeToCurrentLevel.GetValue(TimeUnits.Minute):F2}, min"));
            data.Add("Average Outle flow", new ReportField($"{AverageOutleFlow.GetValue(MassFlowUnits.Kg_min):F2}, Kg/min"));
            return data;
        }
    }
    public class ContinuousWipTank : WipTank
    {
        public override Dictionary<string, ReportField> GetReportData()
        {
            var data = base.GetReportData(); // Trae el "Name"






            return data;
        }
        public ContinuousWipTank(StorageTank baseTank) : base(baseTank) { }

        public ContinuousSystem? CurrentSKID;
        public override void ReceiveRequirementFromLine(PackagingLine line)
        {
            base.ReceiveRequirementFromLine(line);
            if (CurrentMaterial == null) return;
            var skids = Inputs.OfType<ContinuousSystem>().Where(x => x.Materials.Any(x => x.Id == CurrentMaterial.Id)).ToList();

            if (skids.Any())
            {
                CurrentSKID = skids[0];
                CurrentSKID?.ReceiveRequirementFromWIP(this);

            }


        }
        public override void Update()
        {
            //Para verificar estados de entrada y salida

        }
        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
            if (MassPendingToProduce > 0)
            {

                if (CurrentLevel <= MinWorkingLevel.GetValue(MassUnits.KiloGram))
                {
                    TransitionInBound(new ReceiveingFromSkid(this));
                    return;
                }
                TransitionInBound(new WaitingLevelToStart(this));
                return;
            }

            TransitionInBound(new ProductNotNeeded(this));
        }
        public override void Notify()
        {
            base.Notify();
            CurrentSKID?.Update();
        }


        public void DetachSKid()
        {
            if (CurrentSKID != null)
            {
                CurrentSKID.DetachWip(this);
                CurrentSKID = null;

            }
        }
        public void TransitionInBound(SkidWipTankInletState newState)
      => TransitionInBoundInternal(newState);
    }
    public class BatchWipTank : WipTank
    {


        public BatchWipTank(StorageTank baseTank) : base(baseTank) { }


        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
            TransitionInBound(new TankAvailable(this));
            if (CurrentLevel < CriticalMinLevel.GetValue(MassUnits.KiloGram))
            {
                TransitionOutbound(new TankLoLevelState(this));
                return;
            }
            TransitionOutbound(new TankAvailableState(this));
        }
        public override void Notify()
        {
            base.Notify();
        }

    }

}
