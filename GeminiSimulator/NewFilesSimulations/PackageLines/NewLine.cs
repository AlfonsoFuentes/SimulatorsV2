using GeminiSimulator.DesignPatterns;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.Enums;

namespace GeminiSimulator.NewFilesSimulations.PackageLines
{
    public class NewLine : NewPlantUnit
    {
        private readonly Dictionary<LineStateCategory, double> _stateAccumulator = new();
        public WashoutMatrix WashoutRules { get; private set; }
        public bool LineIsRunning => InletState is NewLineInletAvailable;
        public Dictionary<LineStateCategory, double> StateAcumulator => _stateAccumulator;
        public ShiftType ShiftType { get; private set; }
        private Queue<ProductionOrder> _productionQueue = new();
        public int ProductionOrderCount => _productionQueue.Count;
        public TimeSpan AuCheckInterval { get; private set; }
        public NewLine(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory, TimeSpan auCheckInterval, WashoutMatrix _WashoutRules) : base(id, name, type, focusFactory)
        {
            AuCheckInterval = auCheckInterval;
            WashoutRules = _WashoutRules;
        }
        // En NewLine.cs

        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            if (_currentOrder != null)
            {
                // 1. SKU ACTUAL
                reportList.Add(new NewInstantaneousReport("SKU", _currentOrder.Material?.Name ?? "Unknown", IsBold: true, Color: "#E91E63"));

                // 2. BARRA DE PROGRESO (Texto)
                double produced = CurrentMasProduced;
                double target = _currentOrder.MassToPack;
                double pct = target == 0 ? 0 : (produced / target) * 100;

                reportList.Add(new NewInstantaneousReport("Progress", $"{pct:F1}%", IsBold: true, Color: pct > 90 ? "#4CAF50" : "#2196F3"));

                // 3. DETALLE DE KILOS
                reportList.Add(new NewInstantaneousReport("Packed", $"{produced:F1} kg", Color: "#4CAF50"));
                reportList.Add(new NewInstantaneousReport("Pending", $"{CurrentMassPending:F1} kg", Color: "#F44336"));

                // 4. VELOCIDAD ACTUAL
                reportList.Add(new NewInstantaneousReport("Speed", $"{CurrentFlowRateKg:F2} kg/s", Color: "#607D8B"));
            }
            else
            {
                reportList.Add(new NewInstantaneousReport("Order", "No Active Order", Color: "#9E9E9E"));
            }

            // 5. PRÓXIMA ORDEN
            if (_nextOrder != null)
            {
                reportList.Add(new NewInstantaneousReport("Next SKU", _nextOrder.Material?.Name ?? "Unknown", FontSize: "0.75rem", Color: "#9E9E9E"));
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
        public void AccumulateTime(LineStateCategory category, double seconds = 1.0)
        {
            _stateAccumulator[category] += seconds;
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
            CurrentWipTank.CurrentLine=null!;
            CurrentWipTank?.ReleaseAccess(this);

        }
        void SendToWipCurrentOrder()
        {
            CurrentWipTank?.ReceiveOrderFromProductionLine(this,_currentOrder!.Material!, _currentOrder!.MassToPack);
        }
        public override void CheckInitialStatus(DateTime InitialDate)
        {
            base.CheckInitialStatus(InitialDate);
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
        void CheckReinitProductionAtInlet()
        {
            int currentShiftIndex = GetCurrentShiftIndex(CurrentDate);
            bool isScheduledNow = IsLineScheduledForShift(currentShiftIndex, ShiftType);

            if (isScheduledNow)
            {
                TransitionInletState(new NewLineInletAvailable(this));
            }
            else
            {
                // Si el break terminó justo cuando también terminaba el turno,
                // pasamos al estado de espera de personal.
                TransitionInletState(new NewOutOfShiftState(this));
            }
        }




    }
}
