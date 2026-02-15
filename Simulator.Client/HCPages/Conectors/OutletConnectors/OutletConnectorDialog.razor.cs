using MudBlazor;
using QWENShared.DTOS.BaseEquipments;
using QWENShared.DTOS.Conectors;
using QWENShared.Enums;

namespace Simulator.Client.HCPages.Conectors.OutletConnectors
{
    public partial class OutletConnectorDialog
    {
        [CascadingParameter]
        private IMudDialogInstance MudDialog { get; set; } = null!;
        private bool Validated { get; set; } = false;

        async Task ValidateAsync()
        {
            Validated = _fluentValidationValidator == null ? false : await _fluentValidationValidator.ValidateAsync(options => { options.IncludeAllRuleSets(); });
        }

        public BaseEquipmentDTO FromEquipment => Model.From!;
        Guid MainProcessId => Model.MainProcessId;
        ProcessEquipmentType EquipmentType => FromEquipment == null ? ProcessEquipmentType.None : FromEquipment.EquipmentType;
        protected override async Task OnInitializedAsync()
        {
            await GetAllEquipments();
            await getById();

        }
        FluentValidationValidator _fluentValidationValidator = null!;

        private async Task Submit()
        {
            if (Model.FromId == Guid.Empty)
            {
                MudDialog.Close(DialogResult.Ok(true));
                return;
            }
            var result = await ClientService.Save(Model);


            if (result.Succeeded)
            {
                
                MudDialog.Close(DialogResult.Ok(true));
            }
           

        }


        private void Cancel() => MudDialog.Cancel();

        [Parameter]
        public OutletConnectorDTO Model { get; set; } = new();
        async Task getById()
        {
            if (Model.Id == Guid.Empty)
            {
                return;
            }
            var result = await ClientService.GetById(Model);
            if (result.Succeeded)
            {
                Model = result.Data;
            }
        }

        List<BaseEquipmentDTO> Items { get; set; } = new();
        async Task GetAllEquipments()
        {
            var result = await ClientService.GetAll(new BaseEquipmentDTO()
            {
                MainProcessId = MainProcessId,
            });
            if (result.Succeeded)
            {
                switch (EquipmentType)
                {
                    case ProcessEquipmentType.Pump:
                        Items = result.Data.Where(x =>
                        x!.EquipmentType == ProcessEquipmentType.Tank ||
                        x!.EquipmentType == ProcessEquipmentType.Mixer ||
                        x!.EquipmentType == ProcessEquipmentType.Line ||
                        x!.EquipmentType == ProcessEquipmentType.ContinuousSystem).ToList();
                        break;
                    case ProcessEquipmentType.Mixer:
                    case ProcessEquipmentType.Tank:
                        Items = result.Data.Where(x => x!.EquipmentType == ProcessEquipmentType.Pump).ToList();
                        break;
                    case ProcessEquipmentType.Operator:
                        Items = result.Data.Where(x => x!.EquipmentType == ProcessEquipmentType.Mixer
                        || x!.EquipmentType == ProcessEquipmentType.ContinuousSystem).ToList();
                        break;
                    case ProcessEquipmentType.ContinuousSystem:
                        Items = result.Data.Where(x => x!.EquipmentType == ProcessEquipmentType.Tank).ToList();
                        break;
                    case ProcessEquipmentType.StreamJoiner:
                        Items = result.Data.Where(x => x!.EquipmentType == ProcessEquipmentType.Line).ToList();
                        break;
                    default:
                        Items = result.Data.Where(x => x!.EquipmentType == ProcessEquipmentType.Pump).ToList();
                        break;

                }
            }

        }
        
    }
}
