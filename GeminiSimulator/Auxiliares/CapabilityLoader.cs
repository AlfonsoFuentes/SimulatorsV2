using GeminiSimulator.Main;
using Simulator.Shared.Simulations;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeminiSimulator.Auxiliares
{
    public class CapabilityLoader : IPhysicalLoader
    {
        private readonly SimulationContext _context;
        public int ExecutionOrder => 21; // Se ejecuta justo después de crear los equipos

        public CapabilityLoader(SimulationContext context) => _context = context;

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
    public class TopologyLoader : IPhysicalLoader
    {
        private readonly SimulationContext _context;
        public int ExecutionOrder => 22; // Después de las capacidades

        public TopologyLoader(SimulationContext context) => _context = context;

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
