using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using Simulator.Shared.Simulations;

//namespace GeminiSimulator.PlantUnits.Tanks
//{
//    public class TankLoader : IPhysicalLoader
//    {
//        private readonly SimulationContext _context;

//        // Se carga junto con el resto de equipos
//        public int ExecutionOrder => 20;

//        public TankLoader(SimulationContext context)
//        {
//            _context = context;
//        }

//        public void Load(NewSimulationDTO data)
//        {
//            Console.WriteLine("--- Cargando Tanques de Almacenamiento ---");
//            int count = 0;

//            if (data.Tanks == null) return;

//            foreach (var dto in data.Tanks)
//            {
//                try
//                {
//                    // 1. Instanciar StorageTank
//                    // Mapeamos las propiedades del DTO a los argumentos del constructor
//                    var tank = new StorageTank(
//                        dto.Id,
//                        dto.Name,
//                        dto.EquipmentType,
//                        dto.FocusFactory,

//                        // Niveles (Amounts)
//                        dto.Capacity,      // Físico
//                        dto.MaxLevel,      // Operativo Máximo
//                        dto.MinLevel,      // Operativo Mínimo
//                        dto.LoLoLevel,     // Crítico
//                        dto.InitialLevel,  // Arranque

//                        // Configuración
//                        dto.IsStorageForOneFluid,
//                        dto.MaterialType
//                    );

//                    // 2. Cargar Comunes (Conectores, Paradas, etc.)
//                    // Nota: Los tanques a veces tienen restricciones de producto específicas 
//                    // en MaterialEquipments, esto también se carga aquí.
//                    tank.LoadCommonFrom(dto);

//                    // 3. Registrar
//                    _context.RegisterUnit(tank);
//                    count++;
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[ERROR] Falló carga de Tanque '{dto.Name}': {ex.Message}");
//                }
//            }

//            Console.WriteLine($" -> {count} Tanques cargados.");
//        }
//    }
//}
