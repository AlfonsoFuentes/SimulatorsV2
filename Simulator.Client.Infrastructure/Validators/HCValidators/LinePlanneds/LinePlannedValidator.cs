using QWENShared.DTOS.LinePlanneds;
using QWENShared.Enums;
using Simulator.Client.Infrastructure.Managers.ClientCRUDServices;

namespace Web.Infrastructure.Validators.FinishinLines.LinePlanneds
{

    public class LinePlannedValidator : AbstractValidator<LinePlannedDTO>
    {
        private readonly IClientCRUDService Service;

        public LinePlannedValidator(IClientCRUDService service)
        {
            Service = service;

            RuleFor(x => x.ShiftType).NotEqual(ShiftType.None).WithMessage("Shift Type must be defined!");
            RuleFor(x => x.PlannedSKUDTOs.Count).NotEqual(0).WithMessage("SKU List  must be defined!");

          

        }

        
    }
}
