using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using QWENShared.DTOS.Materials;
using Simulator.Shared.Simulations;

//namespace GeminiSimulator.Materials
//{
//    public class MaterialLoader : IPhysicalLoader
//    {
//        private readonly SimulationContext _context;
//        public int ExecutionOrder => 0;
//        public MaterialLoader(SimulationContext context)
//        {
//            _context = context;
//        }
//        public void Load(NewSimulationDTO data)
//        {
//            // Lógica de carga igual que antes, tomando data.Materials
//            // ...
//            Console.WriteLine($"[Loader] Cargando Materiales...");
//            Load(data.Materials); // (Tu método privado o lógica directa)
//        }
//        void Load(List<MaterialDTO> dtos)
//        {
//            Console.WriteLine("--- Iniciando Carga de Materiales (Modo Mapper) ---");
//            int successCount = 0;
//            int errorCount = 0;

//            foreach (var dto in dtos)
//            {
//                try
//                {
//                    if (_context.Products.ContainsKey(dto.Id)) continue; // Evitar duplicados

//                    // 1. Crear Entidad Básica
//                    var product = new ProductDefinition(
//                        dto.Id,
//                        dto.M_Number,
//                        dto.CommonName,
//                        dto.MaterialType,
//                        dto.ProductCategory,
//                        dto.IsForWashing
//                    );

//                    // 2. Mapear Receta (si existe)
//                    if (dto.BackBoneSteps != null && dto.BackBoneSteps.Any())
//                    {
//                        var stepsList = new List<RecipeStep>();

//                        // Importante: Ordenar aquí usando los datos del DTO
//                        foreach (var stepDto in dto.BackBoneSteps.OrderBy(x => x.Order))
//                        {
//                            // Extraemos ID con seguridad (como estaba en tu lógica original)
//                            // Si stepDto.RawMaterialId viene vacío, pasamos null
//                            Guid? ingId = (stepDto.RawMaterialId == Guid.Empty) ? null : stepDto.RawMaterialId;
//                            string ingreditename = (stepDto.StepRawMaterial == null) ? string.Empty : stepDto.StepRawMaterial.CommonName;
//                            // Creamos el paso limpio
//                            var step = new RecipeStep(
//                                stepDto.Order,
//                                stepDto.BackBoneStepType,
//                                ingId,
//                                stepDto.Percentage,
//                                stepDto.Time,
//                                ingreditename// Amount
//                            );

//                            stepsList.Add(step);
//                        }

//                        // Asignamos la lista convertida al producto
//                        product.SetRecipe(stepsList);
//                    }

//                    // 3. Registrar
//                    _context.Products.Add(product.Id, product);
//                    successCount++;
//                }
//                catch (Exception ex)
//                {
//                    Console.WriteLine($"[ERROR] Material {dto.CommonName}: {ex.Message}");
//                    errorCount++;
//                }
//            }

//            Console.WriteLine($"[MaterialLoader] Cargados: {successCount}. Errores: {errorCount}");

//            // Validamos integridad al final
//            ValidateRecipeIntegrity();
//        }

//        private void ValidateRecipeIntegrity()
//        {
//            foreach (var product in _context.Products.Values)
//            {
//                if (product.IsManufactured && product.Recipe != null)
//                {
//                    foreach (var step in product.Recipe.Steps)
//                    {
//                        if (step.IsMaterialAddition && step.IngredientId.HasValue)
//                        {
//                            if (!_context.Products.ContainsKey(step.IngredientId.Value))
//                            {
//                                Console.WriteLine($"[INTEGRIDAD] '{product.Name}' requiere ingrediente {step.IngredientId} (no encontrado).");
//                            }
//                        }
//                    }
//                }
//            }
//        }
//    }
//}
