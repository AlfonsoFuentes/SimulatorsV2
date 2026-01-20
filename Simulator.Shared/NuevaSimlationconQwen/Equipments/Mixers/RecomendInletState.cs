using Simulator.Shared.NuevaSimlationconQwen.Equipments.Operators;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Pumps;
using Simulator.Shared.NuevaSimlationconQwen.ManufacturingOrders;
using Simulator.Shared.NuevaSimlationconQwen.Materials;
using Simulator.Shared.NuevaSimlationconQwen.States.BaseClass;
using System;
using System.Collections.Generic;
using System.Text;

namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers
{
    public interface IShortLabelInletStateMixer
    {
        string ShortLabel { get; }
    }
    public interface IMixerStarved
    {
    }

    public abstract class MixerInletState : InletState<ProcessMixer>, IShortLabelInletStateMixer
    {
        protected MixerInletState(ProcessMixer mixer) : base(mixer) { }
        public string ShortLabel { get; set; } = string.Empty;
    }

    public class MixerInletWaitingForManufactureOrderState : MixerInletState, IMixerStarved
    {
        public MixerInletWaitingForManufactureOrderState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = $"Waiting for Manufacture Order";

            // Así estaba...
            // AddTransition<MixerInletReviewForWashingAtInitBatchState>(mixer => mixer.InitBatchFromQueue());

            // PARCHE: Ahora antes de revisar lavado, debemos capturar la disponibilidad del operario
            AddTransition<MixerInletWaitingForOperatorState>(mixer => mixer.InitBatchFromQueue());
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.NoBatchAssigned);
        }
    }

    public class MixerInletReviewForWashingAtInitBatchState : MixerInletState
    {
        public MixerInletReviewForWashingAtInitBatchState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = $"Reviewing Init Washout Batch";
            AddTransition<MixerInletReviewWashingTankState>(mixer => mixer.IsMustWashTank());
            AddTransition<MixerInletSelectNextStepState>();
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.StarvedByWashoutPump);
        }
    }

    public class MixerInletReviewWashingTankState : MixerInletState
    {
        public MixerInletReviewWashingTankState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = $"{mixer.Name} review any washing pump available";
            AddTransition<MixerInletWashingTankState>(mixer => mixer.IsWashoutPumpAvailable());
            AddTransition<MixerInletStarvedWashingTankState>();
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.StarvedByWashoutPump);
        }
    }

    public class MixerInletWashingTankState : MixerInletState
    {
        Amount WashingTime = new Amount(0, TimeUnits.Second);
        Amount CurrentTime = new Amount(0, TimeUnits.Second);
        Amount PendingTime => WashingTime - CurrentTime;
        public MixerInletWashingTankState(ProcessMixer mixer) : base(mixer)
        {
            WashingTime = mixer.GetWashoutTime();
            StateLabel = $"{mixer.Name} Washing Tank";

            // PARCHE: El lavado es ATÓMICO. No hay transición a parada programada aquí.
            AddTransition<MixerInletSelectNextStepState>(mixer => IsWashingTimeCompleted());
        }
        public override void Run(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Washing);
            StateLabel = $"{Context.Name} Washing Tank {Math.Round(PendingTime.GetValue(TimeUnits.Minute), 2)}, min";
            CurrentTime += Context.OneSecond;
        }
        bool IsWashingTimeCompleted()
        {
            if (PendingTime <= Context.ZeroTime)
            {
                Context.ReleaseWashoutPump();
                return true;
            }
            return false;
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Washing);
        }
    }

    public class MixerInletStarvedWashingTankState : MixerInletState, IMixerStarved
    {
        public MixerInletStarvedWashingTankState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = $"{mixer.Name} Washing Starved";
            AddTransition<MixerInletWashingTankState>(mixer =>
            {
                if (mixer.Feeder != null && mixer.Feeder.OcuppiedBy == mixer)
                {
                    return true;
                }
                return false;
            });
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.StarvedByWashoutPump);
        }
    }

    public class MixerInletSelectNextStepState : MixerInletState
    {
        public MixerInletSelectNextStepState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = $"Select Next Step";
            mixer.CurrentManufactureOrder.CurrentStep = null!;
            mixer.InitBatchDate();

            // PARCHE: Antes de iniciar cualquier paso, verificamos si el operario entró en parada programada (Almuerzo)
            AddTransition<MixerInletStarvedByOperatorBreakState>(mixer => mixer.IsCapturedOperatorOnBreak());

            // Así estaba... (Transiciones originales)
            AddTransition<MixerFinishingBatchState>(mixer => mixer.IsManufacturingRecipeFinished());
            AddTransition<MixerBatchingByOperatorTimeState>(_mixer => _mixer.IsCurrentStepByOperator());
            AddTransition<MixerBatchingByTimeState>(_mixer => _mixer.IsCurrentStepDifferentThanAdd());
            AddTransition<MixerBatchingByMassState>(_mixer => _mixer.IsCurrentStepFeederAvailable());
            AddTransition<MixerBatchingByMassStarvedByFeederNoAvailableState>();
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Batching);
        }
    }

    public class MixerBatchingByMassState : MixerInletState
    {
        public Amount RequiredMass = new Amount(0, MassUnits.KiloGram);
        public Amount PendingMass = new Amount(0, MassUnits.KiloGram);
        Amount StepMass = new Amount(0, MassUnits.KiloGram);
        IRecipeStep CurrentStep => CurrentManufactureOrder.CurrentStep;
        Amount BatchSize => CurrentManufactureOrder.BatchSize;
        IManufactureOrder CurrentManufactureOrder => Context.CurrentManufactureOrder;
        private readonly IManufactureFeeder? _assignedFeeder;
        public MixerBatchingByMassState(ProcessMixer mixer) : base(mixer)
        {
            _assignedFeeder = Context.Feeder;
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Batching);
            CalculateMassSteRequirements();
            StateLabel = $"{Context.CurrentManufactureOrder.CurrentStep.StepNumber} of " +
                         $"{Context.CurrentManufactureOrder.TotalSteps} - " +
                         $"Adding {Context.CurrentManufactureOrder.CurrentStep.RawMaterialName}";
            ShortLabel = $"{PendingMass.ToString()} of {RequiredMass.ToString()}";
            AddTransition<MixerInletSelectNextStepState>(_mixer => IsMassStepFinalized());
        }

        public override void Run(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Batching);
            CalculateMassStep();
            StateLabel = $"{Context.CurrentManufactureOrder.CurrentStep.StepNumber} of " +
                         $"{Context.CurrentManufactureOrder.TotalSteps} - " +
                         $"Adding {Context.CurrentManufactureOrder.CurrentStep.RawMaterialName}";
            ShortLabel = $"{PendingMass.ToString()} of {RequiredMass.ToString()}";
        }

        public bool IsMassStepFinalized()
        {
            if (PendingMass <= Context.ZeroMass)
            {
                RequiredMass = new Amount(0, MassUnits.KiloGram);
                PendingMass = new Amount(0, MassUnits.KiloGram);
                StepMass = new Amount(0, MassUnits.KiloGram);
                if (_assignedFeeder != null)
                {
                    Context.ReleaseFeeder(_assignedFeeder);
                }

                return true;
            }
            return false;
        }

        public void CalculateMassSteRequirements()
        {
            var step = CurrentStep;
            var batchsize = BatchSize;
            Context.Feeder?.ActualFlow = Context.Feeder?.Flow ?? new Amount(1, MassFlowUnits.Kg_min);
            RequiredMass = step.Percentage / 100 * batchsize;
            StepMass = Context.Feeder?.Flow * Context.OneSecond;
            PendingMass += RequiredMass;

        }

        public void CalculateMassStep()
        {
            if (Context.Feeder is ProcessOperator)
            {
                PendingMass -= RequiredMass;
                Context.CurrentLevel += RequiredMass;
            }
            else
            {
                if (PendingMass <= StepMass)
                {
                    StepMass = PendingMass;
                }
                PendingMass -= StepMass;
                Context.CurrentLevel += StepMass;
            }
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Batching);
        }
    }

    public class MixerBatchingByMassStarvedByFeederNoAvailableState : MixerInletState, IMixerStarved
    {
        public MixerBatchingByMassStarvedByFeederNoAvailableState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = $"{mixer.CurrentManufactureOrder.CurrentStep.StepNumber} of {mixer.CurrentManufactureOrder.TotalSteps} ";
            ShortLabel = $"Starved by {mixer.CurrentManufactureOrder.CurrentStep.RawMaterialName}";
            AddTransition<MixerBatchingByMassState>(_mixer =>
            {
                if (mixer.Feeder != null && mixer.Feeder.OcuppiedBy == mixer)
                {
                    return true;
                }
                return false;
            });
        }
       
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.StarvedByFeeder);
        }
    }

    public class MixerBatchingByOperatorTimeState : MixerInletState
    {
        private readonly Amount _time;
        private Amount _currentTime = new Amount(0, TimeUnits.Second);
        private readonly Amount _oneSecond = new Amount(1, TimeUnits.Second);
        public Amount PendingTime => _time - _currentTime;

        public MixerBatchingByOperatorTimeState(ProcessMixer mixer) : base(mixer)
        {
            _time = mixer.CurrentManufactureOrder.CurrentStep.Time;
            StateLabel = $"{mixer.CurrentManufactureOrder.CurrentStep.StepNumber} of {mixer.CurrentManufactureOrder.TotalSteps} - {mixer.CurrentManufactureOrder.CurrentStep.BackBoneStepType} ";
            ShortLabel = $"{PendingTime.ToString()} of {_time.ToString()}";
            AddTransition<MixerInletSelectNextStepState>(_mixer => PendingTime <= _mixer.ZeroTime);
        }
        public override void Run(DateTime currentdate)
        {
            
            _currentTime += _oneSecond;
            StateLabel = $"{Context.CurrentManufactureOrder.CurrentStep.StepNumber} of {Context.CurrentManufactureOrder.TotalSteps} - {Context.CurrentManufactureOrder.CurrentStep.BackBoneStepType} ";
            ShortLabel = $"{PendingTime.ToString()} of {_time.ToString()}";
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Batching);
        }
    }

    public class MixerBatchingByTimeState : MixerInletState
    {
        private readonly Amount _time;
        private Amount _currentTime = new Amount(0, TimeUnits.Second);
        private readonly Amount _oneSecond = new Amount(1, TimeUnits.Second);
        public Amount PendingTime => _time - _currentTime;

        public MixerBatchingByTimeState(ProcessMixer mixer) : base(mixer)
        {
            _time = mixer.CurrentManufactureOrder.CurrentStep.Time;
            StateLabel = $"{mixer.CurrentManufactureOrder.CurrentStep.StepNumber} of {mixer.CurrentManufactureOrder.TotalSteps} - {mixer.CurrentManufactureOrder.CurrentStep.BackBoneStepType} ";
            ShortLabel = $"{PendingTime.ToString()} of {_time.ToString()}";
            AddTransition<MixerInletSelectNextStepState>(_mixer => PendingTime <= _mixer.ZeroTime);
        }
        public override void Run(DateTime currentdate)
        {
          
            _currentTime += _oneSecond;
            StateLabel = $"{Context.CurrentManufactureOrder.CurrentStep.StepNumber} of {Context.CurrentManufactureOrder.TotalSteps} - {Context.CurrentManufactureOrder.CurrentStep.BackBoneStepType} ";
            ShortLabel = $"{PendingTime.ToString()} of {_time.ToString()}";
        }
        public override void Report(DateTime currentdate)
        {
            Context.CurrentBatchReport?.RecordTime(MixerTimeType.Batching);
        }
    }

    public class MixerFinishingBatchState : MixerInletState
    {
        public MixerFinishingBatchState(ProcessMixer mixer) : base(mixer)
        {
            StateLabel = $"Transfering Batch to Mixer";

            // PARCHE: Al finalizar la secuencia de receta, el operario se libera para el vecino
            mixer.ReleaseCapturedOperator();
        }
    }
}
