using QWENShared.DTOS.Mixers;
using QWENShared.Enums;

namespace Simulator.Client.HCPages.Mixers;
public partial class MixerTable
{
    [Parameter]
    public List<MixerDTO> Items { get; set; } = new();
    string nameFilter = string.Empty;
    public Func<MixerDTO, bool> Criteria => x => x.Name.Contains(nameFilter, StringComparison.InvariantCultureIgnoreCase);
    public List<MixerDTO> FilteredItems => string.IsNullOrEmpty(nameFilter) ? Items :
        Items.Where(Criteria).ToList();
    [Parameter]
    public Guid MainProcessId { get; set; }
    [Parameter]
    [EditorRequired]
    public FocusFactory FocusFactory { get; set; } = FocusFactory.None;
    [Parameter]
    public EventCallback GetAllExternal { get; set; }
    async Task GetAll()
    {
        await GetAllExternal.InvokeAsync();
    }

    public async Task AddNew()
    {
        MixerDTO response = new()
        {
            MainProcessId = MainProcessId,
            FocusFactory = FocusFactory
        };

        var parameters = new DialogParameters<MixerDialog>
        {
           { x => x.Model, response },
        };

        var options = new DialogOptions() { MaxWidth = MaxWidth.Medium };

        var dialog = await DialogService.ShowAsync<MixerDialog>("Mixer", parameters, options);
        var result = await dialog.Result;
        if (result != null && !result.Canceled)
        {
            await GetAll();
            StateHasChanged();
        }

    }
    async Task Edit(MixerDTO response)
    {


        var parameters = new DialogParameters<MixerDialog>
        {

             { x => x.Model, response },
        };
        var options = new DialogOptions() { MaxWidth = MaxWidth.Medium };


        var dialog = await DialogService.ShowAsync<MixerDialog>("Mixer", parameters, options);
        var result = await dialog.Result;
        if (result != null && !result.Canceled)
        {
            await GetAll();
        }
    
    }
    public async Task Delete(MixerDTO response)
    {
        var parameters = new DialogParameters<DialogTemplate>
        {
            { x => x.ContentText, $"Do you really want to delete {response.Name}? This process cannot be undone." },
            { x => x.ButtonText, "Delete" },
            { x => x.Color, Color.Error }
        };

        var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall };

        var dialog = await DialogService.ShowAsync<DialogTemplate>("Delete", parameters, options);
        var result = await dialog.Result;


        if (!result!.Canceled)
        {
            
            var resultDelete = await ClientService.Delete(response);
            if (resultDelete.Succeeded)
            {
                await GetAll();
               


            }
          
        }
 

    }
    HashSet<MixerDTO> SelecteItems = null!;
    public async Task DeleteGroup()
    {
        if (SelecteItems == null) return;
        var parameters = new DialogParameters<DialogTemplate>
        {
            { x => x.ContentText, $"Do you really want to delete this {SelecteItems.Count} Items? This process cannot be undone." },
            { x => x.ButtonText, "Delete" },
            { x => x.Color, Color.Error }
        };

        var options = new DialogOptions() { CloseButton = true, MaxWidth = MaxWidth.ExtraSmall };

        var dialog = await DialogService.ShowAsync<DialogTemplate>("Delete", parameters, options);
        var result = await dialog.Result;


        if (!result!.Canceled)
        {
           
            var resultDelete = await ClientService.DeleteGroup(SelecteItems.ToList());
            if (resultDelete.Succeeded)
            {
                await GetAll();
             
                SelecteItems = null!;

            }
            
        }


    }
}
