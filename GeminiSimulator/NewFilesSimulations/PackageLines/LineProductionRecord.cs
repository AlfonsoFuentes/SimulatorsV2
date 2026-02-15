using System;
using System.Collections.Generic;
using System.Text;

namespace GeminiSimulator.NewFilesSimulations.PackageLines
{
    public class ProductionOrderReport
    {
        public Guid OrderId { get; set; }
        public string SkuName { get; set; } = string.Empty;
        public string MaterialName { get; set; } = string.Empty;
        public string WipName { get; set; } = "N/A";
        public string OrderGoalSummary { get; set; } = string.Empty; // Ej: "2500 CASES OF FABULOSO"

        public double TargetMassKg { get; set; }
        public double ProducedMassKg { get; set; }

        // Acumuladores de Tiempo (Segundos)
        public double ProducingSeconds { get; set; }
        public double InletStarvationSeconds { get; set; }
        public double InternalLossSeconds { get; set; }
        public double ChangeOverSeconds { get; set; }
        public double OutOfShiftSeconds { get; set; }
        public double PlannedDownTimeSeconds { get; set; }
        public double BlockedByResourceSeconds { get; set; }
        public double WipLevelKg {  get; set; }

        // KPIs (Calculados para evitar errores en UI)
        public double Progress => TargetMassKg <= 0.1 ? 0 : (ProducedMassKg / TargetMassKg) * 100;

   

        // --- NUEVO CAMPO ESPECÍFICO ---
        public double WashingPumpWaitSeconds { get; set; }

        // Ajusta el WallClockTime para incluirlo
        public double WallClockTime => ProducingSeconds + InletStarvationSeconds + InternalLossSeconds +
                                       ChangeOverSeconds + OutOfShiftSeconds +
                                       PlannedDownTimeSeconds + BlockedByResourceSeconds +
                                       WashingPumpWaitSeconds;

        public double Real_AU => (WallClockTime - PlannedDownTimeSeconds - OutOfShiftSeconds) <= 0 ? 0 :
                                 (ProducingSeconds / (WallClockTime - PlannedDownTimeSeconds - OutOfShiftSeconds)) * 100;
    }
}
