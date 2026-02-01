using QWENShared.DTOS.BackBoneSteps;
using QWENShared.Enums;
using Simulator.Client.Infrastructure.Managers.ClientCRUDServices;
using UnitSystem;

namespace Simulator.Client.Infrastructure.Validators.HCValidators.BackBones
{

    public class BackBoneStepValidator : AbstractValidator<BackBoneStepDTO>
    {
        private readonly IClientCRUDService Service;

        public BackBoneStepValidator(IClientCRUDService service)
        {
            Service = service;

            RuleFor(x => x.BackBoneStepType).NotEqual(BackBoneStepType.None).WithMessage("Type must be defined!");
            RuleFor(x => x.Percentage).NotEqual(0).When(x => x.BackBoneStepType == BackBoneStepType.Add).WithMessage("Percentage must be defined!");
            RuleFor(x => x.StepRawMaterial).NotNull().When(x => x.BackBoneStepType == BackBoneStepType.Add).WithMessage("Raw Material must be defined!");
            RuleFor(x => x.Time).NotEqual(new UnitSystem.Amount(0, TimeUnits.Minute)).When(x => x.BackBoneStepType == BackBoneStepType.Mixing).WithMessage("Time must be defined!");


            RuleFor(x => x.Time).NotEqual(new UnitSystem.Amount(0, TimeUnits.Minute)).When(x => x.BackBoneStepType == BackBoneStepType.Analisys).WithMessage("Time must be defined!");


            RuleFor(x => x.Time).NotEqual(new UnitSystem.Amount(0, TimeUnits.Minute)).When(x => x.BackBoneStepType == BackBoneStepType.Adjust).WithMessage("Time must be defined!");


            RuleFor(x => x.Time).NotEqual(new UnitSystem.Amount(0, TimeUnits.Minute)).When(x => x.BackBoneStepType == BackBoneStepType.Connect_Mixer_WIP).WithMessage("Time must be defined!");

            RuleFor(x => x.StepRawMaterial).NotNull().When(x => x.BackBoneStepType == BackBoneStepType.Washout).WithMessage("Washout material must be defined!");





        }

       
    }
}
