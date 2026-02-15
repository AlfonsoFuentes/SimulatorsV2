using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using QWENShared.DTOS.SimulationPlanneds;
using Simulator.Shared.Simulations;
using System.Reflection;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    public class NewSimulationBuilder
    {
        private readonly NewSimulationContext _context;

        // AHORA TENEMOS DOS LISTAS SEPARADAS
        private readonly List<INewPhysicalLoader> _physicalLoaders = new();
        private readonly List<INewPlanLoader> _planLoaders = new();

        public NewSimulationBuilder()
        {
            _context = new NewSimulationContext();

            // Auto-descubrimiento inteligente
            RegisterLoadersAutomatically();
        }

        private void RegisterLoadersAutomatically()
        {
            Console.WriteLine("--- Iniciando Auto-Descubrimiento de Loaders (Bifásico) ---");

            var assembly = Assembly.GetExecutingAssembly();

            // Obtenemos todos los tipos concretos (no abstractos, no interfaces)
            var allTypes = assembly.GetTypes().Where(t => !t.IsInterface && !t.IsAbstract);

            foreach (var type in allTypes)
            {
                try
                {
                    // 1. DETECTAR LOADERS FÍSICOS (Materiales, Equipos)
                    if (typeof(INewPhysicalLoader).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type, _context) is INewPhysicalLoader loader)
                        {
                            _physicalLoaders.Add(loader);
                            Console.WriteLine($" -> [Físico] Registrado: {type.Name} (Orden: {loader.ExecutionOrder})");
                        }
                    }
                    // 2. DETECTAR LOADERS DE PLAN (Escenario, Órdenes)
                    else if (typeof(INewPlanLoader).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type, _context) is INewPlanLoader loader)
                        {
                            _planLoaders.Add(loader);
                            Console.WriteLine($" -> [Plan]   Registrado: {type.Name} (Orden: {loader.ExecutionOrder})");
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR CRÍTICO] No se pudo instanciar {type.Name}. Error: {ex.Message}");
                }
            }

            Console.WriteLine($"--- Total: {_physicalLoaders.Count} Físicos, {_planLoaders.Count} Planes ---");
        }

        // --- FASE 1: CONSTRUIR LA PLANTA (Equipos y Tuberías) ---
        public NewSimulationContext BuildPhysicalPlant(NewSimulationDTO physicalData)
        {

            Console.WriteLine($"=== FASE 1: CONSTRUYENDO PLANTA '{physicalData.Name}' ===");
            _context.Clear(); // Borra todo para empezar de cero

            // Ordenamos y ejecutamos solo los loaders físicos
            var orderedLoaders = _physicalLoaders.OrderBy(x => x.ExecutionOrder).ToList();

            foreach (var loader in orderedLoaders)
            {
                var loadername = loader.GetType().Name;
                Console.WriteLine($"[...] Ejecutando {loadername}...");
                loader.Load(physicalData);
            }

            // WIREUP: Solo se hace en la fase física (conectar tuberías)
            Console.WriteLine("--- Ejecutando WireUp (Conexión de Objetos) ---");
            foreach (var unit in _context.AllUnits.Values)
            {
                unit.WireUp(_context.AllUnits);
            }

            PropagateCapabilitiesToPumps();
            PropagateMixerBatchCycletime();
            Console.WriteLine("=== PLANTA CONSTRUIDA EXITOSAMENTE ===");
            return _context;
        }

        // --- FASE 2: APLICAR EL PLAN (Turnos y Órdenes) ---
        public void ApplyProductionPlan(SimulationPlannedDTO planData)
        {
            Console.WriteLine($"=== FASE 2: CARGANDO PLAN '{planData.Name}' ===");

            // Limpiamos solo datos anteriores de planes (colas, escenario), manteniendo los equipos
            _context.ClearOperationalData();

            // Ordenamos y ejecutamos solo los loaders de planificación
            var orderedLoaders = _planLoaders.OrderBy(x => x.ExecutionOrder).ToList();

            foreach (var loader in orderedLoaders)
            {
                Console.WriteLine($"[...] Ejecutando {loader.GetType().Name}...");
                loader.Load(planData);
            }

            SetPreferedMixersToLines(planData);
            Console.WriteLine("=== PLAN APLICADO EXITOSAMENTE ===");
        }

       public void PropagateMixerBatchCycletime()
        {

            foreach (var mixer in _context.Mixers)
            {
                foreach (var material in mixer.SupportedProducts)
                {
                    var batchSize = mixer.ProductCapabilities[material].GetValue(MassUnits.KiloGram);

                    var steps = material.RecipeSteps;
                    if (steps == null) continue;
                    Amount totalTime = new Amount(0, TimeUnits.Minute);
                    foreach (var step in steps.ToList())
                    {
                        if (step == null) continue;
                        if (step.IsMaterialAddition)
                        {
                            var ingredientId = step.IngredientId;
                            var pump = mixer.Inputs.OfType<NewPump>().Where(x => x.SupportedProducts.Any(m => m.Id == ingredientId)).FirstOrDefault();
                            if (pump == null)
                            {
                                step.SetDuration(new Amount(1, TimeUnits.Second));
                                totalTime += new Amount(1, TimeUnits.Second);
                            }
                            else
                            {
                                var pumpFlow = pump.NominalFlowRate.GetValue(MassFlowUnits.Kg_min); // Asumimos que está en unidades de volumen/tiempo
                                var mass = step.TargetPercentage / 100 * batchSize;
                                var durationMinutes = pumpFlow == 0 ? 0 : mass / pumpFlow;
                                step.SetDuration(new Amount(durationMinutes, TimeUnits.Minute));
                                totalTime += new Amount(durationMinutes, TimeUnits.Minute);
                                step.SetMassTarget(new Amount(mass, MassUnits.KiloGram));
                            }
                        }
                        else
                        {
                            if (step.OperationType != QWENShared.Enums.BackBoneStepType.Connect_Mixer_WIP)
                            {
                                totalTime += step.Duration;
                            }
                            else if (_context.BatchTransferCalculationModel == TransferBatchToMixerCalculation.Manual)
                            {
                                 totalTime += step.Duration;
                            }


                        }
                    
                    }
                    mixer.TheoricalBatchTime[material] = totalTime;


                }

            }
        }
        void PropagateCapabilitiesToPumps()
        {
            Console.WriteLine("--- Propagando Capacidades: Materia Prima (Comprada + InHouse) ---");

            // Unimos ambas listas de tanques que actúan como "Suministros"
            var supplyTanks = _context.TanksRawMaterial.Cast<NewProcessTank>()
                              .Concat(_context.TanksInHouse.Cast<NewProcessTank>()).ToList();

            foreach (var tank in supplyTanks)
            {
                // Obtenemos los materiales definidos en el tanque
                var materials = tank.SupportedProducts;

                // Buscamos las bombas conectadas a la salida de este tanque
                var connectedPumps = tank.Outputs.OfType<NewPump>().ToList();

                foreach (var pump in connectedPumps)
                {
                    foreach (var material in materials)
                    {
                        // Registramos la capacidad en la bomba usando tu método tipado
                        pump.SetProductCapability(material, pump.NominalFlowRate);

                        Console.WriteLine($" -> [Config] Pump '{pump.Name}' enabled for Material: {material.Name} (Source: {tank.Name})");
                    }
                }
            }
        }
        public void SetPreferedMixersToLines(SimulationPlannedDTO planData)
        {

            foreach (var plannedline in planData.PlannedLines)
            {
                var newLine = _context.Lines.FirstOrDefault(x => x.Id == plannedline.LineId);
                if (newLine != null)
                {
                    foreach (var prefredMixer in plannedline.PreferedMixerDTOs)
                    {
                        var newMixer = _context.Mixers.FirstOrDefault(x => x.Id == prefredMixer.MixerId);
                        if (newMixer != null)
                        {
                         
                            newMixer.AddPreferedLine(newLine);
                        }
                    }
                }
            }
        }

    }
}
