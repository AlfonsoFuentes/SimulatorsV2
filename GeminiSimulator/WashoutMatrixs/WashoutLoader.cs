using GeminiSimulator.Main;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.WashoutMatrixs
{
    public class WashoutLoader : IPhysicalLoader
    {
        private readonly SimulationContext _context;

        // Nivel 10: Definiciones (Junto con SKUs). 
        // No depende de equipos, pero los equipos dependerán de esto.
        public int ExecutionOrder => 10;

        public WashoutLoader(SimulationContext context)
        {
            _context = context;
        }

        public void Load(NewSimulationDTO data)
        {
            Console.WriteLine("--- Cargando Matriz de Tiempos de Lavado (Washouts) ---");
            int count = 0;

            if (data.WashouTimes == null) return;

            foreach (var dto in data.WashouTimes)
            {
                // Agregamos la regla a la matriz del contexto
                _context.WashoutRules.AddRule(
                    dto.ProductCategoryCurrent,  // Desde
                    dto.ProductCategoryNext,     // Hasta
                    dto.MixerWashoutTime,        // Tiempo Mixer
                    dto.LineWashoutTime          // Tiempo Línea
                );
                count++;
            }

            Console.WriteLine($" -> {count} Reglas de lavado cargadas.");
        }
    }
}
