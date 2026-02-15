using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Context.CheckAvailability
{
    public class PlanValidationReport
    {
        public List<LineReport> Lines { get; set; } = new();
        public bool GlobalPermit => Lines.All(l => l.IsLineViable);
    }

    public class LineReport
    {
        public string LineName { get; set; } = string.Empty;
        public bool IsLineViable { get; set; }
        public List<OrderReport> Orders { get; set; } = new();
       
    }

    public class OrderReport
    {
        public string SkuName { get; set; } = string.Empty;
        public double PlannedCases { get; set; }
        public string? MaterialName { get; set; } // Nullable para el primer check
        public List<ManufactureReport> PotentialSystems { get; set; } = new();
    }

    public class ManufactureReport
    {
        public string Name { get; set; } = string.Empty;
        public bool IsViable { get; set; }
        public Amount TheoricalBCT { get; set; } = new Amount(0, TimeUnits.Minute);
        public List<IngredientCheck> Ingredients { get; set; } = new();
        public string SummaryMessage { get; set; } = string.Empty;
    }

    public class IngredientCheck
    {
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsConnected { get; set; }
        public bool IsManual { get; set; }
        public Amount? Mass { get; set; }
        public Amount Time { get; set; } = new Amount(0, TimeUnits.Minute);
        public string SourcePath { get; set; } = string.Empty;
        public List<PumpPath> AvailablePaths { get; set; } = new();
    }

    public class PumpPath
    {
        public string PumpName { get; set; } = string.Empty;
        public double NominalFlow { get; set; }
        public List<string> ConnectedTanks { get; set; } = new();
    }
}
