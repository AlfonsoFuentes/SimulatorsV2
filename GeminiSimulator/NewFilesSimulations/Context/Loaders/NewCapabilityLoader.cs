using GeminiSimulator.NewFilesSimulations.Context;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.WashoutMatrixs
{
    public class NewCapabilityLoader : INewPhysicalLoader
    {
        private readonly NewSimulationContext _context;
        public int ExecutionOrder => 21; // Se ejecuta justo después de crear los equipos

        public NewCapabilityLoader(NewSimulationContext context) => _context = context;

        public void Load(NewSimulationDTO data)
        {
            if (data.MaterialEquipments == null) return;

            Console.WriteLine("--- Cargando Capacidades Material-Equipo ---");

            foreach (var record in data.MaterialEquipments)
            {
                var unit = _context.GetUnit(record.EquipmentId);
                var material = _context.GetMaterial(record.MaterialId);
                if (unit != null && material != null)
                {
                    // Usamos la propiedad Capacity (Amount) de tu record
                    unit.SetProductCapability(material, record.Capacity);
                }
            }
        }
    }
}
