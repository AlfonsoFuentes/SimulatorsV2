using GeminiSimulator.Materials;
using GeminiSimulator.Plans;
using GeminiSimulator.PlantUnits;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Skids;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Operators;
using GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps;
using GeminiSimulator.PlantUnits.StreamJoiners;
using GeminiSimulator.PlantUnits.Tanks;
using GeminiSimulator.SKUs;
using GeminiSimulator.WashoutMatrixs;
using UnitSystem;

namespace GeminiSimulator.Main
{

    public class SimulationContext
    {
        // --- 1. DATOS DE DEFINICIÓN (Configuración estática) ---

        public TimeSpan TotalSimulationSpan { get; set; } = TimeSpan.FromSeconds(0);
        public Dictionary<Guid, ProductDefinition> Products { get; } = new();
        public Dictionary<Guid, SkuDefinition> Skus { get; } = new();

        // Reglas de limpieza (Matriz)
        public WashoutMatrix WashoutRules { get; private set; } = new WashoutMatrix();

        // --- 2. DATOS DE ESCENARIO (Configuración dinámica del plan) ---
        // Puede ser nulo si aún no se ha cargado la Fase 2
        public SimulationScenario? Scenario { get; set; }

        public OperatorEngagementType OperatorEngagementType { get; set; } = OperatorEngagementType.StartOnDefinedTime;
        public Amount TimeOperatorOcupy { get; set; } = new Amount(10, TimeUnits.Minute);

        // --- 3. LA ESTRATEGIA HÍBRIDA DE EQUIPOS ---

        // A. EL MAESTRO (Todos los equipos mezclados)
        // Vital para el WireUp y búsquedas agnósticas por ID
        public Dictionary<Guid, PlantUnit> AllUnits { get; } = new();

        // B. LOS ÍNDICES ESPECIALIZADOS (Subconjuntos rápidos)
        // Apuntan A LOS MISMOS OBJETOS en memoria que AllUnits.
        public Dictionary<Guid, PackagingLine> Lines { get; } = new();
        public Dictionary<Guid, BatchMixer> Mixers { get; } = new();
        public Dictionary<Guid, StorageTank> Tanks { get; } = new();
        public Dictionary<Guid, Pump> Pumps { get; } = new();
        public Dictionary<Guid, ContinuousSystem> Skids { get; } = new();     // Sistemas Continuos
        public Dictionary<Guid, Operator> Operators { get; } = new();
        public Dictionary<Guid, StreamJoiner> Joiners { get; } = new();       // Uniones de flujo
        public List<RawMaterialTank> TanksRawMaterial { get; } = new();
        public List<InHouseTank> TanksInHouse { get; } = new();
        public List<ContinuousWipTank> TanksContinuousWip { get; } = new();
        public List<BatchWipTank> TanksBatchWip { get; } = new();
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
            Joiners.Clear();
            Tanks.Clear();
            TanksRawMaterial.Clear();
            TanksInHouse.Clear();
            TanksContinuousWip.Clear();
            TanksBatchWip.Clear();
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
            foreach (var line in Lines.Values)
            {
                line.ClearPlan(); // Asegúrate de crear este método en PackagingLine
            }

            // 3. Limpiar colas de trabajo en los Mixers
            foreach (var mixer in Mixers.Values)
            {
                // mixer.ClearPlan(); // Asegúrate de crear este método en BatchMixer
            }

            // Nota: Tanques, Bombas y Operadores usualmente no guardan "Planes", 
            // sino que reaccionan al estado, así que no requieren limpieza de cola.
        }

        /// <summary>
        /// Helper para agregar equipos y clasificarlos automáticamente.
        /// </summary>
        public void RegisterUnit(PlantUnit unit)
        {
            // 1. Al Maestro (Siempre)
            if (!AllUnits.ContainsKey(unit.Id))
            {
                AllUnits.Add(unit.Id, unit);
            }

            // 2. Al Índice Específico (Pattern Matching)
            switch (unit)
            {
                case PackagingLine line:
                    if (!Lines.ContainsKey(line.Id)) Lines.Add(line.Id, line);
                    break;

                case BatchMixer mixer:
                    if (!Mixers.ContainsKey(mixer.Id)) Mixers.Add(mixer.Id, mixer);
                    break;

                case StorageTank tank:
                    if (!Tanks.ContainsKey(tank.Id)) Tanks.Add(tank.Id, tank);
                    break;

                case Pump pump:
                    if (!Pumps.ContainsKey(pump.Id)) Pumps.Add(pump.Id, pump);
                    break;

                case ContinuousSystem skid:
                    if (!Skids.ContainsKey(skid.Id)) Skids.Add(skid.Id, skid);
                    break;

                case Operator op:
                    if (!Operators.ContainsKey(op.Id)) Operators.Add(op.Id, op);
                    break;

                case StreamJoiner joiner:
                    if (!Joiners.ContainsKey(joiner.Id)) Joiners.Add(joiner.Id, joiner);
                    break;
            }
        }

        /// <summary>
        /// Helper para buscar cualquier unidad de forma segura.
        /// </summary>
        public PlantUnit? GetUnit(Guid id)
        {
            return AllUnits.TryGetValue(id, out var unit) ? unit : null;
        }
        public ProductDefinition? GetMaterial(Guid id)
        {
            return Products.TryGetValue(id, out var unit) ? unit : null;
        }
        public void RegisterUnitTanks(StorageTank specializedTank)
        {
            // 1. Reemplazamos en las listas generales (sobreescritura)
            AllUnits[specializedTank.Id] = specializedTank;
            Tanks[specializedTank.Id] = specializedTank;

            // 2. Clasificamos en la lista específica según su clase real
            // Usamos pattern matching, que en .NET 10 es súper eficiente
            switch (specializedTank)
            {
                case RawMaterialTank t:
                    TanksRawMaterial.Add(t);
                    break;
                case InHouseTank t:
                    TanksInHouse.Add(t);
                    break;
                case ContinuousWipTank t:
                    TanksContinuousWip.Add(t);
                    break;
                case BatchWipTank t:
                    TanksBatchWip.Add(t);
                    break;
            }
        }
    }

}
