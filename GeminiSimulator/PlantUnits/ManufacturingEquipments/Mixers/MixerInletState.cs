


//namespace GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers
//{
//    public abstract class MixerInletState : IUnitState
//    {
//        protected BatchMixer _mixer;
//        public abstract string StateName { get; }
//        public virtual string SubStateName => string.Empty;
//        public MixerInletState(BatchMixer m) => _mixer = m;
//        public virtual void Calculate() { }
//        public virtual void CheckTransitions() { }

//        // Helper para Opción 4 (Timer) disponible en todos los estados

//    }
//    public interface IBatchStarvedByPump
//    {

//    }

//    // ================================================================
//    // 1. IDLE
//    // ================================================================
//    public class MixerIdle : MixerInletState
//    {
//        public MixerIdle(BatchMixer m) : base(m) { }
//        public override string StateName => $"{_mixer.Name} Available";
//        // Espera pasiva a ReceiveRequirementFromWIP
//    }
//    public class MixerStarvedByAtInitOperator : MixerInletState
//    {
//        public MixerStarvedByAtInitOperator(BatchMixer m) : base(m)
//        {

//            if (_mixer.BatchOperator != null && _mixer.BatchOperator.CurrentOwner != null && _mixer.BatchOperator.CurrentOwner is BatchMixer mixer)
//            {
//                mixeroccpyOperator = mixer;
//            }


//        }
//        BatchMixer? mixeroccpyOperator;
//        public override string StateName => _mixer.EngagementType == OperatorEngagementType.StartOnDefinedTime ?
//            $"Operator in use by {_mixer.BatchOperator?.CurrentOwner?.Name ?? string.Empty}" : $"{_mixer.Name} Starved by operator";
//        public override string SubStateName => _mixer.EngagementType == OperatorEngagementType.StartOnDefinedTime ?
//            $"Operator will be in {mixeroccpyOperator?.PendingOperatorRealse:F0}/{mixeroccpyOperator?.OperatorStdSetupTime}, s" : $"Operator in use by {mixeroccpyOperator?.Name}";
//        public override void Calculate()
//        {
//            _mixer.NetStarvedTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.StarvedByOperator);
//        }
//        public override void CheckTransitions()
//        {
//            if (_mixer.BatchOperator?.InboundState is IOperatorPlanned) return;

//            if (_mixer.BatchOperator?.CurrentOwner == _mixer )  //Si da true es porque el operario recibio release y sigue el siguiente paso que es intentar lavar
//            {

//                if (_mixer.NeedsWashing())
//                {
//                    if (_mixer.WashingPump?.OutboundState is PumpAvailable)
//                    {
//                        _mixer.TransitionInBound(new MixerManagingWashing(_mixer));
//                        return;   //Devulve return porque encontro donde iniciar el batche=>en el lavado, no necesita revisar el inicio del batche
//                    }
//                    _mixer.TransitionInBound(new MixerManagingWashingStarved(_mixer));
//                    return;      //Devulve return porque encontro donde iniciar el batche=>esperando que la bomba se desocupe no necesita revisar el inicio del batche
//                }
//                //S no necesita lavar se selecciona siugiente estado
//                _mixer.SelectNextState();

//            }

//        }
//    }




//    // ================================================================
//    // 3. MANAGING WASHING (Lógica Estricta)
//    // ================================================================
//    public class MixerManagingWashing : MixerInletState
//    {
//        double counter = 0;
//        double MaxWashingTime { get; set; }
//        double PendingWashingTime => MaxWashingTime - counter;
//        public MixerManagingWashing(BatchMixer m) : base(m)
//        {
//            if (_mixer.LastMaterialProcessed != null && _mixer.CurrentMaterial != null)
//                MaxWashingTime = _mixer.Context?.WashoutRules.GetMixerWashout(_mixer.LastMaterialProcessed.Category, _mixer.CurrentMaterial.Category).GetValue(TimeUnits.Second) ?? 0;
//        }

//        public override string StateName => $"{_mixer.Name} Washing {_mixer.LastMaterialProcessed?.Name ?? string.Empty}";
//        public override string SubStateName => $"Washing: {PendingWashingTime:F0}/{MaxWashingTime:F0}, s";
//        public override void Calculate()
//        {
//            counter++;
//            _mixer.CalculateOperatorStatus();
//            _mixer.AccumulateTime(MixerStateCategory.Washing);

//        }
//        public override void CheckTransitions()
//        {
//            if (PendingWashingTime <= 0)     //termina el lavado
//            {
//                _mixer.WashingPump?.ReleaseAccess(_mixer);
//                _mixer.SelectNextState();
//            }
//            _mixer.CheckOperatorStatus();

//        }
//    }
//    public class MixerManagingWashingStarved : MixerInletState, IBatchStarvedByPump
//    {

