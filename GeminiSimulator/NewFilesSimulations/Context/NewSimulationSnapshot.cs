using GeminiSimulator.DesignPatterns;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    public class NewSimulationSnapshot
    {
        public DateTime CurrentSimTime { get; set; }

        // El ID del equipo es la llave, y su diccionario de campos con estilo es el valor
        public Dictionary<Guid, Dictionary<string, ReportField>> EquipmentData { get; set; } = new();
        public ManufactureScheduler Scheduler { get; set; } = null!;
    }
}

