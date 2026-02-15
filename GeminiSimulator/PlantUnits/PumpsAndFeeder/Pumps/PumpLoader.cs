using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using Simulator.Shared.Simulations;

//namespace GeminiSimulator.PlantUnits.PumpsAndFeeder.Pumps
//{
//    public class PumpLoader : IPhysicalLoader
//    {
//        private readonly SimulationContext _context;

//        // Mismo nivel que los otros equipos (20)
//        public int ExecutionOrder => 20;

//        public PumpLoader(SimulationContext context)
//        {
//            _context = context;
//        }

//        public void Load(NewSimulationDTO data)
//        {
//            Console.WriteLine("--- Cargando Bombas (Pumps) ---");
//            int count = 0;

//            if (data.Pumps == null) return;

//            foreach (var dto in data.Pumps)
//            {
//                try
//                {
//                    // 1. Instanciar la Bomba (Específico)
//                    // Pasamos el Amount 'Flow' y el booleano 'IsForWashing'
//                    var pump = new Pump(
//                        dto.Id,
//                        dto.Name,
//                        dto.EquipmentType,
//                        dto.FocusFactory,
//                        dto.Flow,
//                        dto.IsForWashing
//                    );

//                    // 2. Cargar propiedades comunes (Topología, Paradas, etc.)
//                    // Usando el Extension Method que creamos
//                    pump.LoadCommonFrom(dto);

//                    // 3. Registrar
//                    _context.RegisterUnit(pump);
//                    count++;
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[ERROR] Falló carga de Bomba '{dto.Name}': {ex.Message}");
//                }
//            }

//            Console.WriteLine($" -> {count} Bombas cargadas.");
//        }
//    }
//}
