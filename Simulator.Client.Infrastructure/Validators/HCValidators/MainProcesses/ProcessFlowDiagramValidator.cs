using QWENShared.DTOS.MainProcesss;
using QWENShared.Enums;
using Simulator.Client.Infrastructure.ExtensionMethods;
using Simulator.Client.Infrastructure.Managers.ClientCRUDServices;
using Web.Infrastructure.Managers.Generic;

namespace Web.Infrastructure.Validators.FinishinLines.MainProcesss
{

    public class ProcessFlowDiagramValidator : AbstractValidator<ProcessFlowDiagramDTO>
    {
        private readonly IClientCRUDService Service;

        public ProcessFlowDiagramValidator(IClientCRUDService service)
        {
            Service = service;

            RuleFor(x => x.Name).NotEmpty().WithMessage("Name must be defined!");


            RuleFor(x => x.Name).MustBeUnique(service, x => x.Name)
         .WithMessage("Name already exists");

            RuleFor(x => x.FocusFactory).NotEqual(FocusFactory.None).WithMessage("Focus Factory must be defined!");

        }

       
       
    }
}
