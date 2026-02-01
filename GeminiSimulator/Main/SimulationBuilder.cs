using GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps;
using GeminiSimulator.PlantUnits.Tanks;
using QWENShared.DTOS.SimulationPlanneds;
using Simulator.Shared.Simulations;
using System.Reflection;

namespace GeminiSimulator.Main
{


    public class SimulationBuilder
    {
        private readonly SimulationContext _context;

        // AHORA TENEMOS DOS LISTAS SEPARADAS
        private readonly List<IPhysicalLoader> _physicalLoaders = new();
        private readonly List<IPlanLoader> _planLoaders = new();

        public SimulationBuilder()
        {
            _context = new SimulationContext();

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
                    if (typeof(IPhysicalLoader).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type, _context) is IPhysicalLoader loader)
                        {
                            _physicalLoaders.Add(loader);
                            Console.WriteLine($" -> [Físico] Registrado: {type.Name} (Orden: {loader.ExecutionOrder})");
                        }
                    }
                    // 2. DETECTAR LOADERS DE PLAN (Escenario, Órdenes)
                    else if (typeof(IPlanLoader).IsAssignableFrom(type))
                    {
                        if (Activator.CreateInstance(type, _context) is IPlanLoader loader)
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
        public SimulationContext BuildPhysicalPlant(NewSimulationDTO physicalData)
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
            SpecializedVesselReads();
            foreach (var unit in _context.AllUnits.Values)
            {
                unit.WireUp(_context.AllUnits);
                unit.SetContext(_context);  
            }
            PropagateCapabilitiesToPumps();
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

            Console.WriteLine("=== PLAN APLICADO EXITOSAMENTE ===");
        }
        void SpecializedVesselReads()
        {
            // Obtenemos los tanques que fueron cargados inicialmente como StorageTank
            var genericTanks = _context.Tanks.Values.ToList();

            // Limpiamos las listas de tipos específicos antes de rellenar 
            // (por si acaso se llama dos veces)
            _context.TanksRawMaterial.Clear();
            _context.TanksInHouse.Clear();
            _context.TanksContinuousWip.Clear();
            _context.TanksBatchWip.Clear();

            foreach (var generic in genericTanks)
            {
                // El TankSpecializer crea la instancia correcta (Tipo 1, 2, 3 o 4)
                var specialized = TankSpecializer.CreateSpecialized(generic);
              
                // Llamamos al registro especial que acabamos de crear
                _context.RegisterUnitTanks(specialized);

                Console.WriteLine($"[Especializador] '{specialized.Name}' clasificado en su lista específica.");
            }
        }
        void PropagateCapabilitiesToPumps()
        {
            Console.WriteLine("--- Propagando Capacidades: Materia Prima (Comprada + InHouse) ---");

            // Unimos ambas listas de tanques que actúan como "Suministros"
            var supplyTanks = _context.TanksRawMaterial.Cast<StorageTank>()
                              .Concat(_context.TanksInHouse.Cast<StorageTank>()).ToList();

            foreach (var tank in supplyTanks)
            {
                // Obtenemos los materiales definidos en el tanque
                var materials = tank.Materials;

                // Buscamos las bombas conectadas a la salida de este tanque
                var connectedPumps = tank.Outputs.OfType<Pump>().ToList();

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

    }
}
