using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Context.CheckAvailability;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.SKUs;
using GeminiSimulator.WashoutMatrixs;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    public enum TransferBatchToMixerCalculation
    {
        Automatic,
        Manual
    }
    public class NewSimulationContext
    {


        public NewSimulationBuilder Builder { get; set; } = null!;

        public NewSimulationEngine Engine { get; set; } = null!;
        public TimeSpan TotalSimulationSpan { get; set; } = TimeSpan.FromSeconds(0);
        public Dictionary<Guid, ProductDefinition> Products { get; } = new();
        public Dictionary<Guid, SkuDefinition> Skus { get; } = new();

        // Reglas de limpieza (Matriz)
        public WashoutMatrix WashoutRules { get; private set; } = new WashoutMatrix();

        // --- 2. DATOS DE ESCENARIO (Configuración dinámica del plan) ---
        // Puede ser nulo si aún no se ha cargado la Fase 2
        public NewSimulationScenario? Scenario { get; set; }
        public TransferBatchToMixerCalculation BatchTransferCalculationModel { get; private set; } = TransferBatchToMixerCalculation.Manual;

        public void SetBatchTrasnferCalculationModel(TransferBatchToMixerCalculation _BatchTrasnferCalculationModel)
        {
            BatchTransferCalculationModel = _BatchTrasnferCalculationModel;
            foreach (var mixer in Mixers)
            {
                mixer.SetTransferBatchToMixerCalculation(BatchTransferCalculationModel);
            }
            Builder.PropagateMixerBatchCycletime();
            Engine.SetCalculationModel(_BatchTrasnferCalculationModel);

        }
        public OperatorEngagementType OperatorEngagementType { get; private set; } = OperatorEngagementType.Infinite;
        public Amount TimeOperatorOcupy { get; private set; } = new Amount(10, TimeUnits.Minute);



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
        public PlanValidationReport GetDetailedViabilityReport()
        {
            var report = new PlanValidationReport();

            foreach (var line in Lines)
            {
                // --- VALIDACIÓN INICIAL: LÍNEA VACÍA ---
                if (!line.ProductionQueue.Any()) continue;

                // Si hay órdenes, la línea entra al reporte. 
                // Se asume viable (READY) hasta que una validación física falle.
                var lineRep = new LineReport
                {
                    LineName = line.Name,
                    IsLineViable = true
                };

                foreach (var order in line.ProductionQueue)
                {
                    var oRep = new OrderReport { SkuName = order.SkuName, PlannedCases = order.PlannedCases };

                    // --- NIVEL 0: VALIDACIÓN DE MATERIAL ---
                    if (order.Material == null)
                    {
                        lineRep.IsLineViable = false;
                        oRep.MaterialName = "[UNDEFINED]";
                        oRep.PotentialSystems.Add(new ManufactureReport { Name = "DATA ERROR", IsViable = false, SummaryMessage = "❌ FATAL: Material not defined for this SKU." });
                        lineRep.Orders.Add(oRep);
                        continue;
                    }
                    oRep.MaterialName = order.Material.Name;

                    // --- NIVEL 1: LINE -> INLET PUMPS ---
                    var inletPumps = line.Inputs.OfType<NewPump>().ToList();
                    if (!inletPumps.Any())
                    {
                        lineRep.IsLineViable = false;
                        oRep.PotentialSystems.Add(new ManufactureReport { Name = "LINE ERROR", IsViable = false, SummaryMessage = $"❌ Error: No Inlet Pumps connected to {line.Name}." });
                        lineRep.Orders.Add(oRep);
                        continue;
                    }

                    // --- NIVEL 2: PUMPS -> WIP TANKS ---
                    var wipTanks = inletPumps.SelectMany(p => p.Inputs.OfType<NewWipTank>()).Distinct().ToList();
                    if (!wipTanks.Any())
                    {
                        lineRep.IsLineViable = false;
                        oRep.PotentialSystems.Add(new ManufactureReport { Name = "PUMP ERROR", IsViable = false, SummaryMessage = "❌ Error: Inlet Pumps are not feeding any WIP Tank." });
                        lineRep.Orders.Add(oRep);
                        continue;
                    }

                    // --- NIVEL 3: WIP -> MIXERS (RUTA DUAL: DIRECTO O BOMBEADO) ---
                    var directMixers = wipTanks.SelectMany(w => w.Inputs.OfType<NewManufacture>());
                    var pumpedMixers = wipTanks.SelectMany(w => w.Inputs.OfType<NewPump>()).SelectMany(p => p.Inputs.OfType<NewManufacture>());

                    var candidateMixers = directMixers.Concat(pumpedMixers)
                        .Where(m => m.SupportedProducts.Any(p => p.Id == order.Material.Id))
                        .Distinct().ToList();

                    if (!candidateMixers.Any())
                    {
                        lineRep.IsLineViable = false;
                        oRep.PotentialSystems.Add(new ManufactureReport { Name = "WIP ERROR", IsViable = false, SummaryMessage = $"❌ Error: No path to a compatible Mixer for {order.Material.Name}." });
                        lineRep.Orders.Add(oRep);
                        continue;
                    }

                    // --- NIVEL 4: AUDITORÍA INTERNA DEL MIXER ---
                    foreach (var mixer in candidateMixers)
                    {
                        var mRep = new ManufactureReport { Name = mixer.Name, IsViable = true };

                        // VERIFICACIÓN DE DICCIONARIO: ¿Existe BatchSize para este material?
                        if (!mixer.ProductCapabilities.ContainsKey(order.Material))
                        {
                            mRep.IsViable = false;
                            mRep.SummaryMessage = $"❌ Data Error: {mixer.Name} lacks a Batch Size definition for {order.Material.Name}.";
                            lineRep.IsLineViable = false;
                            oRep.PotentialSystems.Add(mRep);
                            continue;
                        }

                        double batchSize = mixer.ProductCapabilities[order.Material].GetValue(MassUnits.KiloGram);
                        double accumulatedMinutes = 0;

                        foreach (var step in order.Material.RecipeSteps.OrderBy(x => x.Order).ToList())
                        {
                            var ingCheck = new IngredientCheck();



                            if (step.IsMaterialAddition)
                            {
                                // CORRECCIÓN: Etiquetado de adición
                                ingCheck.Name = $"Add {step.IngredientName}";

                                // A. Búsqueda Automática
                                var relevantPumps = mixer.Inputs.OfType<NewPump>().Where(p =>
                                    p.SupportedProducts.Any(sp => sp.Id == step.IngredientId)).ToList();

                                foreach (var pump in relevantPumps)
                                {
                                    var path = new PumpPath { PumpName = pump.Name, NominalFlow = pump.NominalFlowRate.GetValue(MassFlowUnits.Kg_min) };
                                    path.ConnectedTanks = pump.Inputs.OfType<NewProcessTank>()
                                        .Where(t => t.SupportedProducts.Any(sp => sp.Id == step.IngredientId))
                                        .Select(t => t.Name).ToList();

                                    if (path.ConnectedTanks.Any()) ingCheck.AvailablePaths.Add(path);
                                }

                                if (ingCheck.AvailablePaths.Any())
                                {
                                    ingCheck.IsConnected = true;
                                    var primary = ingCheck.AvailablePaths.First();
                                    double targetMass = (step.TargetPercentage / 100.0) * batchSize;
                                    double min = targetMass / primary.NominalFlow;

                                    ingCheck.Mass = new Amount(targetMass, MassUnits.KiloGram);
                                    ingCheck.Time = new Amount(min, TimeUnits.Minute);
                                    ingCheck.Status = "✅ Automatic";
                                    accumulatedMinutes += min;
                                }
                                // B. Búsqueda Manual (1 Kg/s)
                                else if (mixer.Inputs.OfType<NewOperator>().Any())
                                {
                                    ingCheck.IsConnected = true;
                                    ingCheck.IsManual = true;
                                    double targetMass = (step.TargetPercentage / 100.0) * batchSize;
                                    double manualMin = targetMass / 60.0;

                                    ingCheck.Mass = new Amount(targetMass, MassUnits.KiloGram);
                                    ingCheck.Time = new Amount(manualMin, TimeUnits.Minute);
                                    ingCheck.Status = "👤 Manual (1 Kg/s)";
                                    accumulatedMinutes += manualMin;
                                }
                                else
                                {
                                    ingCheck.IsConnected = false;
                                    mRep.IsViable = false;
                                    ingCheck.Status = "❌ NO SOURCE";
                                }
                            }
                            else
                            {
                                // CORRECCIÓN: Etiquetado de pasos de proceso (Agitación, Análisis, etc.)
                                ingCheck.Name = $"{step.OperationType}";
                                ingCheck.IsConnected = true;
                                ingCheck.Time = step.Duration;
                                ingCheck.Status = "⚙️ Process Step";
                                accumulatedMinutes += step.Duration.GetValue(TimeUnits.Minute);
                            }
                            mRep.Ingredients.Add(ingCheck);
                        }

                        mRep.TheoricalBCT = new Amount(accumulatedMinutes, TimeUnits.Minute);
                        if (!mRep.IsViable) lineRep.IsLineViable = false;
                        mRep.SummaryMessage = mRep.IsViable ? $"Verified. BCT: {accumulatedMinutes:F1} min." : "Blocked: Missing Raw Material or Operator.";
                        oRep.PotentialSystems.Add(mRep);
                    }
                    lineRep.Orders.Add(oRep);
                }
                report.Lines.Add(lineRep);
            }
            return report;
        }
    }


}

