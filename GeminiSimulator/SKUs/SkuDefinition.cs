using GeminiSimulator.Materials;
using QWENShared.DTOS.SKUs;
using QWENShared.Enums;
using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.SKUs
{
    public class SkuDefinition
    {
        public Guid Id { get; private set; }
        public string Code { get; private set; } // SkuCode
        public string Name { get; private set; }

        // RELACIÓN DE DOMINIO: El líquido que lleva adentro
        public ProductDefinition Product { get; private set; }

        // Propiedades Físicas
        public Amount UnitWeight { get; private set; } // Peso neto por unidad
        public Amount UnitVolume { get; private set; } // Volumen (Size)
        public int UnitsPerCase { get; private set; }  // Unidades por caja

        // Clasificación
        public ProductCategory Category { get; private set; }
        public PackageType PackageType { get; private set; }

        // CONSTRUCTOR LIMPIO (Sin DTOs)
        public SkuDefinition(
            Guid id,
            string code,
            string name,
            ProductDefinition product,
            Amount unitWeight,
            Amount unitVolume,
            int unitsPerCase,
            ProductCategory category,
            PackageType packageType)
        {
            Id = id;
            Code = code;
            Name = name;
            Product = product ?? throw new ArgumentNullException(nameof(product));
            UnitWeight = unitWeight;
            UnitVolume = unitVolume;
            UnitsPerCase = unitsPerCase;
            Category = category;
            PackageType = packageType;
        }

        // --- LÓGICA DE NEGOCIO ---

        /// <summary>
        /// Calcula la masa total (Kg) de una caja.
        /// </summary>
        public double GetMassPerCaseKg()
        {
            double weightKg = UnitWeight.GetValue(MassUnits.KiloGram);
            return weightKg * UnitsPerCase;
        }
    }
}
