using QWENShared.DTOS.PreferedMixers;
using Simulator.Client.Infrastructure.Managers.ClientCRUDServices;
using Web.Infrastructure.Managers.Generic;

namespace Web.Infrastructure.Validators.FinishinLines.PreferedMixers
{

    public class PreferedMixerValidator : AbstractValidator<PreferedMixerDTO>
    {
        private readonly IClientCRUDService Service;

        public PreferedMixerValidator(IClientCRUDService service)
        {
            Service = service;

          
            RuleFor(x => x.Mixer).NotNull().WithMessage("Mixer must be defined!");
          
           
        }

       
    }
}
