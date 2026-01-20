using Simulator.Shared.Enums.HCEnums.Enums;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Lines;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Operators;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Pumps;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Skids;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Tanks.States;
using Simulator.Shared.NuevaSimlationconQwen.ManufacturingOrders;
using Simulator.Shared.NuevaSimlationconQwen.Materials;
using Simulator.Shared.NuevaSimlationconQwen.Reports;

namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Tanks
{
    public class ProcessWipTankForLine : ProcessBaseTank, ILiveReportable, IRequestTansferTank
    {

        private IWIPManufactureOrder _NextOrder { get; set; } = null!;

        public IWIPManufactureOrder CurrentOrder => _CurrentOrder;
        private IWIPManufactureOrder _CurrentOrder { get; set; } = null!;

        private Queue<TransferFromMixertoWIPOrder> TransfersOrdersFromMixers { get; set; } = new Queue<TransferFromMixertoWIPOrder>();
        private TransferFromMixertoWIPOrder? CurrentTransferFromMixer { get; set; }
        public List<ProcessMixer> InletMixers => InletEquipments.SelectMany(x => x.InletEquipments.OfType<ProcessMixer>().ToList()).ToList();
        public List<ProcessContinuousSystem> InletSKIDS => InletEquipments.OfType<ProcessContinuousSystem>().ToList().ToList();
        List<ManufaturingEquipment> ManufactureAttached => [.. InletSKIDS, .. InletMixers];

        public ProcessPump? WIPTankPump => OutletPumps.FirstOrDefault();

        public override void ValidateOutletInitialState(DateTime currentdate)
        {
            CurrentLevel = InitialLevel;

            OutletState = new ProcessWipTankOutletInitializeTankState(this);


        }

        public WipTankForLineReport CurrentReport => CurrentOrder?.Report ?? new WipTankForLineReport();
        public void ReceiveInitFromLineProductionOrder(FromLineToWipProductionOrder order)
        {
            var manufactures = ManufactureAttached.FirstOrDefault(x => x.EquipmentMaterials.Any(x => x.Material.Id == order.Material.Id));
            if (manufactures != null)
            {
                OutletState = new ProcessWipTankOutletReviewInitInletStateTankState(this);
                if (manufactures is ProcessContinuousSystem skid)
                {
                    if (_CurrentOrder == null)
                    {
                        _CurrentOrder = new WIPInletSKIDManufacturingOrder(this, order);

                        order.ReceiveWipCanHandleMaterial(this);
                        _CurrentOrder.AddMassProduced(CurrentLevel);



                    }
                    else
                    {
                        _NextOrder = new WIPInletSKIDManufacturingOrder(this, order);
                        order.ReceiveWipCanHandleMaterial(this);
                    }
                }
                else
                {
                    if (_CurrentOrder == null)
                    {
                        _CurrentOrder = new WIPInletMixerManufacturingOrder(this, order);

                        order.ReceiveWipCanHandleMaterial(this);
                        _CurrentOrder.AddMassProduced(CurrentLevel);

                    }
                    else
                    {
                        _NextOrder = new WIPInletMixerManufacturingOrder(this, order);
                        order.ReceiveWipCanHandleMaterial(this);
                    }
                }

            }


        }
        //outlet state Methods
        public bool IsMustWashTank()
        {
            if (_CurrentOrder == null) return false;

            if (LastMaterial == null)
            {

                LastMaterial = _CurrentOrder.Material;
                return false;
            }
            if (_CurrentOrder.Material == null) return false;
            if (_CurrentOrder.Material.Id == LastMaterial.Id) return false;

            var washDef = WashoutTimes
                .FirstOrDefault(x => x.ProductCategoryCurrent == _CurrentOrder.Material?.ProductCategory &&
                                   x.ProductCategoryNext == LastMaterial.ProductCategory);


            if (washDef != null)
            {

                return true;
            }

            return false;
        }
        public void SelectInletStateBasedOnManufacturingEquipment()
        {
            if (_CurrentOrder == null) return;

            var manufactures = ManufactureAttached.FirstOrDefault(x =>
                x.EquipmentMaterials.Any(m => m.Material.Id == _CurrentOrder.Material.Id));

            if (manufactures is ProcessContinuousSystem)
            {
                InletSKIDS.ForEach(x => x.ReceiveManufactureOrderFromWIP(_CurrentOrder));
                InletState = new TankInletManufacturingOrderReceivedSKIDState(this);
            }
            else
            {
                InletState = new TankInletWaitingForInletMixerState(this);
            }
        }
        public Amount GetWashoutTime()
        {
            var result = new Amount(0, TimeUnits.Minute);
            if (_CurrentOrder == null)
            {
                return result;
            }
            result = GetWashoutTime(LastMaterial, _CurrentOrder.Material);
            LastMaterial = _CurrentOrder.Material;

            return result;
        }
        private Amount GetWashoutTime(IMaterial current, IMaterial Next)
        {
            if (ManufactureAttached.Any(x => x.EquipmentMaterials.Any(x => x.Material.Id == Next.Id)))
            {
                if (current != null && Next != null)
                {
                    var washDef = WashoutTimes
                    .FirstOrDefault(x => x.ProductCategoryCurrent == current.ProductCategory &&
                                       x.ProductCategoryNext == Next.ProductCategory);
                    if (washDef != null)
                    {

                        return washDef.LineWashoutTime;
                    }
                }
            }



            return new Amount(0, TimeUnits.Second);
        }
        public bool IsCurrentOrderRealesed()
        {
            if (_NextOrder != null)
            {
                _CurrentOrder = _NextOrder;
                _NextOrder = null!;

                return true;
            }
            _CurrentOrder = null!;

            return true;
        }
        public bool IsCurrentOrderMassDeliveredCompleted()
        {
            if (_CurrentOrder != null)
            {
                if (_CurrentOrder.MassPendingToDeliver <= ZeroMass)
                {

                    return true;
                }
            }
            return false;
        }
        ManufaturingEquipment? IdentifyManufacturingEquipment(IWIPManufactureOrder order)
        {

            var wiptanks = order.Line.InletPumps
                .SelectMany(x => x.InletWipTanks).ToList();

            var manufactures = wiptanks.SelectMany(x => x.ManufactureAttached)
                .FirstOrDefault(x => x.EquipmentMaterials.Any(x => x.Material.Id == order.Material.Id));

            return manufactures;
        }
        public bool IsNextOrderMaterialNeeded()
        {

            var currentOrderManufactureBy = IdentifyManufacturingEquipment(_CurrentOrder);
            if (currentOrderManufactureBy is ProcessMixer)
            {
                if (_NextOrder != null)
                {
                    return IsNextMaterialNeededByMixer(_CurrentOrder, _NextOrder);
                }
            }
            if (currentOrderManufactureBy is ProcessContinuousSystem)
            {
                return IsNextMaterialNeedBySKID(_CurrentOrder);
            }



            return false;
        }
        public override void CalculateOutletLevel()
        {
            base.CalculateOutletLevel();

            if (_CurrentOrder != null)
            {
                _CurrentOrder.AddMassDelivered(MassDeliveredBySecond);
                _CurrentOrder.AddRunTime();

            }
        }
        public override void CalculateRunTime()
        {
            base.CalculateRunTime();
            if (_CurrentOrder != null)
            {

                _CurrentOrder.AddRunTime();

            }
        }
        //inlet state Methods inletMixers
        public bool IsCurrentOrderMassProducedCompleted()
        {
            if (_CurrentOrder != null)
            {
                if (TransfersOrdersFromMixers.Count > 0 || CurrentTransferFromMixer != null || _CurrentOrder.ManufactureOrdersFromMixers.Count > 0)
                {
                    return false;
                }
                if (_CurrentOrder.IsPendingToProduceCompleted())
                {
                    return true;
                }

            }


            return false;
        }
        public bool ReviewIfTransferCanInit()
        {
            if (TransfersOrdersFromMixers.Count == 0) return false;

            CurrentTransferFromMixer = TransfersOrdersFromMixers.Dequeue();
            CurrentTransferFromMixer.SourceMixer.ReceiveTransferOrderFromWIPToInit(CurrentTransferFromMixer);
            return true;
        }
        public bool IsHighLevelDuringMixerTransfer()
        {
            if (CurrentTransferFromMixer != null && base.IsTankHigherThenHiLevel())
            {
                StartCriticalReport(
                        this,
                        "Starved by High Level",
                        $"Tank {Name} is full."
                    );
                return true;
            }
            return false;
        }
        public bool IsTransferFinalized()
        {
            if (CurrentTransferFromMixer != null)
            {
                if (CurrentTransferFromMixer.IsTransferComplete)
                {
                    CurrentTransferFromMixer.SendMixerTransferIsFinished();
                    CurrentTransferFromMixer = null;
                    return true;
                }
            }
            return false;
        }
        public bool IfTransferStarvedByHighLevelCanResume()
        {
            if (CurrentTransferFromMixer != null)
            {
                if (CurrentTransferFromMixer.CanTransferWithoutOverflowingDestination())
                {
                    EndCriticalReport();
                    return true;
                }
            }
            return false;
        }
        public void ReceiveTransferRequestFromMixer(TransferFromMixertoWIPOrder order)
        {
            TransfersOrdersFromMixers.Enqueue(order);
        }
        public void SetCurrentMassTransfered()
        {

            if (CurrentTransferFromMixer != null)
            {
                CurrentTransferFromMixer.SetCurrentMassTransfered();
            }
        }

        //inlet state Methods SKIDS
        public bool IsSKIDMustStop()
        {
            if (base.IsTankHigherThenHiLevel())
            {
                StopSkid();
                return true;
            }
            return false;
        }
        public bool IsSKIDCanStart()
        {
            if (_CurrentOrder == null) return false;

            if (IsTankInLoLevel())
            {
                StartSkid();
                return true;
            }
            return false;
        }
        void StartSkid()
        {
            InletSKIDS.ForEach(x => x.Produce());

        }
        void StopSkid()
        {
            InletSKIDS.ForEach(x => x.Stop());

        }
        public bool IsSKIDWIPProducedCompleted()
        {

            if (_CurrentOrder.IsPendingToProduceCompleted())
            {

                InletSKIDS.ForEach(x => x.ReceiveTotalStop());
                return true;
            }



            return false;
        }
        public void ReceiveProductFromSKID(Amount flow)
        {
            var mass = flow * OneSecond;
            CurrentLevel += mass;
            if (_CurrentOrder != null)
            {
                _CurrentOrder.AddMassProduced(mass);
            }
        }
        public bool IsMaterialNeeded()
        {
            // 1. Verificación de orden activa
            if (_CurrentOrder is null) return false;

            // 2. Obtención directa del Plan desde el Selector
            // Reemplazamos el método intermedio por la llamada directa
            var plan = SelectCandidateMixers(_CurrentOrder.Line, _CurrentOrder.Material);

            if (plan.Mixer is null || plan.Recipe is null)
                return false;

            // 3. Cálculo de Masa Total (Inventario + Tránsito + Cola)
            var currentTotalMass = _CurrentOrder.TotalMassStoragedOrProducing;

            // 4. Caso de Emergencia: Tanque vacío
            if (currentTotalMass.Value == 0)
            {
                return TryToStartNewOrder(_CurrentOrder, plan.Mixer, plan.Recipe);
            }

            // 5. Proyección de Nivel Futuro (Balance de Masa)
            // Sumamos la masa actual y el tamaño del nuevo lote potencial
            var futureLevel = currentTotalMass + plan.Recipe.BatchSize;

            // 6. Descuento por Consumo Proyectado (Salida del WIP)
            if (_CurrentOrder.AverageOutletFlow.Value > 0)
            {
                // Usamos el 'TotalArrivalTime' calculado honestamente por el selector
                // (Incluye cola, interferencia del operario y paradas programadas)
                var effectiveConsumptionTime = plan.TotalArrivalTime;

                // Aplicamos tu factor de seguridad del 15% (1.15) 
                // para evitar el nivel crítico visto en WIPa#10
                var massOutletDuringProcess = effectiveConsumptionTime * 1.15 * _CurrentOrder.AverageOutletFlow;

                futureLevel -= massOutletDuringProcess;
            }

            // 7. Sumar Transferencias Activas (Seguridad adicional)
            if (CurrentTransferFromMixer != null)
            {
                futureLevel += CurrentTransferFromMixer.PendingToReceive;
            }

            // 8. Validación de Capacidad Final
            // Solo pedimos el lote si el nivel proyectado no desborda el tanque
            if (futureLevel <= Capacity)
            {
                return TryToStartNewOrder(_CurrentOrder, plan.Mixer, plan.Recipe);
            }

            return false;
        }


        public bool TryToStartNewOrder(IWIPManufactureOrder order, ManufaturingEquipment mixer, IEquipmentMaterial recipe)
        {
            if (order is null) return false;
            var lastMixer = order.LastInOrder;

            if (lastMixer is null)
            {
                StartNewOrder(order, mixer);
                return true;
            }


            // es mayor al tiempo que tardaremos en transferir, podemos encolar.
            if (lastMixer.ManufaturingEquipment.CurrentManufactureOrder.CurrentBatchTime > recipe.TransferTime)
            {
                StartNewOrder(order, mixer);
                return true;
            }
            return false;
        }
        public record MixerSelectionPlan(
          ManufaturingEquipment Mixer,
          IEquipmentMaterial Recipe,
          Amount TotalArrivalTime,
          Amount PreparationTime
      );
        public void StartNewOrder(IWIPManufactureOrder order, ManufaturingEquipment mixer)
        {
            mixer.ReceiveManufactureOrderFromWIP(order);
        }
        


        public MixerSelectionPlan SelectCandidateMixers(ProcessLine Line, IMaterial Material)
        {
            if (Material is null) return new MixerSelectionPlan(null!, null!, new Amount(0, TimeUnits.Minute), new Amount(0, TimeUnits.Minute));

            var allCompatible = InletMixers
                .Where(x => x.EquipmentMaterials.Any(m => m.Material.Id == Material.Id))
                .ToList();

            if (!allCompatible.Any()) return new MixerSelectionPlan(null!, null!, new Amount(0, TimeUnits.Minute), new Amount(0, TimeUnits.Minute));

            // --- NIVEL 1: PREFERIDOS LIBRES ---
            var preferredCandidates = allCompatible
                 .Where(m => Line.PreferredManufacturer.Contains(m) && m.GetManufactureOrderCount() == 0)
                 .ToList();

            if (preferredCandidates.Any())
            {
                var mixer = preferredCandidates.First();
                var recipe = mixer.EquipmentMaterials.First(x => x.Material.Id == Material.Id);

                // Calculamos tiempos para un mixer libre
                Amount washTime = GetWashoutTime(mixer, Material);
                Amount batchTime = recipe.BatchCycleTime;
                Amount opDelay = GetOperatorDownTimeDelay(mixer, new Amount(0, TimeUnits.Minute), washTime + batchTime);

                Amount prepTime = washTime + batchTime + opDelay;
                Amount arrivalTime = prepTime + recipe.TransferTime;

                return new MixerSelectionPlan(mixer, recipe, arrivalTime, prepTime);
            }

            // --- NIVEL 2: RANKING POR PRODUCTIVIDAD (ETA REAL) ---
            var totalRankings = allCompatible.Select(mixer =>
            {
                var recipe = mixer.EquipmentMaterials.First(m => m.Material.Id == Material.Id);
                Amount totalMinutesToGetFree = new Amount(0, TimeUnits.Minute);

                // 1. Interferencia del Operario
                var op = mixer.InletEquipments.OfType<ProcessOperator>().FirstOrDefault();
                if (op != null)
                {
                    if (op.OcuppiedBy != null && op.OcuppiedBy != mixer)
                    {
                        if (op.OcuppiedBy is ProcessMixer otherMixer)
                            totalMinutesToGetFree += otherMixer.CurrentManufactureOrder?.PendingBatchTime ?? new Amount(0, TimeUnits.Minute);
                    }
                    else if (op.OcuppiedBy == null && op.OutletState is FeederPlannedDownTimeState)
                    {
                        var otherMixers = op.OutletMixers.Where(x => x != mixer).ToList();
                        otherMixers.ForEach(x =>
                        {
                            if (x.CurrentManufactureOrder != null)
                                totalMinutesToGetFree += x.CurrentManufactureOrder.PendingBatchTime ?? new Amount(0, TimeUnits.Minute);
                        });
                    }
                }

                // 2. Estado actual del Mixer (Producción o Transferencia)
                if (mixer.CurrentManufactureOrder != null)
                {
                    totalMinutesToGetFree += ((MixerManufactureOrder)mixer.CurrentManufactureOrder).PendingBatchTime;
                    totalMinutesToGetFree += ((MixerManufactureOrder)mixer.CurrentManufactureOrder).TransferTime;
                }
                else if (mixer.CurrentTransferRequest != null)
                {
                    totalMinutesToGetFree += mixer.CurrentTransferRequest.PendingTransferTime;
                }

                // 3. Órdenes en Cola
                foreach (var nextMO in mixer.ManufacturingOrders)
                {
                    totalMinutesToGetFree += GetWashoutTime(mixer, nextMO.Material);
                    totalMinutesToGetFree += ((MixerManufactureOrder)nextMO).BatchTime;
                    totalMinutesToGetFree += ((MixerManufactureOrder)nextMO).TransferTime;
                }

                // 4. Preparación de la nueva orden
                totalMinutesToGetFree += GetWashoutTime(mixer, Material);

                // 5. Retraso por Almuerzos/Breaks
                Amount opDelay = GetOperatorDownTimeDelay(mixer, totalMinutesToGetFree, recipe.BatchCycleTime);

                Amount finalPrepTime = totalMinutesToGetFree + recipe.BatchCycleTime + opDelay;
                Amount finalArrival = finalPrepTime + recipe.TransferTime;

                // Score de productividad real (Kg/min)
                double score = recipe.BatchSize.GetValue(MassUnits.KiloGram) / finalArrival.GetValue(TimeUnits.Minute);

                return new
                {
                    Mixer = mixer,
                    Recipe = recipe,
                    Score = score,
                    ETA = finalArrival,
                    Prep = finalPrepTime
                };
            })
            .OrderByDescending(x => x.Score)
            .ToList();
            var bestRanked = totalRankings.First();
            return new MixerSelectionPlan(bestRanked.Mixer, bestRanked.Recipe, bestRanked.ETA, bestRanked.Prep);
        }
        (ManufaturingEquipment MixerCandidate, IEquipmentMaterial Recipe) SelectCandidateMixers2(ProcessLine Line, IMaterial Material)
        {
            if (Material is null) return (null!, null!);

            // Filtramos todos los mixers que pueden producir este material
            var allCompatible = InletMixers
                .Where(x => x.EquipmentMaterials.Any(m => m.Material.Id == Material.Id))
                .ToList();

            if (!allCompatible.Any()) return (null!, null!);

            //   // --- NIVEL 1: OBLIGAR PREFERIDOS LIBRES ---
            //   // Si un mixer preferido de la línea está desocupado, se asigna de inmediato.
            var preferredCandidates = allCompatible
                 .Where(m => Line.PreferredManufacturer.Contains(m))
                 .Select(m => new
                 {
                     Mixer = m,
                     OrderCount = m.GetManufactureOrderCount(), // Asumiendo este método en tu clase base
                     Recipe = m.EquipmentMaterials.First(x => x.Material.Id == Material.Id)
                 })
                 .OrderBy(x => x.OrderCount) // Prioridad: Menor cola

                 .ToList();

            if (preferredCandidates.Any() && preferredCandidates.First().OrderCount == 0)
            {
                return (preferredCandidates.First().Mixer, preferredCandidates.First().Recipe);
            }





            // --- NIVEL 2: Buscamo por el menor tiempo en lliberrarse ---
            // Si no hay nadie libre, evaluamos quién entregará masa más rápido considerando:
            // Tiempo de batch actual + Lavado + Ciclo de producción.
            var TotalbestOccupied = allCompatible.Select(mixer =>
            {
                var recipe = mixer.EquipmentMaterials.First(m => m.Material.Id == Material.Id);
                Amount totalminutestoGetFree = new Amount(0, TimeUnits.Minute);
                var op = mixer.InletEquipments.OfType<ProcessOperator>().FirstOrDefault();
                if (op != null)
                {
                    // Caso 1: El operario está atendiendo a OTRO Mixer ahora mismo
                    if (op.OcuppiedBy != null && op.OcuppiedBy != mixer)
                    {
                        if (op.OcuppiedBy is ProcessMixer otherMixer)
                        {
                            // El mixer actual no puede empezar su fase manual hasta que el otro termine
                            totalminutestoGetFree += otherMixer.CurrentManufactureOrder?.PendingBatchTime ?? new Amount(0, TimeUnits.Minute);
                        }
                    }
                    // Caso 2: El operario está en descanso programado (Almuerzo/Break)
                    else if (op.OcuppiedBy == null && op.OutletState is FeederPlannedDownTimeState)
                    {
                        // Sumamos el tiempo de los otros mixers que están en cola para ser atendidos al regreso
                        var otherMixers = op.OutletMixers.Where(x => x != mixer).ToList();
                        otherMixers.ForEach(x =>
                        {
                            if (x.CurrentManufactureOrder != null)
                                totalminutestoGetFree += x.CurrentManufactureOrder.PendingBatchTime ?? new Amount(0, TimeUnits.Minute);
                        });
                    }
                }
                if (mixer.CurrentManufactureOrder != null)
                {
                    totalminutestoGetFree += ((MixerManufactureOrder)mixer.CurrentManufactureOrder).PendingBatchTime;
                    totalminutestoGetFree += ((MixerManufactureOrder)mixer.CurrentManufactureOrder).TransferTime;


                }
                else if (mixer.CurrentTransferRequest != null)
                {
                    totalminutestoGetFree += mixer.CurrentTransferRequest.PendingTransferTime;

                }
                foreach (var nextMO in mixer.ManufacturingOrders)
                {
                    totalminutestoGetFree += GetWashoutTime(mixer, nextMO.Material);
                    totalminutestoGetFree += ((MixerManufactureOrder)nextMO).BatchTime;
                    totalminutestoGetFree += ((MixerManufactureOrder)nextMO).TransferTime;
                }
                totalminutestoGetFree += GetWashoutTime(mixer, Material);
                totalminutestoGetFree += recipe.BatchCycleTime;
                totalminutestoGetFree += recipe.TransferTime;

                Amount opDelay = GetOperatorDownTimeDelay(mixer, totalminutestoGetFree, recipe.BatchCycleTime);
                totalminutestoGetFree += opDelay;
                // ETA Total (minutos de simulación)
                double totalArrivalMinutes = totalminutestoGetFree.GetValue(TimeUnits.Minute);

                // Score = Masa / Tiempo. El que entregue más Kg/min es el ganador.
                double massValue = recipe.BatchSize.GetValue(MassUnits.KiloGram);
                double productivityScore = massValue / (totalArrivalMinutes > 0 ? totalArrivalMinutes : 1);

                return new
                {
                    Mixer = mixer,
                    Recipe = recipe,
                    Score = productivityScore,

                };
            })

            .OrderByDescending(x => x.Score)

            .ToList();

            var bestOccupied = TotalbestOccupied.First();

            return (bestOccupied.Mixer, bestOccupied.Recipe);
        }

        Amount GetWashoutTime(ManufaturingEquipment mixer, IMaterial material)
        {
            Amount washoutTime = new Amount(0, TimeUnits.Minute);
            if (mixer.LastMaterial != null)
            {

                var washoutDef = mixer.WashoutTimes
                                .FirstOrDefault(x => x.ProductCategoryCurrent == mixer.LastMaterial.ProductCategory &&
                                                   x.ProductCategoryNext == material.ProductCategory);

                if (washoutDef != null)
                {
                    washoutTime = washoutDef.MixerWashoutTime;
                }
            }
            return washoutTime;
        }
        IEquipmentMaterial SelectMaterialFromMixer(ManufaturingEquipment mixer, IMaterial material)
        {

            var materialFoundFromMixer = mixer.EquipmentMaterials.FirstOrDefault(x => x.Material.Id == material.Id);

            return materialFoundFromMixer!;
        }








        public bool IsNextMaterialNeedBySKID(IWIPManufactureOrder _CurrentOrder)
        {
            var nextProductionOrder = _CurrentOrder.LineNextProductionOrder;
            if (nextProductionOrder != null)
            {
                var wiptanks = nextProductionOrder.Line.InletPumps
                    .SelectMany(x => x.InletWipTanks).ToList();

                var manufactures = wiptanks.SelectMany(x => x.ManufactureAttached)
                    .FirstOrDefault(x => x.EquipmentMaterials.Any(x => x.Material.Id == nextProductionOrder.Material.Id));
                if (manufactures != null && manufactures is ProcessMixer mixer)
                {
                    wiptanks = wiptanks.Where(x => x.ManufactureAttached.Any(x =>
                    x.EquipmentMaterials.Any(x => x.Material.Id == nextProductionOrder.Material.Id))).ToList();


                    var result = IsNextProductionBySKIDNeededToStart(_CurrentOrder, nextProductionOrder, wiptanks);
                    if (result)
                    {
                        return true;
                    }
                    return false;


                }

            }
            return false;
        }
        public bool IsNextProductionBySKIDNeededToStart(IWIPManufactureOrder _CurrentOrder, FromLineToWipProductionOrder nextproductionorder, List<ProcessWipTankForLine> wiptanks)
        {

            if (nextproductionorder == null)
                return false;

            var productionPlan = SelectCandidateMixers(nextproductionorder.Line, nextproductionorder.Material);
            if (productionPlan.Mixer is null || productionPlan.Recipe is null)
                return false;

            if (_CurrentOrder.AverageOutletFlow.Value <= 0)
                return false;

            // Tank wash time (due to material change in the WIP tank)
            Amount tankWashTime = new Amount(0, TimeUnits.Minute);
            Amount currentLevelTanks = new Amount(0, MassUnits.KiloGram);
            foreach (var wipTank in wiptanks)
            {
                tankWashTime += GetWashoutTime(wipTank.LastMaterial, nextproductionorder.Material);
                currentLevelTanks = wipTank.CurrentLevel > currentLevelTanks ? wipTank.CurrentLevel : currentLevelTanks;
            }
            Amount changeovetime = new Amount(0, TimeUnits.Minute);
            if (_CurrentOrder.LineCurrentProductionOrder.Line.MustChangeFormat())
            {
                changeovetime = _CurrentOrder.LineCurrentProductionOrder.TimeToChangeSKU;
            }



            if (changeovetime > tankWashTime)
            {
                tankWashTime = changeovetime;
            }


            // Total time for mixer to complete next batch (includes mixer washout + production)
            var mixerTotalTime = productionPlan.PreparationTime;

            if (_CurrentOrder.TimeToEmptyMassInProcess.Value > 0)
            {
                var timetoEmptyVessel = currentLevelTanks / _CurrentOrder.AverageOutletFlow;
                var timeUntilTankIsReady = _CurrentOrder.TimeToEmptyMassInProcess + tankWashTime + timetoEmptyVessel;

                // Start mixer IF it will finish BEFORE or EXACTLY when tank is ready
                if (timeUntilTankIsReady <= mixerTotalTime)
                {
                    //if (!_CurrentOrder.IsSendToLineCurrentOrderIsProduced)
                    //{
                    //    _CurrentOrder.SendToLineCurrentOrderIsProduced();
                    //}

                    return true;
                }
                // Time from NOW until tank is ready (empty + washed)

            }
            return false;
        }
        public bool IsNextMaterialNeededByMixer(IWIPManufactureOrder _CurrentOrder, IWIPManufactureOrder _NextOrder)
        {
            if (_NextOrder == null || _CurrentOrder == null)
                return false;

            var productionPlan = SelectCandidateMixers(_NextOrder.Line, _NextOrder.Material);
            if (productionPlan.Mixer is null || productionPlan.Recipe is null)
                return false;

            if (_CurrentOrder.AverageOutletFlow.Value <= 0)
                return false;

            // Tank wash time (due to material change in the WIP tank)
            Amount tankWashTime = new Amount(0, TimeUnits.Minute);
            if (_NextOrder.ManufactureOrdersFromMixers.Count == 0)
            {
                tankWashTime = GetWashoutTime(_CurrentOrder.Material, _NextOrder.Material);

            }
            Amount changeovetime = new Amount(0, TimeUnits.Minute);
            if (_CurrentOrder.LineCurrentProductionOrder.Line.MustChangeFormat())
            {
                changeovetime = _CurrentOrder.LineCurrentProductionOrder.TimeToChangeSKU;
            }

            if (changeovetime > tankWashTime)
            {
                tankWashTime = changeovetime;
            }
            // Total time for mixer to complete next batch (includes mixer washout + production)
            var mixerTotalTime = productionPlan.PreparationTime;

            if (_NextOrder.TotalMassProducingInMixer == ZeroMass)
            {
                var timeUntilTankIsReady = _CurrentOrder.TimeToEmptyMassInProcess + tankWashTime;

                // Start mixer IF it will finish BEFORE or EXACTLY when tank is ready
                if (timeUntilTankIsReady <= mixerTotalTime)
                {
                    return TryToStartNewOrder(_NextOrder, productionPlan.Mixer, productionPlan.Recipe);
                }
            }
            else
            {
                var futureLevel = _NextOrder.TotalMassProducingInMixer + productionPlan.Recipe.BatchSize;
                if (futureLevel <= Capacity)
                {
                    return TryToStartNewOrder(_NextOrder, productionPlan.Mixer, productionPlan.Recipe);
                }
            }


            return false;
        }

        // Dentro de ProcessWipTankForLine.cs o en una clase de utilidad

       

    }

}