//        public MixerManagingWashingStarved(BatchMixer m) : base(m)
//        {

//        }

//        public override string StateName => $"{_mixer.Name} Washout Starved";
//        public override string SubStateName => $"Washing pump in use by: {_mixer.WashingPump?.CurrentOwner?.Name ?? string.Empty}";
//        public override void Calculate()
//        {

//            _mixer.NetStarvedTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.StarvedByWashoutPump);
//        }
//        public override void CheckTransitions()
//        {
//            if (_mixer.WashingPump?.CurrentOwner == _mixer)
//            {
//                _mixer.TransitionInBound(new MixerManagingWashing(_mixer));

//            }
//            _mixer.CheckOperatorStatus();
//        }
//    }



//    // ================================================================
//    // 5. STATES DE EJECUCIÓN (Bomba, Manual, Tiempo)
//    // ================================================================

//    public class MixerFillingWithPump : MixerInletState
//    {
//        private double _targetMass;
//        double flow = 0;
//        double currentMass = 0;
//        double PendingMass => _targetMass - currentMass;
//        string IngredientName = "";
//        public MixerFillingWithPump(BatchMixer m) : base(m)
//        {
//            _targetMass = _mixer.Capacity * _mixer.CurrentStep?.TargetPercentage / 100 ?? 0;
//            flow = _mixer.CurrentFillingPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
//            IngredientName = _mixer.CurrentStep?.IngredientName ?? string.Empty;
//            _mixer.CurrentStepPendingMass = _targetMass;
//        }

//        public override string StateName => $"{_mixer.CurrentStep?.Order ?? 0}-{_mixer.CurrentTotalSteps} - Adding {IngredientName}";
//        public override string SubStateName => $"{PendingMass:F2}/{_targetMass:F2}, kg";
//        public override void Calculate()
//        {

//            flow = PendingMass > flow ? flow : PendingMass;
//            currentMass += flow;
//            _mixer.CurrentFillingPump?.SetCommandedFlow(flow, _mixer);
//            _mixer.CurrentMass += flow;
//            _mixer.CalculateOperatorStatus();
//            _mixer.CurrentStepPendingMass -= flow;
//            _mixer.NetBatchTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.Batching);

//        }
//        public override void CheckTransitions()
//        {
//            if (PendingMass <= 0)
//            {
//                _mixer.CurrentFillingPump?.SetCommandedFlow(0, _mixer);
//                _mixer.CurrentFillingPump?.ReleaseAccess(_mixer);
//                _mixer.CurrentFillingPump = null;
//                _mixer.CurrentStepPendingMass = 0; // Ya no falta nada

//                _mixer.SelectNextState();
//            }
//            _mixer.CheckOperatorStatus();
//            //Aqui falta bomba parada por el tanque
//        }
//    }
//    public class MixerFillingStarvedWithPump : MixerInletState, IBatchStarvedByPump
//    {

//        public MixerFillingStarvedWithPump(BatchMixer m) : base(m)
//        {

//        }

//        public override string StateName => $"{_mixer.CurrentStep?.Order ?? 0}-{_mixer.CurrentTotalSteps} Starved ";

//        public override string SubStateName => $"{_mixer.CurrentFillingPump?.Name ?? string.Empty} in use by {_mixer.CurrentFillingPump?.CurrentOwner?.Name ?? string.Empty}";
//        public override void Calculate()
//        {

//            _mixer.NetStarvedTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.StarvedByFeederPump);
//        }
//        public override void CheckTransitions()
//        {
//            if (_mixer.CurrentFillingPump?.CurrentOwner == _mixer)
//            {
//                _mixer.TransitionInBound(new MixerFillingWithPump(_mixer));
//            }
//            _mixer.CheckOperatorStatus();
//        }
//    }
//    public class MixerFillingManual : MixerInletState
//    {
//        private double _targetMass;

//        public MixerFillingManual(BatchMixer m) : base(m)
//        {
//            _targetMass = _mixer.Capacity * _mixer.CurrentStep?.TargetPercentage / 100 ?? 0;
//        }
//        public override string StateName => $"{_mixer.CurrentStep?.Order ?? 0}-{_mixer.CurrentTotalSteps} ";
//        public override string SubStateName => $"Adding {_mixer.CurrentStep?.IngredientName}";
//        public override void Calculate()
//        {
//            _mixer.CurrentMass += _targetMass;
//            _mixer.CalculateOperatorStatus();
//            _mixer.NetBatchTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.Batching);
//        }
//        public override void CheckTransitions()
//        {
//            _mixer.SelectNextState();
//            _mixer.CheckOperatorStatus();
//        }
//    }

//    public class MixerProcessingTime : MixerInletState
//    {
//        double counter = 0;
//        double maxtime = 0;
//        double pendingtime => maxtime - counter;
//        public MixerProcessingTime(BatchMixer m) : base(m)
//        {

