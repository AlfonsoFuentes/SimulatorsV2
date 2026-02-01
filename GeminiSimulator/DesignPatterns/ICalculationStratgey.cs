namespace GeminiSimulator.DesignPatterns
{
    public interface ICalculationStratgey
    {
        void Calculate(DateTime currentTime);
        void ExecuteProcess();
        void CheckStatus();

    }
}
