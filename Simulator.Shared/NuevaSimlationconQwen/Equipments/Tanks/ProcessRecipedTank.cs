namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Tanks
{
    //public class ProcessRecipedTank : ProcessBaseTankForRawMaterial, IRequestTansferTank
    //{

    //    public ITankManufactureOrder CurrentTankManufactureOrder { get; set; } = null!;
    //    public Queue<TransferFromMixertoWIPOrder> TransfersOrdersFromMixers { get; set; } = new Queue<TransferFromMixertoWIPOrder>();
    //    private TransferFromMixertoWIPOrder? CurrentTransferFromMixer { get; set; } = null!;
    //    public List<ProcessPump> InletPumps => InletEquipments.OfType<ProcessPump>().ToList();
    //    public List<ProcessMixer> InletMixers => InletPumps.SelectMany(x => x.InletManufacturingEquipments.OfType<ProcessMixer>()).ToList();



    //    public override void ValidateOutletInitialState(DateTime currentdate)
    //    {
    //        CurrentLevel = InitialLevel;
    //        OutletState = new TankOutletInitializeTankState(this);
    //        InletState = new ProcessRecipedTankInletWaitingForInletMixerState(this);
    //        CurrentTankManufactureOrder = new RecipedTankManufactureOrder(this, Material!);

    //    }
    //    public void ReceiveTransferRequestFromMixer(TransferFromMixertoWIPOrder order)
    //    {
    //        TransfersOrdersFromMixers.Enqueue(order);
    //    }
    //    public bool IsMaterialNeeded()
    //    {
    //        // 1. Verificación de orden activa
    //        if (CurrentTankManufactureOrder is null) return false;

    //        // 2. Obtención directa del Plan desde el Selector
    //        // Reemplazamos el método intermedio por la llamada directa
    //        var plan = SelectCandidateMixers(CurrentTankManufactureOrder.Material);

    //        if (plan.Mixer is null || plan.Recipe is null)
    //            return false;

    //        // 3. Cálculo de Masa Total (Inventario + Tránsito + Cola)
    //        var currentTotalMass = CurrentTankManufactureOrder.TotalMassStoragedOrProducing;

    //        // 4. Caso de Emergencia: Tanque vacío
    //        if (currentTotalMass.Value == 0)
    //        {
    //            return TryToStartNewOrder(plan.Mixer, plan.Recipe);
    //        }

    //        // 5. Proyección de Nivel Futuro (Balance de Masa)
    //        // Sumamos la masa actual y el tamaño del nuevo lote potencial
    //        var futureLevel = currentTotalMass + plan.Recipe.BatchSize;

    //        // 6. Descuento por Consumo Proyectado (Salida del WIP)


    //        // 7. Sumar Transferencias Activas (Seguridad adicional)
    //        if (CurrentTransferFromMixer != null)
    //        {
    //            futureLevel += CurrentTransferFromMixer.PendingToReceive;
    //        }

    //        // 8. Validación de Capacidad Final
    //        // Solo pedimos el lote si el nivel proyectado no desborda el tanque
    //        if (futureLevel <= Capacity)
    //        {
    //            return TryToStartNewOrder(plan.Mixer, plan.Recipe);
    //        }

    //        return false;
    //    }
    //    public MixerSelectionPlan SelectCandidateMixers(IMaterial Material)
    //    {
    //        if (Material is null) return new MixerSelectionPlan(null!, null!, new Amount(0, TimeUnits.Minute), new Amount(0, TimeUnits.Minute));

    //        var allCompatible = InletMixers
    //            .Where(x => x.EquipmentMaterials.Any(m => m.Material.Id == Material.Id))
    //            .ToList();

    //        if (!allCompatible.Any()) return new MixerSelectionPlan(null!, null!, new Amount(0, TimeUnits.Minute), new Amount(0, TimeUnits.Minute));

    //        // --- NIVEL 1: PREFERIDOS LIBRES ---


    //        // --- NIVEL 2: RANKING POR PRODUCTIVIDAD (ETA REAL) ---
    //        var totalRankings = allCompatible.Select(mixer =>
    //        {
    //            var recipe = mixer.EquipmentMaterials.First(m => m.Material.Id == Material.Id);
    //            Amount totalMinutesToGetFree = new Amount(0, TimeUnits.Minute);

    //            // 1. Interferencia del Operario
    //            var op = mixer.InletEquipments.OfType<ProcessOperator>().FirstOrDefault();
    //            if (op != null)
    //            {
    //                if (op.OcuppiedBy != null && op.OcuppiedBy != mixer)
    //                {
    //                    if (op.OcuppiedBy is ProcessMixer otherMixer)
    //                        totalMinutesToGetFree += otherMixer.CurrentManufactureOrder?.PendingBatchTime ?? new Amount(0, TimeUnits.Minute);
    //                }
    //                else if (op.OcuppiedBy == null && op.OutletState is FeederPlannedDownTimeState)
    //                {
    //                    var otherMixers = op.OutletMixers.Where(x => x != mixer).ToList();
    //                    otherMixers.ForEach(x =>
    //                    {
    //                        if (x.CurrentManufactureOrder != null)
    //                            totalMinutesToGetFree += x.CurrentManufactureOrder.PendingBatchTime ?? new Amount(0, TimeUnits.Minute);
    //                    });
    //                }
    //            }

    //            // 2. Estado actual del Mixer (Producción o Transferencia)
    //            if (mixer.CurrentManufactureOrder != null)
    //            {
    //                totalMinutesToGetFree += ((MixerManufactureOrder)mixer.CurrentManufactureOrder).PendingBatchTime;
    //                totalMinutesToGetFree += ((MixerManufactureOrder)mixer.CurrentManufactureOrder).TransferTime;
    //            }
    //            else if (mixer.CurrentTransferRequest != null)
    //            {
    //                totalMinutesToGetFree += mixer.CurrentTransferRequest.PendingTransferTime;
    //            }

    //            // 3. Órdenes en Cola
    //            foreach (var nextMO in mixer.ManufacturingOrders)
    //            {
    //                totalMinutesToGetFree += GetWashoutTime(mixer, nextMO.Material);
    //                totalMinutesToGetFree += ((MixerManufactureOrder)nextMO).BatchCycleTime;
    //                totalMinutesToGetFree += ((MixerManufactureOrder)nextMO).TransferTime;
    //            }

    //            // 4. Preparación de la nueva orden
    //            totalMinutesToGetFree += GetWashoutTime(mixer, Material);

    //            // 5. Retraso por Almuerzos/Breaks

    //            Amount opDelay = op?.GetOperatorDownTimeDelay(totalMinutesToGetFree, recipe.BatchCycleTime) ?? new Amount(0, TimeUnits.Minute);

    //            Amount finalPrepTime = totalMinutesToGetFree + recipe.BatchCycleTime + opDelay;
    //            Amount finalArrival = finalPrepTime + recipe.TransferTime;

    //            // Score de productividad real (Kg/min)
    //            double score = recipe.BatchSize.GetValue(MassUnits.KiloGram) / finalArrival.GetValue(TimeUnits.Minute);

    //            return new
    //            {
    //                Mixer = mixer,
    //                Recipe = recipe,
    //                Score = score,
    //                ETA = finalArrival,
    //                Prep = finalPrepTime
    //            };
    //        })
    //        .OrderByDescending(x => x.Score)
    //        .ToList();
    //        var bestRanked = totalRankings.First();
    //        return new MixerSelectionPlan(bestRanked.Mixer, bestRanked.Recipe, bestRanked.ETA, bestRanked.Prep);
    //    }

    //    public bool TryToStartNewOrder(ManufaturingEquipment mixer, IEquipmentMaterial recipe)
    //    {
    //        var lastMixer = CurrentTankManufactureOrder.LastInOrder;
    //        if (lastMixer == null)
    //        {
    //            StartNewOrder(mixer);
    //            return true;
    //        }

    //        if (lastMixer.ManufaturingEquipment.CurrentManufactureOrder.CurrentBatchTime > recipe.TransferTime)
    //        {
    //            StartNewOrder(mixer);
    //            return true;
    //        }

    //        return false;


    //    }
    //    public void StartNewOrder(ManufaturingEquipment mixer)
    //    {

    //        mixer.ReceiveManufactureOrderFromWIP(CurrentTankManufactureOrder);
    //    }
    //    public bool ReviewIfTransferCanInit()
    //    {
    //        if (TransfersOrdersFromMixers.Count == 0) return false;

    //        CurrentTransferFromMixer = TransfersOrdersFromMixers.Dequeue();
    //        CurrentTransferFromMixer.SourceMixer.ReceiveTransferOrderFromWIPToInit(CurrentTransferFromMixer);
    //        return true;


    //    }
    //    public bool IsTransferFinalized()
    //    {
    //        if (CurrentTransferFromMixer != null)
    //        {
    //            if (CurrentTransferFromMixer.IsTransferComplete)
    //            {
    //                CurrentTransferFromMixer.SendMixerTransferIsFinished();
    //                CurrentTransferFromMixer = null;
    //                return true;
    //            }
    //        }
    //        return false;
    //    }


    //    public void SetCurrentMassTransfered()
    //    {
    //        if (CurrentTransferFromMixer != null)
    //        {
    //            Amount massTransfered = CurrentTransferFromMixer.TransferFlow * OneSecond;
    //            if (CurrentTransferFromMixer.PendingToReceive <= massTransfered)
    //            {
    //                massTransfered = CurrentTransferFromMixer.PendingToReceive;
    //            }
    //            CurrentLevel += massTransfered;
    //            CurrentTransferFromMixer.MassReceived += massTransfered;
    //            CurrentTransferFromMixer.SourceMixer.ReceiveReportOfMassDelivered(massTransfered);
    //        }
    //    }

    //    public bool ReviewIfTransferCanReinit()
    //    {
    //        if (CurrentTransferFromMixer != null)
    //        {
    //            if (Capacity - CurrentLevel >= CurrentTransferFromMixer.PendingToReceive)
    //            {
    //                ReportReinitTransferToMixer();
    //                return true;
    //            }

    //            return false;
    //        }
    //        return false;
    //    }
    //    public void ReportReinitTransferToMixer()
    //    {
    //        if (CurrentTransferFromMixer != null)
    //        {
    //            //CurrentTransferFromMixer.SourceMixer.ReceiveTransferReInitStarvedFromWIP();
    //        }
    //    }
    //    Amount GetWashoutTime(ManufaturingEquipment mixer, IMaterial material)
    //    {
    //        Amount washoutTime = new Amount(0, TimeUnits.Minute);
    //        if (mixer.LastMaterial != null)
    //        {

    //            var washoutDef = mixer.WashoutTimes
    //                            .FirstOrDefault(x => x.ProductCategoryCurrent == mixer.LastMaterial.ProductCategory &&
    //                                               x.ProductCategoryNext == material.ProductCategory);

    //            if (washoutDef != null)
    //            {
    //                washoutTime = washoutDef.MixerWashoutTime;
    //            }
    //        }
    //        return washoutTime;
    //    }
    //    //(Amount TotalBatchTime, Amount BatchTime, ManufaturingEquipment SelectedMixer, IEquipmentMaterial SelectedRecipe)
    //    //  GetTimeToProduceProduct(IMaterial? Material)
    //    //{
    //    //    if (Material == null) return new(null!, null!, null!, null!);
    //    //    var selectedMixerMaterial = SelectCandidateMixers(Material);
    //    //    if (selectedMixerMaterial.MixerCandidate is null)
    //    //        return (new Amount(0, TimeUnits.Minute), new Amount(0, TimeUnits.Minute), null!, null!);

    //    //    //Added ten minutes by Unknow delays
    //    //    Amount TenMinutes = new Amount(10, TimeUnits.Minute);

    //    //    var washoutTime = GetWashoutTime(selectedMixerMaterial.MixerCandidate, Material);
    //    //    var batchTime = selectedMixerMaterial.Recipe.BatchCycleTime;
    //    //    var totalBatchtTime = batchTime + washoutTime;
    //    //    var transferTime = selectedMixerMaterial.Recipe.TransferTime;
    //    //    var totalTime = washoutTime + transferTime + batchTime;

    //    //    return (totalTime, totalBatchtTime, selectedMixerMaterial.MixerCandidate, selectedMixerMaterial.Recipe);
    //    //}
    //    //(ManufaturingEquipment MixerCandidate, IEquipmentMaterial Recipe) SelectCandidateMixers(IMaterial Material)
    //    //{
    //    //    if (Material is null) return (null!, null!);
    //    //    IEquipmentMaterial materialFromMixer = null!;
    //    //    // 1. Preferidos libres

    //    //    var mixers = InletMixers;
    //    //    // 2. Todos los mezcladores que producen el material
    //    //    var allMixersThatProduceMaterial = mixers
    //    //        .Where(x => x.EquipmentMaterials.Any(m => m.Material.Id == Material.Id))
    //    //        .ToList();

    //    //    if (allMixersThatProduceMaterial.Count == 0) return (null!, null!);

    //    //    // 3. Si alguno está libre → devolver el primero

    //    //    var freeMixers = allMixersThatProduceMaterial.Where(x => x.CurrentManufactureOrder == null).ToList();
    //    //    var freeMixer = freeMixers.RandomElement(); // ← ¡Así de simple!
    //    //    if (freeMixer != null)
    //    //    {
    //    //        materialFromMixer = SelectMaterialFromMixer(freeMixer, Material);
    //    //        return (freeMixer, materialFromMixer);
    //    //    }

    //    //    // 4. Todos ocupados → buscar el primero que pueda encolar (batchTime > transferTime)
    //    //    var orderedMixers = allMixersThatProduceMaterial
    //    //        .OrderBy(x => x.CurrentManufactureOrder.CurrentBatchTime.GetValue(TimeUnits.Minute))
    //    //        .ToList();

    //    //    foreach (var candidate in orderedMixers)
    //    //    {
    //    //        materialFromMixer = SelectMaterialFromMixer(candidate, Material);
    //    //        // Asegúrate de que materialFromMixer no sea null
    //    //        if (materialFromMixer != null &&
    //    //            candidate.CurrentManufactureOrder.CurrentBatchTime > materialFromMixer.TransferTime)
    //    //        {

    //    //            return (candidate, materialFromMixer); // ¡Encontramos uno que puede encolar!
    //    //        }
    //    //    }
    //    //    var FirstMixer = orderedMixers.FirstOrDefault();
    //    //    if (FirstMixer != null)
    //    //    {
    //    //        materialFromMixer = SelectMaterialFromMixer(FirstMixer!, Material);
    //    //        // 5. Si ninguno puede encolar → devolver el que termine primero
    //    //        return (FirstMixer, materialFromMixer);
    //    //    }
    //    //    materialFromMixer = SelectMaterialFromMixer(FirstMixer!, Material);
    //    //    // 5. Si ninguno puede encolar → devolver el que termine primero
    //    //    return (null!, null!);
    //    //}
    //    IEquipmentMaterial SelectMaterialFromMixer(ManufaturingEquipment mixer, IMaterial material)
    //    {

    //        var materialFoundFromMixer = mixer.EquipmentMaterials.FirstOrDefault(x => x.Material.Id == material.Id);

    //        return materialFoundFromMixer!;
    //    }
    //    public bool IsHighLevelDuringMixerTransfer()
    //    {
    //        if (CurrentTransferFromMixer != null && base.IsTankHigherThenHiLevel())
    //        {
    //            StartCriticalReport(
    //                    this,
    //                    "Starved by High Level",
    //                    $"Tank {Name} is full."
    //                );
    //            return true;
    //        }
    //        return false;
    //    }
    //    public bool IfTransferStarvedByHighLevelCanResume()
    //    {
    //        if (CurrentTransferFromMixer != null)
    //        {
    //            if (CurrentTransferFromMixer.CanTransferWithoutOverflowingDestination())
    //            {
    //                EndCriticalReport();
    //                return true;
    //            }
    //        }
    //        return false;
    //    }
    //}


}
