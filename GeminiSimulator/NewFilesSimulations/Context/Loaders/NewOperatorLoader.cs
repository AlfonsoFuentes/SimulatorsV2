using GeminiSimulator.Helpers;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.Operators;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.WashoutMatrixs
{
    public class NewOperatorLoader : INewPhysicalLoader
    {
        private readonly NewSimulationContext _context;

        // Mismo nivel que el resto de equipos
        public int ExecutionOrder => 20;

        public NewOperatorLoader(NewSimulationContext context)
        {
            _context = context;
        }

        public void Load(NewSimulationDTO data)
        {
            Console.WriteLine("--- Cargando Operadores ---");
            int count = 0;

            if (data.Operators == null) return;

            foreach (var dto in data.Operators)
            {
                try
                {
                    // 1. Instanciar (Solo datos base)
                    var op = new NewOperator(
                        dto.Id,
                        dto.Name,
                        dto.EquipmentType,
                        dto.FocusFactory 

                    );

                    // 2. Cargar Comunes (Aquí se cargan las velocidades de operación manual por material)
                    op.LoadCommonFrom(dto);

                    // 3. Registrar
                    _context.RegisterUnit(op);
                    count++;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ERROR] Falló carga de Operador '{dto.Name}': {ex.Message}");
                }
            }

            Console.WriteLine($" -> {count} Operadores cargados.");
        }
    }
}
