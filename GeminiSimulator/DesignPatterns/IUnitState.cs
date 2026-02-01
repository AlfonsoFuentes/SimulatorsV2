using GeminiSimulator.Plans;
using GeminiSimulator.PlantUnits;

namespace GeminiSimulator.DesignPatterns
{
    public interface IUnitState
    {
        // --- Identificación ---
        string StateName { get; }

        string SubStateName { get; }

        // --- Ejecución ---
        /// <summary>
        /// Realiza los cálculos de flujo de masa y energía específicos para este estado.
        /// (Física pura)
        /// </summary>
        void Calculate();

        /// <summary>
        /// Evalúa las condiciones para cambiar a otro estado.
        /// (Lógica de negocio y reglas de control)
        /// </summary>
        void CheckTransitions();
    }
    
}
