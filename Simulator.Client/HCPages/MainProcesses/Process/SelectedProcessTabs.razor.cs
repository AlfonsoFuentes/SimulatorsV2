using QWENShared.DTOS.Tanks;
using Simulator.Shared.NuevaSimlationconQwen;
using static MudBlazor.CategoryTypes;

namespace Simulator.Client.HCPages.MainProcesses.Process
{
    public partial class SelectedProcessTabs
    {
        async Task GetAllTanks()
        {
            var result = await ClientService.GetAll(new TankDTO()
            {
                MainProcessId = MainProcessId,


            });
            if (result.Succeeded)
            {
                SimulationDTO.Tanks = result.Data;
                StateHasChanged();
            }
        }
    }
}
