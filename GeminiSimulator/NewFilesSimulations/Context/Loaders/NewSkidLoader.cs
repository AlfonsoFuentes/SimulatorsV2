using GeminiSimulator.Helpers;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.WashoutMatrixs
{
    public class NewSkidLoader : INewPhysicalLoader
    {
        private readonly NewSimulationContext _context;
        public int ExecutionOrder => 20;

        public NewSkidLoader(NewSimulationContext context) => _context = context;

        public void Load(NewSimulationDTO data)
        {
            Console.WriteLine("--- Cargando Sistemas Continuos (Skids) ---");
            int count = 0;

            if (data.Skids == null) return;

            foreach (var dto in data.Skids)
            {
                // 1. Instanciar (Pasamos el Flow del DTO al Constructor)
                var skid = new NewSkid(
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
