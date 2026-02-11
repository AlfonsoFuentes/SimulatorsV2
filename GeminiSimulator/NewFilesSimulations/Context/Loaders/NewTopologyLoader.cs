using GeminiSimulator.NewFilesSimulations.Context;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.WashoutMatrixs
{
    public class NewTopologyLoader : INewPhysicalLoader
    {
        private readonly NewSimulationContext _context;
        public int ExecutionOrder => 22; // Después de las capacidades

        public NewTopologyLoader(NewSimulationContext context) => _context = context;

        public void Load(NewSimulationDTO data)
        {
            if (data.Connectors == null) return;

            Console.WriteLine("--- Cargando Topología de Conexiones ---");

            foreach (var connector in data.Connectors)
            {
                var fromUnit = _context.GetUnit(connector.FromId);
                var toUnit = _context.GetUnit(connector.ToId);

                // Registramos la salida en el origen y la entrada en el destino
                fromUnit?.AddOutlet(connector.ToId);
                toUnit?.AddInlet(connector.FromId);
            }
        }
    }
}
