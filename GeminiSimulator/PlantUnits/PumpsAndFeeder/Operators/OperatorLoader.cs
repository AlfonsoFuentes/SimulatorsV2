using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.PlantUnits.PumpsAndFeeder.Operators
{
    public class OperatorLoader : IPhysicalLoader
    {
        private readonly SimulationContext _context;

        // Mismo nivel que el resto de equipos
        public int ExecutionOrder => 20;

        public OperatorLoader(SimulationContext context)
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
                    var op = new Operator(
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
