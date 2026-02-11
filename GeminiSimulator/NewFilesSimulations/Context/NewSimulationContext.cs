using GeminiSimulator.Main;
using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.Plans;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Skids;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Operators;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps;
using GeminiSimulator.PlantUnits.StreamJoiners;
using GeminiSimulator.SKUs;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.DTOS.Materials;
using Simulator.Shared.Simulations;
using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    public class NewSimulationContext
    {
        public TimeSpan TotalSimulationSpan { get; set; } = TimeSpan.FromSeconds(0);
        public Dictionary<Guid, ProductDefinition> Products { get; } = new();
        public Dictionary<Guid, SkuDefinition> Skus { get; } = new();

        // Reglas de limpieza (Matriz)
        public WashoutMatrix WashoutRules { get; private set; } = new WashoutMatrix();

        // --- 2. DATOS DE ESCENARIO (Configuración dinámica del plan) ---
        // Puede ser nulo si aún no se ha cargado la Fase 2
        public NewSimulationScenario? Scenario { get; set; }

        public OperatorEngagementType OperatorEngagementType { get; private set; } = OperatorEngagementType.StartOnDefinedTime;
        public Amount TimeOperatorOcupy { get; private set; } = new Amount(10, TimeUnits.Minute);
        public void SetOperationOperatorTime(OperatorEngagementType type, Amount _TimeOperatorOcupy)
        {
            OperatorEngagementType = type;
            TimeOperatorOcupy = _TimeOperatorOcupy;
            foreach (var mixer in Mixers.ToList())
            {
                mixer.SetOperationOperatorTime(type, _TimeOperatorOcupy);
            }
            foreach (var newoperator in Operators.ToList())
            {
                newoperator.SetOperationOperatorTime(type, _TimeOperatorOcupy);
            }
        }


        // --- 3. LA ESTRATEGIA HÍBRIDA DE EQUIPOS ---

        // A. EL MAESTRO (Todos los equipos mezclados)
        // Vital para el WireUp y búsquedas agnósticas por ID
        public Dictionary<Guid, NewPlantUnit> AllUnits { get; } = new();

        // B. LOS ÍNDICES ESPECIALIZADOS (Subconjuntos rápidos)
        // Apuntan A LOS MISMOS OBJETOS en memoria que AllUnits.
        public List<NewLine> Lines => AllUnits.Values.OfType<NewLine>().ToList();
        public List<NewMixer> Mixers => AllUnits.Values.OfType<NewMixer>().ToList();
        public List<NewProcessTank> Tanks => AllUnits.Values.OfType<NewProcessTank>().ToList();
        public List<NewPump> Pumps => AllUnits.Values.OfType<NewPump>().ToList();
        public List<NewSkid> Skids => AllUnits.Values.OfType<NewSkid>().ToList();
        public List<NewOperator> Operators => AllUnits.Values.OfType<NewOperator>().ToList();
        //public Dictionary<Guid, StreamJoiner> Joiners { get; } = new();       // Uniones de flujo
        public List<NewRawMaterialTank> TanksRawMaterial => AllUnits.Values.OfType<NewRawMaterialTank>().ToList();
        public List<NewRawMaterialInhouseTank> TanksInHouse => AllUnits.Values.OfType<NewRawMaterialInhouseTank>().ToList();
        public List<NewWipTank> WipTanks => AllUnits.Values.OfType<NewWipTank>().ToList();
        public List<NewManufacture> Manufactures => AllUnits.Values.OfType<NewManufacture>().ToList();
        public List<NewRecipedInletTank> RecipeTanks => AllUnits.Values.OfType<NewRecipedInletTank>().ToList();

        /// <summary>
        /// Borra ABSOLUTAMENTE TODO (Fase 1 y Fase 2).
        /// Se usa al cargar una planta nueva desde cero.
        /// </summary>
        public void Clear()
        {
            Products.Clear();
            Skus.Clear();
            WashoutRules = new WashoutMatrix(); // Reiniciar reglas
            Scenario = null;

            // Limpiamos Maestro
            AllUnits.Clear();

            // Limpiamos Índices
            Lines.Clear();
            Mixers.Clear();
            Tanks.Clear();
            Pumps.Clear();
            Skids.Clear();
            Operators.Clear();
            //Joiners.Clear();
            Tanks.Clear();
            TanksRawMaterial.Clear();
            TanksInHouse.Clear();
            WipTanks.Clear();
            RecipeTanks.Clear();

        }

        /// <summary>
        /// Borra SOLO los datos del Plan de Producción (Fase 2).
        /// Mantiene los equipos físicos intactos.
        /// </summary>
        public void ClearOperationalData()
        {
            // 1. Reiniciar Escenario (Turnos, Fechas)
            Scenario = null;

            // 2. Limpiar colas de trabajo en las Líneas
            foreach (var line in Lines)
            {
                line.ClearPlan(); // Asegúrate de crear este método en PackagingLine
            }

            // 3. Limpiar colas de trabajo en los Mixers
            foreach (var mixer in Mixers)
            {
                // mixer.ClearPlan(); // Asegúrate de crear este método en BatchMixer
            }

            // Nota: Tanques, Bombas y Operadores usualmente no guardan "Planes", 
            // sino que reaccionan al estado, así que no requieren limpieza de cola.
        }

        /// <summary>
        /// Helper para agregar equipos y clasificarlos automáticamente.
        /// </summary>
        public void RegisterUnit(NewPlantUnit unit)
        {
            // 1. Al Maestro (Siempre)
            if (!AllUnits.ContainsKey(unit.Id))
            {
                AllUnits.Add(unit.Id, unit);
            }

            // 2. Al Índice Específico (Pattern Matching)
           
        }

        /// <summary>
        /// Helper para buscar cualquier unidad de forma segura.
        /// </summary>
        public NewPlantUnit? GetUnit(Guid id)
        {
            return AllUnits.TryGetValue(id, out var unit) ? unit : null;
        }
        public ProductDefinition? GetMaterial(Guid id)
        {
            return Products.TryGetValue(id, out var unit) ? unit : null;
        }

    }


}

