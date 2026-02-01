namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Tanks
{

    //Este es el dañado
//    public class ProcessWipTankForLine2 : ProcessBaseTank, ILiveReportable, IRequestTansferTank
//    {

//        private IWIPManufactureOrder _NextOrder { get; set; } = null!;

//        public IWIPManufactureOrder CurrentOrder => _CurrentOrder;
//        private IWIPManufactureOrder _CurrentOrder { get; set; } = null!;

//        private Queue<TransferFromMixertoWIPOrder> TransfersOrdersFromMixers { get; set; } = new Queue<TransferFromMixertoWIPOrder>();
//        private TransferFromMixertoWIPOrder? CurrentTransferFromMixer { get; set; }
//        public List<ProcessMixer> InletMixers => InletEquipments.SelectMany(x => x.InletEquipments.OfType<ProcessMixer>().ToList()).ToList();
//        public List<ProcessContinuousSystem> InletSKIDS => InletEquipments.OfType<ProcessContinuousSystem>().ToList().ToList();
//        List<ManufaturingEquipment> ManufactureAttached => [.. InletSKIDS, .. InletMixers];

//        public ProcessPump? WIPTankPump => OutletPumps.FirstOrDefault();

//        public override void ValidateOutletInitialState(DateTime currentdate)
//        {
//            CurrentLevel = InitialLevel;

//            OutletState = new ProcessWipTankOutletInitializeTankState(this);


//        }

//        public WipTankForLineReport CurrentReport => CurrentOrder?.Report ?? new WipTankForLineReport();
//        public void ReceiveInitFromLineProductionOrder(FromLineToWipProductionOrder order)
//        {
//            var manufactures = ManufactureAttached.FirstOrDefault(x => x.EquipmentMaterials.Any(x => x.Material.Id == order.Material.Id));
//            if (manufactures != null)
//            {
//                OutletState = new ProcessWipTankOutletReviewInitInletStateTankState(this);
//                if (manufactures is ProcessContinuousSystem skid)
//                {
//                    if (_CurrentOrder == null)
//                    {
//                        _CurrentOrder = new WIPInletSKIDManufacturingOrder(this, order);

//                        order.ReceiveWipCanHandleMaterial(this);
//                        _CurrentOrder.AddMassProduced(CurrentLevel);



//                    }
//                    else
//                    {
//                        _NextOrder = new WIPInletSKIDManufacturingOrder(this, order);
//                        order.ReceiveWipCanHandleMaterial(this);
//                    }
//                }
//                else
//                {
//                    if (_CurrentOrder == null)
//                    {
//                        _CurrentOrder = new WIPInletMixerManufacturingOrder(this, order);

//                        order.ReceiveWipCanHandleMaterial(this);
//                        _CurrentOrder.AddMassProduced(CurrentLevel);

//                    }
//                    else
//                    {
//                        _NextOrder = new WIPInletMixerManufacturingOrder(this, order);
//                        order.ReceiveWipCanHandleMaterial(this);
//                    }
//                }

//            }


//        }
//        //outlet state Methods
//        public bool IsMustWashTank()
//        {
//            if (_CurrentOrder == null) return false;

//            if (LastMaterial == null)
//            {

//                LastMaterial = _CurrentOrder.Material;
//                return false;
//            }
//            if (_CurrentOrder.Material == null) return false;
//            if (_CurrentOrder.Material.Id == LastMaterial.Id) return false;

//            var washDef = WashoutTimes
//                .FirstOrDefault(x => x.ProductCategoryCurrent == _CurrentOrder.Material?.ProductCategory &&
//                                   x.ProductCategoryNext == LastMaterial.ProductCategory);


//            if (washDef != null)
//            {

//                return true;
//            }

//            return false;
//        }
//        public void SelectInletStateBasedOnManufacturingEquipment()
//        {
//            if (_CurrentOrder == null) return;

//            var manufactures = ManufactureAttached.FirstOrDefault(x =>
//                x.EquipmentMaterials.Any(m => m.Material.Id == _CurrentOrder.Material.Id));

//            if (manufactures is ProcessContinuousSystem)
//            {
//                InletSKIDS.ForEach(x => x.ReceiveManufactureOrderFromWIP(_CurrentOrder));
//                InletState = new TankInletManufacturingOrderReceivedSKIDState(this);
//            }
//            else
//            {
//                InletState = new TankInletWaitingForInletMixerState(this);
//            }
//        }
//        public Amount GetWashoutTime()
//        {
//            var result = new Amount(0, TimeUnits.Minute);
//            if (_CurrentOrder == null)
//            {
//                return result;
//            }
//            result = GetWashoutTime(LastMaterial, _CurrentOrder.Material);
//            LastMaterial = _CurrentOrder.Material;

//            return result;
//        }
//        private Amount GetWashoutTime(IMaterial current, IMaterial Next)
//        {
//            if (ManufactureAttached.Any(x => x.EquipmentMaterials.Any(x => x.Material.Id == Next.Id)))
//            {
//                if (current != null && Next != null)
//                {
//                    var washDef = WashoutTimes
//                    .FirstOrDefault(x => x.ProductCategoryCurrent == current.ProductCategory &&
//                                       x.ProductCategoryNext == Next.ProductCategory);
//                    if (washDef != null)
//                    {

//                        return washDef.LineWashoutTime;
//                    }
//                }
//            }



//            return new Amount(0, TimeUnits.Second);
//        }
//        public bool IsCurrentOrderRealesed()
//        {
//            if (_NextOrder != null)
//            {
//                _CurrentOrder = _NextOrder;
//                _NextOrder = null!;

//                return true;
//            }
//            _CurrentOrder = null!;

//            return true;
//        }
//        public bool IsCurrentOrderMassDeliveredCompleted()
//        {
//            if (_CurrentOrder != null)
//            {
//                if (_CurrentOrder.MassPendingToDeliver <= ZeroMass)
//                {

//                    return true;
//                }
//            }
//            return false;
//        }
//        ManufaturingEquipment? IdentifyManufacturingEquipment(IWIPManufactureOrder order)
//        {

//            var wiptanks = order.Line.InletPumps
//                .SelectMany(x => x.InletWipTanks).ToList();

//            var manufactures = wiptanks.SelectMany(x => x.ManufactureAttached)
//                .FirstOrDefault(x => x.EquipmentMaterials.Any(x => x.Material.Id == order.Material.Id));

//            return manufactures;
//        }
//        public bool IsNextOrderMaterialNeeded()
//        {

//            var currentOrderManufactureBy = IdentifyManufacturingEquipment(_CurrentOrder);
//            if (currentOrderManufactureBy is ProcessMixer)
//            {
//                if (_NextOrder != null)
//                {
//                    return IsNextMaterialNeededByMixer(_CurrentOrder, _NextOrder);
//                }
//            }
//            if (currentOrderManufactureBy is ProcessContinuousSystem)
//            {
//                return IsNextMaterialNeedBySKID(_CurrentOrder);
//            }



//            return false;
//        }
//        public override void CalculateOutletLevel()
//        {
//            base.CalculateOutletLevel();

//            if (_CurrentOrder != null)
//            {
//                _CurrentOrder.AddMassDelivered(MassDeliveredBySecond);
//                _CurrentOrder.AddRunTime();

//            }
//        }
//        public override void CalculateRunTime()
//        {
//            base.CalculateRunTime();
//            if (_CurrentOrder != null)
//            {

//                _CurrentOrder.AddRunTime();

//            }
//        }
//        //inlet state Methods inletMixers
//        public bool IsCurrentOrderMassProducedCompleted()
//        {
//            if (_CurrentOrder != null)
//            {
//                if (TransfersOrdersFromMixers.Count > 0 || CurrentTransferFromMixer != null || _CurrentOrder.ManufactureOrdersFromMixers.Count > 0)
//                {
//                    return false;
//                }
//                if (_CurrentOrder.IsPendingToProduceCompleted())
//                {
//                    return true;
//                }

//            }


//            return false;
//        }
//        public bool ReviewIfTransferCanInit()
//        {
//            if (TransfersOrdersFromMixers.Count == 0) return false;

//            CurrentTransferFromMixer = TransfersOrdersFromMixers.Dequeue();
//            CurrentTransferFromMixer.SourceMixer.ReceiveTransferOrderFromWIPToInit(CurrentTransferFromMixer);
//            return true;
//        }
//        public bool IsHighLevelDuringMixerTransfer()
//        {
//            if (CurrentTransferFromMixer != null && base.IsTankHigherThenHiLevel())
//            {
//                StartCriticalReport(
//                        this,
//                        "Starved by High Level",
//                        $"Tank {Name} is full."
//                    );
//                return true;
//            }
//            return false;
//        }
//        public bool IsTransferFinalized()
//        {
//            if (CurrentTransferFromMixer != null)
//            {
//                if (CurrentTransferFromMixer.IsTransferComplete)
//                {
//                    CurrentTransferFromMixer.SendMixerTransferIsFinished();
//                    CurrentTransferFromMixer = null;
//                    return true;
//                }
//            }
//            return false;
//        }
//        public bool IfTransferStarvedByHighLevelCanResume()
//        {
//            if (CurrentTransferFromMixer != null)
//            {
//                if (CurrentTransferFromMixer.CanTransferWithoutOverflowingDestination())
//                {
//                    EndCriticalReport();
//                    return true;
//                }
//            }
//            return false;
//        }
//        public void ReceiveTransferRequestFromMixer(TransferFromMixertoWIPOrder order)
//        {
//            TransfersOrdersFromMixers.Enqueue(order);
//        }
//        public void SetCurrentMassTransfered()
//        {

//            if (CurrentTransferFromMixer != null)
//            {
//                CurrentTransferFromMixer.SetCurrentMassTransfered();
//            }
//        }

//        //inlet state Methods SKIDS
//        public bool IsSKIDMustStop()
//        {
//            if (base.IsTankHigherThenHiLevel())
//            {
//                StopSkid();
//                return true;
//            }
//            return false;
//        }
//        public bool IsSKIDCanStart()
//        {
//            if (_CurrentOrder == null) return false;

//            if (IsTankInLoLevel())
//            {
//                StartSkid();
//                return true;
//            }
//            return false;
//        }
//        void StartSkid()
//        {
//            InletSKIDS.ForEach(x => x.Produce());

//        }
//        void StopSkid()
//        {
//            InletSKIDS.ForEach(x => x.Stop());

//        }
//        public bool IsSKIDWIPProducedCompleted()
//        {

//            if (_CurrentOrder.IsPendingToProduceCompleted())
//            {

//                InletSKIDS.ForEach(x => x.ReceiveTotalStop());
//                return true;
//            }



//            return false;
//        }
//        public void ReceiveProductFromSKID(Amount flow)
//        {
//            var mass = flow * OneSecond;
//            CurrentLevel += mass;
//            if (_CurrentOrder != null)
//            {
//                _CurrentOrder.AddMassProduced(mass);
//            }
//        }
//        public bool IsMaterialNeeded()
//        {
//            if (_CurrentOrder is null) return false;
//            var plan = GetMixerRankings(_CurrentOrder.Line, _CurrentOrder.Material);
//            if (plan == null) return false;

//            var currentTotalMass = _CurrentOrder.TotalMassStoragedOrProducing;

//            // El tiempo ocupado incluye esperas, lavados, batches y transferencias con sus paradas
//            var lineConsumption = plan.TotalMixerBatchCycleTime * 1.15 * _CurrentOrder.AverageOutletFlow;
//            var futureLevel = currentTotalMass - lineConsumption + plan.Recipe.BatchSize;

//            if (futureLevel <= Capacity)
//            {
//                StartNewOrder(_CurrentOrder, plan.Mixer);
//                return true;
//            }
//            return false;
//        }





//        public bool TryToStartNewOrder(IWIPManufactureOrder order, ManufaturingEquipment mixer, IEquipmentMaterial recipe)
//        {
//            if (order is null) return false;
//            var lastMixer = order.LastInOrder;

//            if (lastMixer is null)
//            {
//                StartNewOrder(order, mixer);
//                return true;
//            }
//            if (mixer.CurrentManufactureOrder == null)
//            {
//                return false;
//            }

//            if (((MixerManufactureOrder)mixer.CurrentManufactureOrder).NetBatchTime > recipe.TransferTime)
//            {
//                StartNewOrder(order, mixer);
//                return true;
//            }
//            // es mayor al tiempo que tardaremos en transferir, podemos encolar.

//            return false;
//        }
//        public record MixerSelectionPlan(
//          ManufaturingEquipment Mixer,
//          IEquipmentMaterial Recipe,
//          Amount TotalArrivalTime,
//          Amount PreparationTime
//      );
//        public void StartNewOrder2(IWIPManufactureOrder order, ManufaturingEquipment mixer)
//        {
//            mixer.ReceiveManufactureOrderFromWIP(order);
//        }
//        public void StartNewOrder(IWIPManufactureOrder order, ManufaturingEquipment mixer)
//        {
//            // 1. Notificar al Mixer (Encolar en el equipo)
//            mixer.ReceiveManufactureOrderFromWIP(order);

//            // 2. Notificar al Operario (Encolar en la Agenda Única)

//        }
//        public MixerRankingInfo? GetMixerRankingsFirstAttemp(ProcessLine Line, IMaterial Material)
//        {
//            var allCompatible = InletMixers.Where(x => x.IsMixerFree
//            && Line.PreferredManufacturer.Contains(x)
//            && x.EquipmentMaterials.Any(m => m.Material.Id == Material.Id)).Select(x => new
//            {
//                Mixer = x,
//                Recipe = x.EquipmentMaterials.Where(x => x.Material.Id == Material.Id).OrderBy(y => y.BatchCycleTime).FirstOrDefault(),

//            }).OrderBy(x => x.Recipe?.BatchCycleTime).ToList();

//            if (allCompatible.Any())
//            {

//                var FreeOption = allCompatible.First();

//            }

//            return null;
//        }
//        public MixerRankingInfo? GetMixerRankings(ProcessLine Line, IMaterial Material)
//        {
//            var allCompatible = InletMixers.Where(x => x.EquipmentMaterials.Any(m => m.Material.Id == Material.Id)).ToList();
//            List<MixerRankingInfo> rankings = new();

//            foreach (var mixer in allCompatible)
//            {
//                var recipe = mixer.EquipmentMaterials.First(m => m.Material.Id == Material.Id);
//                var op = mixer.InletOperators.FirstOrDefault();

//                // Disponibilidad: El máximo entre la Agenda del Operario y el Acero del Mixer
//                Amount opReady = op?.GetTotalWorkloadTime() ?? new Amount(0, TimeUnits.Minute);
//                Amount mixerReady = mixer.GetTimeUntilMixerIsReadyForNewOrder();

//                double currentTime = Math.Max(opReady.GetValue(TimeUnits.Minute), mixerReady.GetValue(TimeUnits.Minute));

//                // Proyección cronológica con paradas programadas

//                Amount washTime = GetWashoutTime(mixer, Material);
//                double mixerVailableTime = washTime.GetValue(TimeUnits.Minute);

//                currentTime += washTime.GetValue(TimeUnits.Minute);
//                if (op != null && washTime.Value > 0)
//                {
//                    var operatordelay = op.GetOperatorDownTimeDelay(new Amount(currentTime - washTime.Value, TimeUnits.Minute), washTime).Value;
//                    currentTime += operatordelay;
//                    mixerVailableTime += operatordelay;
//                }


//                double batchTime = recipe.BatchCycleTime.GetValue(TimeUnits.Minute);
//                mixerVailableTime += batchTime;

//                if (op != null)
//                {
//                    var operatordelay = op.GetOperatorDownTimeDelay(new Amount(currentTime - batchTime, TimeUnits.Minute), new Amount(batchTime, TimeUnits.Minute)).Value;
//                    currentTime += operatordelay;
//                    mixerVailableTime += operatordelay;
//                }



//                mixerVailableTime += recipe.TransferTime.GetValue(TimeUnits.Minute);
//                currentTime = currentTime == 0 ? 1 : currentTime;
//                double score = recipe.BatchSize.GetValue(MassUnits.KiloGram) / currentTime;
//                int preferenceRank = 1;
//                if (Line.PreferredManufacturer.Contains(mixer))
//                {
//                    preferenceRank = 2;

//                }

//                rankings.Add(new MixerRankingInfo(mixer, recipe, new Amount(mixerVailableTime, TimeUnits.Minute), score, preferenceRank));
//            }

//            rankings = rankings.OrderByDescending(x => x.PreferenceRank).ThenByDescending(x => x.Score).ToList();

//            var bestRanking = rankings.FirstOrDefault();
//            return bestRanking;
//        }






//        public record MixerRankingInfo(
//    ManufaturingEquipment Mixer,
//    IEquipmentMaterial Recipe,
//    Amount TotalMixerBatchCycleTime, // Tiempo hasta que la última gota entra al WIP
//    double Score,
//    int PreferenceRank = 0
//);

//        public Amount CalculateTotalOccupancy(ProcessMixer mixer, IEquipmentMaterial recipe, ProcessOperator? op)
//        {
//            Amount totalWait = new Amount(0, TimeUnits.Minute);

//            // 1. DETERMINAR PUNTO DE INICIO (El máximo entre disponibilidad de humano y máquina)
//            Amount opWait = op?.GetTotalWorkloadTime() ?? new Amount(0, TimeUnits.Minute);
//            Amount mixerWait = mixer.GetTimeUntilMixerIsReadyForNewOrder();

//            // Empezamos a contar desde que AMBOS están libres
//            totalWait = new Amount(Math.Max(opWait.GetValue(TimeUnits.Minute),
//                                            mixerWait.GetValue(TimeUnits.Minute)), TimeUnits.Minute);

//            // 2. FASE DE LAVADO (Si aplica)
//            Amount washTime = GetWashoutTime(mixer, recipe.Material);
//            if (washTime.Value > 0)
//            {
//                // Sumamos el lavado + cualquier almuerzo que ocurra durante el lavado
//                totalWait += washTime;
//                if (op != null)
//                    totalWait += op.GetOperatorDownTimeDelay(totalWait - washTime, washTime);
//            }

//            // 3. FASE DE BATCH (Preparación manual y proceso)
//            Amount batchTime = recipe.BatchCycleTime;
//            totalWait += batchTime;
//            if (op != null)
//                totalWait += op.GetOperatorDownTimeDelay(totalWait - batchTime, batchTime);

//            // 4. FASE DE TRANSFERENCIA (Vaciado al WIP)
//            Amount transferTime = recipe.TransferTime;
//            totalWait += transferTime;
//            // Nota: Si la transferencia no requiere al operario, no sumamos su DownTimeDelay aquí.

//            return totalWait;
//        }

//        Amount GetWashoutTime(ManufaturingEquipment mixer, IMaterial material)
//        {
//            Amount washoutTime = new Amount(0, TimeUnits.Minute);
//            if (mixer.LastMaterial != null)
//            {

//                var washoutDef = mixer.WashoutTimes
//                                .FirstOrDefault(x => x.ProductCategoryCurrent == mixer.LastMaterial.ProductCategory &&
//                                                   x.ProductCategoryNext == material.ProductCategory);

//                if (washoutDef != null)
//                {
//                    washoutTime = washoutDef.MixerWashoutTime;
//                }
//            }
//            return washoutTime;
//        }









//        public bool IsNextMaterialNeedBySKID(IWIPManufactureOrder _CurrentOrder)
//        {
//            var nextProductionOrder = _CurrentOrder.LineNextProductionOrder;
//            if (nextProductionOrder != null)
//            {
//                var wiptanks = nextProductionOrder.Line.InletPumps
//                    .SelectMany(x => x.InletWipTanks).ToList();

//                var manufactures = wiptanks.SelectMany(x => x.ManufactureAttached)
//                    .FirstOrDefault(x => x.EquipmentMaterials.Any(x => x.Material.Id == nextProductionOrder.Material.Id));
//                if (manufactures != null && manufactures is ProcessMixer mixer)
//                {
//                    wiptanks = wiptanks.Where(x => x.ManufactureAttached.Any(x =>
//                    x.EquipmentMaterials.Any(x => x.Material.Id == nextProductionOrder.Material.Id))).ToList();


//                    var result = IsNextProductionBySKIDNeededToStart(_CurrentOrder, nextProductionOrder, wiptanks);
//                    if (result)
//                    {
//                        return true;
//                    }
//                    return false;


//                }

//            }
//            return false;
//        }
//        public bool IsNextProductionBySKIDNeededToStart(IWIPManufactureOrder _CurrentOrder, FromLineToWipProductionOrder nextproductionorder, List<ProcessWipTankForLine> wiptanks)
//        {

//            if (nextproductionorder == null)
//                return false;

//            var productionPlan = GetMixerRankings(nextproductionorder.Line, nextproductionorder.Material);
//            if (productionPlan == null || productionPlan.Mixer is null || productionPlan.Recipe is null)
//                return false;

//            if (_CurrentOrder.AverageOutletFlow.Value <= 0)
//                return false;

//            // Tank wash time (due to material change in the WIP tank)
//            Amount tankWashTime = new Amount(0, TimeUnits.Minute);
//            Amount currentLevelTanks = new Amount(0, MassUnits.KiloGram);
//            foreach (var wipTank in wiptanks)
//            {
//                tankWashTime += GetWashoutTime(wipTank.LastMaterial, nextproductionorder.Material);
//                currentLevelTanks = wipTank.CurrentLevel > currentLevelTanks ? wipTank.CurrentLevel : currentLevelTanks;
//            }
//            Amount changeovetime = new Amount(0, TimeUnits.Minute);
//            if (_CurrentOrder.LineCurrentProductionOrder.Line.MustChangeFormat())
//            {
//                changeovetime = _CurrentOrder.LineCurrentProductionOrder.TimeToChangeSKU;
//            }



//            if (changeovetime > tankWashTime)
//            {
//                tankWashTime = changeovetime;
//            }


//            // Total time for mixer to complete next batch (includes mixer washout + production)
//            var mixerTotalTime = productionPlan.TotalMixerBatchCycleTime;

//            if (_CurrentOrder.TimeToEmptyMassInProcess.Value > 0)
//            {
//                var timetoEmptyVessel = currentLevelTanks / _CurrentOrder.AverageOutletFlow;
//                var timeUntilTankIsReady = _CurrentOrder.TimeToEmptyMassInProcess + tankWashTime + timetoEmptyVessel;

//                // Start mixer IF it will finish BEFORE or EXACTLY when tank is ready
//                if (timeUntilTankIsReady <= mixerTotalTime)
//                {
//                    //if (!_CurrentOrder.IsSendToLineCurrentOrderIsProduced)
//                    //{
//                    //    _CurrentOrder.SendToLineCurrentOrderIsProduced();
//                    //}

//                    return true;
//                }
//                // Time from NOW until tank is ready (empty + washed)

//            }
//            return false;
//        }
//        public bool IsNextMaterialNeededByMixer(IWIPManufactureOrder _CurrentOrder, IWIPManufactureOrder _NextOrder)
//        {
//            if (_NextOrder == null || _CurrentOrder == null)
//                return false;

//            var productionPlan = GetMixerRankings(_NextOrder.Line, _NextOrder.Material);
//            if (productionPlan == null || productionPlan.Mixer is null || productionPlan.Recipe is null)
//                return false;

//            if (_CurrentOrder.AverageOutletFlow.Value <= 0)
//                return false;

//            // Tank wash time (due to material change in the WIP tank)
//            Amount tankWashTime = new Amount(0, TimeUnits.Minute);
//            if (_NextOrder.ManufactureOrdersFromMixers.Count == 0)
//            {
//                tankWashTime = GetWashoutTime(_CurrentOrder.Material, _NextOrder.Material);

//            }
//            Amount changeovetime = new Amount(0, TimeUnits.Minute);
//            if (_CurrentOrder.LineCurrentProductionOrder.Line.MustChangeFormat())
//            {
//                changeovetime = _CurrentOrder.LineCurrentProductionOrder.TimeToChangeSKU;
//            }

//            if (changeovetime > tankWashTime)
//            {
//                tankWashTime = changeovetime;
//            }
//            // Total time for mixer to complete next batch (includes mixer washout + production)
//            var mixerTotalTime = productionPlan.TotalMixerBatchCycleTime;

//            if (_NextOrder.TotalMassProducingInMixer == ZeroMass)
//            {
//                var timeUntilTankIsReady = _CurrentOrder.TimeToEmptyMassInProcess + tankWashTime;

//                // Start mixer IF it will finish BEFORE or EXACTLY when tank is ready
//                if (timeUntilTankIsReady <= mixerTotalTime)
//                {
//                    return TryToStartNewOrder(_NextOrder, productionPlan.Mixer, productionPlan.Recipe);
//                }
//            }
//            else
//            {
//                var futureLevel = _NextOrder.TotalMassProducingInMixer + productionPlan.Recipe.BatchSize;
//                if (futureLevel <= Capacity)
//                {
//                    return TryToStartNewOrder(_NextOrder, productionPlan.Mixer, productionPlan.Recipe);
//                }
//            }


//            return false;
//        }

//        // Dentro de ProcessWipTankForLine.cs o en una clase de utilidad



//    }

}
