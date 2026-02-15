using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using QWENShared.DTOS.Lines;
using Simulator.Shared.Simulations;
using UnitSystem;

//namespace GeminiSimulator.PlantUnits.Lines
//{
//    public class PackagingLineLoader : IPhysicalLoader
//    {
//        private readonly SimulationContext _context;
//        public int ExecutionOrder => 20;
//        public PackagingLineLoader(SimulationContext context)
//        {
//            _context = context;
//        }
//        public void Load(NewSimulationDTO data)
//        {
//            Console.WriteLine($"[Loader] Cargando Líneas...");
//            // Usa data.Lines
//            Load(data.Lines);
//        }
//        void Load(List<LineDTO> lineDtos)
//        {
//            Console.WriteLine("--- Iniciando Carga de Líneas (Modo Mapper) ---");
//            int linesLoaded = 0;
//            int profilesLoaded = 0;

//            foreach (var dto in lineDtos)
//            {
//                try
//                {
//                    // ---------------------------------------------------------
//                    // PASO 1: EXTRAER DATOS PRIMITIVOS DEL DTO
//                    // ---------------------------------------------------------
//                    // Convertimos la unidad de tiempo 'Amount' a 'TimeSpan' nativo de .NET
//                    double minutes = dto.TimeToReviewAU.GetValue(TimeUnits.Minute);
//                    TimeSpan auInterval = TimeSpan.FromMinutes(minutes > 0 ? minutes : 60);

//                    // ---------------------------------------------------------
//                    // PASO 2: INSTANCIAR LA ENTIDAD DE DOMINIO (LIMPIA)
//                    // ---------------------------------------------------------
//                    var line = new PackagingLine(
//                        dto.Id,
//                        dto.Name,
//                        dto.EquipmentType,
//                        dto.FocusFactory,
//                        auInterval
//                    );
                  
//                    // ---------------------------------------------------------
//                    // PASO 3: MAPEAR PROPIEDADES BASE (PlantUnit)
//                    // ---------------------------------------------------------

//                    line.LoadCommonFrom(dto);

//                    // C. Capacidades (Si la línea tuviera restricciones de material genéricas)
//                    // En este caso las líneas suelen tener capacidad 0 de almacenamiento,
//                    // pero si hubiera datos en MaterialEquipments, se mapearían aquí con line.SetProductCapability(...)



//                    // ---------------------------------------------------------
//                    // PASO 5: REGISTRAR EN EL CONTEXTO
//                    // ---------------------------------------------------------
//                    _context.RegisterUnit(line);
//                    linesLoaded++;
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[ERROR] Falló carga de línea '{dto.Name}': {ex.Message}");
//                }
//            }

//            Console.WriteLine($"[Loader] Carga finalizada: {linesLoaded} líneas, {profilesLoaded} perfiles.");
//        }
//    }
//}