//            maxtime = _mixer.CurrentStep?.Duration.GetValue(TimeUnits.Second) ?? 0;

//        }
//        public override string StateName => $"{_mixer.CurrentStep?.Order ?? 0}-{_mixer.CurrentTotalSteps} {_mixer.CurrentStep?.OperationType.ToString() ?? string.Empty}";
//        public override string SubStateName => $"{pendingtime:F0}/{maxtime:F0}, s";
//        public override void Calculate()
//        {
//            counter++;
//            _mixer.CalculateOperatorStatus();
//            _mixer.NetBatchTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.Batching);
//        }
//        public override void CheckTransitions()
//        {
//            if (pendingtime <= 0)
//            {
//                _mixer.SelectNextState();
//            }
//            _mixer.CheckOperatorStatus();
//        }
//    }

//    // ================================================================
//    // 6. DISCHARGING (Último paso de la cadena)
//    // ================================================================
//    public class MixerDischarging : MixerInletState
//    {
//        double outleflow = 0;


//        public MixerDischarging(BatchMixer m) : base(m)
//        {
//            outleflow = _mixer.DischargeRate;

//        }
//        public override string StateName => $"Discharging to {_mixer.CurrentWipTank?.Name ?? string.Empty}";
//        public override string SubStateName => $"{_mixer.CurrentMass:F0}/{_mixer.Capacity:F0}, s";
//        public override void Calculate()
//        {
//            outleflow = _mixer.CurrentMass > outleflow ? outleflow : _mixer.CurrentMass;
//            _mixer.CurrentMass -= outleflow;
//            _mixer.CurrentWipTank?.SetInletFlow(outleflow);
//            _mixer.AccumulateTime(MixerStateCategory.Discharging);
//        }
//        public override void CheckTransitions()
//        {
//            if (_mixer.CurrentWipTank?.InboundState is TankInletStarvedHighLevel)
//            {
//                _mixer.CurrentWipTank?.SetInletFlow(0);
//                _mixer.TransitionInBound(new MixerDischargingHighLevelStarved(_mixer));
//                return;
//            }

//            if (_mixer.CurrentMass <= 0)
//            {

//                _mixer.CurrentWipTank?.SetInletFlow(0);
//                _mixer.CurrentWipTank?.ReleaseAccess(_mixer);
//                _mixer.CurrentWipTank = null;
//                _mixer.CurrentMass = 0;
//                _mixer.CurrentMaterial = null;

//                if (_mixer.WipsQueue.Count > 0)
//                {
//                    var next = _mixer.WipsQueue.Dequeue();
//                    _mixer.SelectInitState(next);
//                    return;
//                }
//                _mixer.TransitionInBound(new MixerIdle(_mixer));
//            }
//        }
//    }
//    public class MixerDischargingHighLevelStarved : MixerInletState
//    {
//        public MixerDischargingHighLevelStarved(BatchMixer m) : base(m)
//        {
//            // Lógica Opción 2: FullBatch se libera al final

//        }
//        public override string StateName => $"Discharging Starved ";
//        public override string SubStateName => $"{_mixer.CurrentWipTank?.Name ?? string.Empty} High Level";
//        public override void Calculate()
//        {
//            _mixer.NetStarvedTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.StarvedByTankHighLevel);
//        }
//        public override void CheckTransitions()
//        {
//            if (_mixer.CurrentWipTank?.IsOwnedBy(_mixer) ?? true)
//            {
//                if (_mixer.CurrentWipTank?.InboundState is TankReceiving)
//                {
//                    _mixer.TransitionInBound(new MixerDischarging(_mixer));

//                    return;
//                }

//            }
//        }
//    }

//    public class MixerDischargingStarvedByTankBusy : MixerInletState
//    {
//        public MixerDischargingStarvedByTankBusy(BatchMixer m) : base(m)
//        {
//            // Lógica Opción 2: FullBatch se libera al final

//        }
//        public override string StateName => $"Discharging Starved";
//        public override string SubStateName => $"{_mixer.CurrentWipTank?.Name ?? string.Empty} receiving from {_mixer.CurrentWipTank?.CurrentOwner?.Name ?? string.Empty}";
//        public override void Calculate()
//        {
//            _mixer.NetStarvedTimeInSeconds++;
//            _mixer.AccumulateTime(MixerStateCategory.StarvedByTankBusy);
//        }
//        public override void CheckTransitions()
//        {
//            if (_mixer.CurrentWipTank?.IsOwnedBy(_mixer) ?? true)
//            {
//                if (_mixer.CurrentWipTank?.InboundState is TankReceiving)
//                {
//                    _mixer.TransitionInBound(new MixerDischarging(_mixer));

//                    return;
//                }

//            }
//        }
//    }
//}
