using QWENShared.DTOS.SimulationPlanneds;
using Simulator.Shared.Simulations;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    public interface INewPhysicalLoader
    {
        int ExecutionOrder { get; }
        void Load(NewSimulationDTO physicalData);
    }
    public interface INewPlanLoader
    {
        int ExecutionOrder { get; }
        void Load(SimulationPlannedDTO planData);
    }
}
