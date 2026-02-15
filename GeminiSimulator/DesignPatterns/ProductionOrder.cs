using GeminiSimulator.Materials;
using GeminiSimulator.SKUs;
using QWENShared.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.DesignPatterns
{

    public class ProductionOrder
    {
         
        public Guid OrderId {  get; set; }=Guid.NewGuid();
        public string SkuName => SKU?.Name ?? string.Empty;
        public int OrderSequence { get; private set; } // Orden de ejecución

        // --- DATOS DEL PLAN (Vienen del PlannedSKUDTO) ---
        public int PlannedCases { get; set; }
        private Amount LineSpeed { get; set; }    // Velocidad específica del plan
        private double UnitWeight => SKU?.UnitWeight.GetValue(MassUnits.KiloGram) ?? 0;
        private int UnitsPerCase => SKU?.UnitsPerCase ?? 0;

        // --- DATOS DE REFERENCIA (Vienen del Contexto/SKU) ---
        public ProductCategory Category => SKU?.Category ?? ProductCategory.None;
        private Amount VolumeSize => SKU?.UnitVolume ?? new Amount(0, VolumeUnits.MilliLiter);

        public Amount TimeToChangeSKU { get; private set; }

        public double AU { get; private set; }

        public ProductDefinition? Material => SKU?.Product ?? null!;
        public Guid MaterialId => Material?.Id ?? Guid.Empty;
        public SkuDefinition? SKU { get; private set; }
        public ProductionOrder(

            SkuDefinition sku,

            int sequence,
            double plannedAu,
            int plannedCases,
            Amount lineSpeed,


            Amount timeToChangeSKU)
        {

            SKU = sku;

            OrderSequence = sequence;
            PlannedCases = plannedCases;
            LineSpeed = lineSpeed;


            AU = plannedAu;
            TimeToChangeSKU = timeToChangeSKU;
        }

        // --- Propiedades Calculadas Útiles ---
        public double MassToPack => PlannedCases * UnitsPerCase * UnitWeight;

        public double FlowRatePersec => LineSpeed.GetValue(LineVelocityUnits.EA_sg) * UnitWeight;
    }
}
