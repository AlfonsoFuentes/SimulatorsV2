using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers
{
    public class MixerLoader : IPhysicalLoader
    {
        private readonly SimulationContext _context;

        // Ejecutar junto con los equipos (Nivel 20)
        public int ExecutionOrder => 20;

        public MixerLoader(SimulationContext context) => _context = context;

        public void Load(NewSimulationDTO data)
        {
            Console.WriteLine("--- Cargando Mixers ---");
            int count = 0;

            if (data.Mixers == null) return;

            foreach (var dto in data.Mixers)
            {
                // 1. Instanciar
                var mixer = new BatchMixer(dto.Id, dto.Name, dto.EquipmentType, dto.FocusFactory);

                // 2. Mapear Propiedades Comunes (Conectores, Paradas, Capacidades)
                // Nota: Podríamos extraer esto a un Helper, pero por ahora lo dejo explícito.
                mixer.LoadCommonFrom(dto);

                // 3. Registrar
                _context.RegisterUnit(mixer);
                count++;
            }
            Console.WriteLine($" -> {count} Mixers cargados.");
        }
    }
}
