using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using Simulator.Shared.Simulations;

//namespace GeminiSimulator.PlantUnits.StreamJoiners
//{
//    public class StreamJoinerLoader : IPhysicalLoader
//    {
//        private readonly SimulationContext _context;

//        // Se carga junto con el resto de equipos
//        public int ExecutionOrder => 20;

//        public StreamJoinerLoader(SimulationContext context)
//        {
//            _context = context;
//        }

//        public void Load(NewSimulationDTO data)
//        {
//            Console.WriteLine("--- Cargando Uniones de Flujo (StreamJoiners) ---");
//            int count = 0;

//            if (data.StreamJoiners == null) return;

//            foreach (var dto in data.StreamJoiners)
//            {
//                try
//                {
//                    // 1. Instanciar
//                    var joiner = new StreamJoiner(
//                        dto.Id,
//                        dto.Name,
//                        dto.EquipmentType,
//                        dto.FocusFactory
//                    );

//                    // 2. Cargar Comunes (Conectores - ¡Esto es lo más importante para un Joiner!)
//                    // Aquí es donde se define quién entra y quién sale.
//                    joiner.LoadCommonFrom(dto);

//                    // 3. Registrar
//                    _context.RegisterUnit(joiner);
//                    count++;
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[ERROR] Falló carga de Joiner '{dto.Name}': {ex.Message}");
//                }
//            }

//            Console.WriteLine($" -> {count} Joiners cargados.");
//        }
//    }
//}
