using QWENShared.DTOS.SimulationPlanneds;
using Simulator.Shared.Simulations;

namespace GeminiSimulator.Main
{
   
    public interface IPhysicalLoader
    {
        int ExecutionOrder { get; }
        void Load(NewSimulationDTO physicalData);
    }

    // FASE 2: Planificación Operativa (Usa SimulationPlannedDTO)
    // Ejemplos: ProcessConfigLoader, ProductionPlanLoader
    public interface IPlanLoader
    {
        int ExecutionOrder { get; }
        void Load(SimulationPlannedDTO planData);
    }
}
