using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.PackageLines
{

    public class NewLine : NewPlantUnit
    {
        public bool IsWorkComplete =>
        _productionQueue.Count == 0 && // No hay nada en cola
        _currentOrder == null;
        public List<ProductionOrderReport> OrderHistory { get; private set; } = new();

        private List<NewMixer> _PreferedMixers = new();
        public List<NewMixer> PreferedMixers => _PreferedMixers;

        public void AddPreferedMixers(NewMixer preferedMixers)
        {
            _PreferedMixers.Add(preferedMixers);
        }
        public ProductionOrderReport? CurrentOrderReport { get; private set; }
        public WashoutMatrix WashoutRules { get; private set; }

        public ShiftType ShiftType { get; private set; }
        private Queue<ProductionOrder> _productionQueue = new();
        public int ProductionOrderCount => _productionQueue.Count;
        public Queue<ProductionOrder> ProductionQueue => _productionQueue;
        public NewPump? WashingPump { get; set; }
        public TimeSpan AuCheckInterval { get; private set; }
        public NewLine(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory, TimeSpan auCheckInterval, WashoutMatrix _WashoutRules) : base(id, name, type, focusFactory)
        {
            AuCheckInterval = auCheckInterval;
            WashoutRules = _WashoutRules;

        }
        // En NewLine.cs

        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            // Definimos un color neutro (el que use tu UI por defecto, usualmente negro o gris oscuro)
            string neutral = "";
            string alertRed = "#F44336";

            if (_currentOrder != null && CurrentOrderReport != null)
            {
                // 1. IDENTIDAD (Sin colores distractores)
                reportList.Add(new NewInstantaneousReport("SKU Name", _currentOrder.SkuName, IsBold: true, Color: neutral));
                reportList.Add(new NewInstantaneousReport("Material", CurrentOrderReport.MaterialName, Color: neutral));
                reportList.Add(new NewInstantaneousReport("Source WIP", CurrentOrderReport.WipName, Color: neutral));

                // 2. MASAS
                double produced = CurrentOrderReport.ProducedMassKg;
                double target = CurrentOrderReport.TargetMassKg;
                double pending = Math.Max(0, target - produced);

                reportList.Add(new NewInstantaneousReport("Target Mass", $"{target:N0} kg", Color: neutral));
                reportList.Add(new NewInstantaneousReport("Packed", $"{produced:N1} kg", Color: neutral));

                // ALERTA 1: Lo pendiente en ROJO (es el trabajo que falta por hacer)
                reportList.Add(new NewInstantaneousReport("Pending", $"{pending:N1} kg", Color: neutral, IsBold: false));

                // 3. RENDIMIENTO
                // ALERTA 2: Si la velocidad es 0, la marcamos en ROJO porque la línea está parada
                string speedColor = CurrentFlowRateKg <= 0 ? alertRed : neutral;
                reportList.Add(new NewInstantaneousReport("Speed", $"{CurrentFlowRateKg:F2} kg/s", Color: speedColor, IsBold: CurrentFlowRateKg <= 0));

                // 4. TIEMPOS
                if (CurrentFlowRateKg > 0)
                {
                    double secondsLeft = pending / CurrentFlowRateKg;
                    var etc = TimeSpan.FromSeconds(secondsLeft);
                    reportList.Add(new NewInstantaneousReport("Est. Finish", etc.ToString(@"hh\:mm\:ss"), Color: neutral));
                }
            }
            else
            {
                // ALERTA 3: No hay orden activa (Línea ociosa)
                reportList.Add(new NewInstantaneousReport("Order Status", "NO ACTIVE ORDER", Color: alertRed, IsBold: true));
            }

            // El futuro siempre en un tono sutil (gris) para no confundir con el presente
            if (_nextOrder != null)
            {
                reportList.Add(new NewInstantaneousReport("Next in Queue", _nextOrder.SkuName, FontSize: "0.75rem", Color: "#9E9E9E"));
            }
        }
        public void AssignProductionPlan(IEnumerable<ProductionOrder> orders, IEnumerable<Guid> preferredMixers, ShiftType shift)
        {
            // 1. Limpiamos cola anterior si hubiera
            _productionQueue.Clear();


            // 2. Encolamos las órdenes ordenadas por secuencia
            foreach (var order in orders.OrderBy(x => x.OrderSequence))
            {
                _productionQueue.Enqueue(order);
            }

            // 3. Asignamos preferencias y turno

            ShiftType = shift;
        }
        public void ClearPlan()
        {
            _productionQueue.Clear();

            // Resetear cualquier estado de orden en curso si es necesario
        }


        private ProductionOrder? _currentOrder;
        private ProductionOrder? _nextOrder;


        // Variable interna para saber qué estamos haciendo ahora
        public void AddProducingSecond(double mass)
        {
            if (CurrentOrderReport == null) return;
            CurrentOrderReport.ProducingSeconds++;
            CurrentOrderReport.ProducedMassKg += mass;
        }

        public void AddInletStarvationSecond()
        {
            if (CurrentOrderReport != null) CurrentOrderReport.InletStarvationSeconds++;
        }

        public void AddInternalLossSecond()
        {
            if (CurrentOrderReport != null) CurrentOrderReport.InternalLossSeconds++;
        }

        public void AddChangeOverSecond()
        {
            if (CurrentOrderReport != null) CurrentOrderReport.ChangeOverSeconds++;
        }

        public void AddStandbySecond()
        {
            if (CurrentOrderReport != null) CurrentOrderReport.OutOfShiftSeconds++;
        }

        public void ProductChange(DateTime InitialDate)
        {
            if (_productionQueue.Count == 0)
            {
                TransitionOutletState(new NewLineNotScheduled(this));
                TransitionInletState(new NewNotScheduleInlet(this));
                return;

            }
            GetNextOrder();
            if (_currentOrder == null) return;


            // Lo añadimos al historial de una vez para que sea visible en el Dashboard

            int currentShiftIndex = GetCurrentShiftIndex(InitialDate);
            bool isScheduledNow = IsLineScheduledForShift(currentShiftIndex, ShiftType);
            if (isScheduledNow)
            {
                TransitionInletState(new NewReadyToProduce(this));
            }
            else
            {
                TransitionInletState(new NewOutOfShiftState(this));
            }
            CalculateAuTime();
            TransitionOutletState(new NewProducingLineState(this));
            CalculateCurrentOrderRequirements();
            var result = GetWipToProduce(_currentOrder!);
            CurrentWipTank = result.Wip;
            CurrentWipPump = result.Pump;
            CurrentOrderReport = new ProductionOrderReport
            {
                OrderId = _currentOrder!.OrderId,
                SkuName = _currentOrder.SkuName,
                MaterialName = _currentOrder.Material?.Name ?? "Unknown",
                TargetMassKg = _currentOrder.MassToPack,
                OrderGoalSummary = $"{_currentOrder.PlannedCases} CASES OF {_currentOrder.SkuName}".ToUpper()
            };
            OrderHistory.Add(CurrentOrderReport);
            SendToWipCurrentOrder();
        }
        public NewWipTank? CurrentWipTank { get; private set; }
        public NewPump? CurrentWipPump { get; private set; }
        public (NewWipTank? Wip, NewPump? Pump) GetWipToProduce(ProductionOrder _Order)
        {
            if (_Order == null) return (null!, null!);

            var inletpumps = Inputs.OfType<NewPump>().ToList();

            foreach (var inletpump in inletpumps)
            {
                var inletWipTanks = inletpump.Inputs.OfType<NewWipTank>().ToList();
                foreach (var wip in inletWipTanks)
                {
                    var upstreamEquipment = wip.Inputs.OfType<NewManufacture>()
                   .Concat(wip.Inputs.SelectMany(x => x.Inputs.OfType<NewManufacture>()));

                    foreach (var me in upstreamEquipment)
                    {
                        if (me.SupportedProducts.Any(x => x.Id == _Order.MaterialId))
                        {

                            return (wip, inletpump);
                        }
                    }
                }
            }
            return (null!, null!);


        }
        public void DetachWip()
        {
            if (CurrentWipTank == null) return;
            _currentOrder = null!;
            CurrentWipTank.CurrentLine = null!;
            CurrentWipTank?.ReleaseAccess(this);

        }
        void SendToWipCurrentOrder()
        {
            CurrentWipTank?.ReceiveOrderFromProductionLine(this, _currentOrder!.Material!, _currentOrder!.MassToPack);
        }
        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
            WashingPump = Inputs.OfType<NewPump>().FirstOrDefault(x => x.IsForWashing);
            ProductChange(InitialDate);

        }

        public void GetNextOrder()
        {
            _currentOrder = _productionQueue.Dequeue();

            // Obtenemos el segundo sin alterar la cola
            _nextOrder = _productionQueue.Count > 0 ? _productionQueue.Peek() : null!;
        }
        public int GetCurrentShiftIndex(DateTime currentTime)
        {
            var time = TimeOnly.FromDateTime(currentTime);

            if (time >= new TimeOnly(6, 0) && time < new TimeOnly(14, 0)) return 1;
            if (time >= new TimeOnly(14, 0) && time < new TimeOnly(22, 0)) return 2;

            // El turno 3 cruza la medianoche (22:00 a 05:59:59)
            return 3;
        }
        public bool IsLineScheduledForShift(int shiftIndex, ShiftType type)
        {
            return type switch
            {
                ShiftType.Shift_1_2_3 => true,
                ShiftType.Shift_1_2 => shiftIndex == 1 || shiftIndex == 2,
                ShiftType.Shift_1_3 => shiftIndex == 1 || shiftIndex == 3,
                ShiftType.Shift_2_3 => shiftIndex == 2 || shiftIndex == 3,
                ShiftType.Shift_1 => shiftIndex == 1,
                ShiftType.Shift_2 => shiftIndex == 2,
                ShiftType.Shift_3 => shiftIndex == 3,
                _ => false
            };
        }



        private double _targetMassKg;
        private double _flowRateKgPerSec;
        private double _currentflowRateKg;
        public double _currentMasProduced = 0;
        public double CurrentMasProduced => _currentMasProduced;
        public double CurrentMassPending => _targetMassKg - _currentMasProduced;

        public double CurrentFlowRateKg => _currentflowRateKg;
        public void Produce()
        {
            _currentflowRateKg = _flowRateKgPerSec;
            CurrentWipPump?.SetCurrentFlow(_currentflowRateKg);
            _currentMasProduced += _currentflowRateKg;

        }
        // En NewLine.cs

        // Dentro de NewLine.cs

        public override void ExecuteProcess()
        {
            // Solo registramos si hay una orden activa
            if (CurrentOrderReport != null)
            {
                CurrentOrderReport.WipLevelKg = CurrentWipTank?.CurrentLevel.GetValue(MassUnits.KiloGram) ?? 0;
                // 1. CAPTURA DE ESTADOS GLOBALES (Viene de NewPlantUnit)
                if (!GlobalState.IsOperational)
                {
                    // Si es por mantenimiento o parada programada
                    if (GlobalState is GlobalState_Maintenance || IsOnPlannedBreak(CurrentDate))
                    {
                        CurrentOrderReport.PlannedDownTimeSeconds++;
                        StopFlow(); // Función neutral para detener bombas
                    }
                    // Si la línea está bien pero un equipo externo la bloquea
                    else if (GlobalState is GlobalState_MasterWaiting waitingState)
                    {
                        // Verificamos si el recurso que esperamos es la Bomba de Lavado
                        if (OutletState is NewConcurrentChangeoverState || OutletState is NewWashingOnlyState)
                        {
                            CurrentOrderReport.WashingPumpWaitSeconds++;
                        }
                        else
                        {
                            CurrentOrderReport.BlockedByResourceSeconds++;
                        }
                        StopFlow();
                    }
                }
            }

            // 2. EJECUCIÓN DE ESTADOS OPERACIONALES (Inlet/Outlet)
            // base.ExecuteProcess() llamará al Calculate() de tus estados actuales
            base.ExecuteProcess();
        }

        // Método auxiliar neutral para detener el flujo físico
        public void StopFlow()
        {
            _currentflowRateKg = 0;
            CurrentWipPump?.SetCurrentFlow(0);
        }
        public void NotProduce()
        {
            _currentflowRateKg = 0;
            CurrentWipPump?.SetCurrentFlow(_currentflowRateKg);

        }
        private void CalculateCurrentOrderRequirements()
        {
            if (_currentOrder == null) return;

            // 1. Masa total a producir
            _targetMassKg = _currentOrder.MassToPack;

            _currentMasProduced = 0;
            _flowRateKgPerSec = _currentOrder.FlowRatePersec;
        }
        private double _productionBudgetRemaining;
        private double _stopBudgetRemaining;

        public double ProducingReimaingTime => _productionBudgetRemaining;
        public double ProducingByAuReimaingTime => _stopBudgetRemaining;

        void SetAuBudget(double productionTime, double stopTime)
        {
            _productionBudgetRemaining = productionTime;
            _stopBudgetRemaining = stopTime;
        }
        public void CalculateAuTime()
        {
            // En este estado exacto, la línea está calculando su "hoja de ruta" 
            // para el intervalo actual de AU. No consume masa todavía.

            // 1. Obtenemos el AU del SKU actual (ej. 0.85 para 85%)
            double auFactor = _currentOrder?.AU ?? 0;

            // 2. Definimos el intervalo de cálculo (ej. 3600 segundos = 1 hora)
            double totalIntervalSeconds = AuCheckInterval.TotalSeconds;

            Random _random = new Random();
            var time = _random.Next((int)totalIntervalSeconds);




            // 3. Calculamos tiempos maestros
            double productionTime = time * auFactor / 100;
            double stopTime = time - productionTime;

            // 4. Inyectamos estos tiempos en la línea para que los use el CheckStatus
            SetAuBudget(productionTime, stopTime);
        }
        public ProductionOrder? CurrentOrder => _currentOrder;
        public ProductionOrder? NextOrder => _nextOrder;
        public bool NeedsWashing(ProductDefinition? _last, ProductDefinition? _new)
        {
            if (_last == null || _new == null) return false;
            return _last.Id != _new.Id;
        }




    }
}
