using GeminiSimulator.Helpers;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.Tanks;
using QWENShared.Enums;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.WashoutMatrixs
{
    public class NewTankLoader : INewPhysicalLoader
    {
        private readonly NewSimulationContext _context;

        // Se carga junto con el resto de equipos
        public int ExecutionOrder => 20;

        public NewTankLoader(NewSimulationContext context)
        {
            _context = context;
        }

        public void Load(NewSimulationDTO data)
        {
            Console.WriteLine("--- Cargando Tanques de Almacenamiento ---");
            int count = 0;

            if (data.Tanks == null) return;

            foreach (var dto in data.Tanks)
            {
                try
                {
                    NewProcessTank tank = null!;
                    switch (dto.FluidStorage)
                    {
                        case FluidToStorage.ProductBackBone:
                        {
                                tank = new NewWipTank(dto.Id, dto.Name, dto.EquipmentType, dto.FocusFactory, dto.Capacity,
                                    dto.Capacity, dto.MaxLevel, dto.MinLevel, dto.LoLoLevel, dto.InitialLevel);
                            }
                            break;
                        case FluidToStorage.RawMaterialBackBone:
                            {
                                tank = new NewRawMaterialInhouseTank(dto.Id, dto.Name, dto.EquipmentType, dto.FocusFactory, dto.Capacity,
                                    dto.Capacity, dto.MaxLevel, dto.MinLevel, dto.LoLoLevel, dto.InitialLevel);
                            }
                            break;
                        case FluidToStorage.RawMaterial:
                            {
                                tank = new NewRawMaterialTank(dto.Id, dto.Name, dto.EquipmentType, dto.FocusFactory, dto.Capacity,
                                    dto.Capacity, dto.MaxLevel, dto.MinLevel, dto.LoLoLevel, dto.InitialLevel);
                            }
                            ; break;
                    }
                    // 1. Instanciar StorageTank
                    // Mapeamos las propiedades del DTO a los argumentos del constructor
                    if (tank != null)
                    {
                        tank.LoadCommonFrom(dto);

                        // 3. Registrar
                        _context.RegisterUnit(tank);
                        count++;
                    }

                    // 2. Cargar Comunes (Conectores, Paradas, etc.)
                    // Nota: Los tanques a veces tienen restricciones de producto específicas 
                    // en MaterialEquipments, esto también se carga aquí.
                    
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falló carga de Tanque '{dto.Name}': {ex.Message}");
                }
            }

            Console.WriteLine($" -> {count} Tanques cargados.");
        }
    }
}
