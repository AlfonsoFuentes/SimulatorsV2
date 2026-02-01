using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.PlantUnits.ManufacturingEquipments.Skids
{
    public class ContinuousSystemLoader : IPhysicalLoader
    {
        private readonly SimulationContext _context;
        public int ExecutionOrder => 20;

        public ContinuousSystemLoader(SimulationContext context) => _context = context;

        public void Load(NewSimulationDTO data)
        {
            Console.WriteLine("--- Cargando Sistemas Continuos (Skids) ---");
            int count = 0;

            if (data.Skids == null) return;

            foreach (var dto in data.Skids)
            {
                // 1. Instanciar (Pasamos el Flow del DTO al Constructor)
                var skid = new ContinuousSystem(
                    dto.Id,
                    dto.Name,
                    dto.EquipmentType,
                    dto.FocusFactory,
                    dto.Flow // Pasamos el Amount directamente
                );

                // 2. Mapear Comunes
                skid.LoadCommonFrom(dto);

                // 3. Registrar
                _context.RegisterUnit(skid);
                count++;
            }
            Console.WriteLine($" -> {count} Skids cargados.");
        }
    }
}
