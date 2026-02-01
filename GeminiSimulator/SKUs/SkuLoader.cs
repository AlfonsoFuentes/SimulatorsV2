using GeminiSimulator.Main;
using QWENShared.DTOS.SKUs;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.SKUs
{
    public class SkuLoader : IPhysicalLoader
    {
        private readonly SimulationContext _context;
        public int ExecutionOrder => 10;
        public SkuLoader(SimulationContext context)
        {
            _context = context;
        }
        public void Load(NewSimulationDTO data)
        {
            Console.WriteLine($"[Loader] Cargando SKUs...");
            Load(data.SKUs);
        }
        void Load(List<SKUDTO> skuDtos)
        {
            Console.WriteLine("--- Iniciando Carga de SKUs (Modo Mapper) ---");
            int count = 0;
            int skipped = 0;

            foreach (var dto in skuDtos)
            {
                try
                {
                    // 1. Validar dependencia (BackBone/Material)
                    if (dto.BackBone == null)
                    {
                        // Si no tiene material definido, no sirve para la simulación
                        skipped++;
                        continue;
                    }

                    // 2. Buscar el Material REAL en el Contexto
                    if (!_context.Products.TryGetValue(dto.BackBone.Id, out var productDef))
                    {
                        Console.WriteLine($"[AVISO] SKU {dto.SkuCode} ignorado. Requiere material ID {dto.BackBone.Id} no cargado.");
                        skipped++;
                        continue;
                    }

                    // 3. Crear el Objeto de Dominio (Mapeo explícito)
                    var sku = new SkuDefinition(
                        dto.Id,
                        dto.SkuCode,
                        dto.Name,
                        productDef,         // Inyectamos el objeto ProductDefinition
                        dto.Weigth,         // Amount (Weight)
                        dto.Size,           // Amount (Volume)
                        dto.EA_Case,        // int
                        dto.ProductCategory,// Enum
                        dto.PackageType     // Enum
                    );

                    // 4. Registrar
                    if (!_context.Skus.ContainsKey(sku.Id))
                    {
                        _context.Skus.Add(sku.Id, sku);
                        count++;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falló carga de SKU {dto.SkuCode}: {ex.Message}");
                    skipped++;
                }
            }

            Console.WriteLine($"[SkuLoader] Carga finalizada: {count} SKUs listos. Ignorados/Errores: {skipped}");
        }
    }
}
