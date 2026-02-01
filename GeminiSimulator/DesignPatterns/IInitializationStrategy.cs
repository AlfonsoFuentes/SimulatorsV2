using GeminiSimulator.Main;

namespace GeminiSimulator.DesignPatterns
{
    public interface IInitializationStrategy
    {
        // Prepara todas las unidades del contexto según una lógica específica
        void PrepareUnit(DateTime InitialDate);
    }
}
