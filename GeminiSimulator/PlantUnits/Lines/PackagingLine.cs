namespace GeminiSimulator.PlantUnits.Lines
{
    public enum LineStateCategory
    {
        Producing,          // En NewProducingLineState (Producción real)
        StarvedByInlet,     // En NewStarvedByInlet (Falta de producto en WIP)
        StarvedByAu,        // En NewStarvedByAuState (Paradas técnicas/AU)
        ChangeOver,         // En NewChangeOverState (Lavados y Formatos)
        OutOfShift,         // En NewOutOfShiftState (Personal fuera de turno)
        NotScheduled        // En NewLineNotScheduled (Sin órdenes en cola)
    }
    //public class PackagingLine : PlantUnit
    //{
    //    private readonly Dictionary<LineStateCategory, double> _stateAccumulator = new();

    //    public bool LineIsRunning => _inboundState is LineInletAvailable;
    //    public Dictionary<LineStateCategory, double>   StateAcumulator=> _stateAccumulator;
    //    public override Dictionary<string, ReportField> GetReportData()
    //    {
    //        var data = base.GetReportData(); // Trae el "Name"

    //        // 1. Caso: Línea no programada
    //        if (_currentOrder == null)
    //        {
    //            data.Add("Status", new ReportField("NOT SCHEDULED", IsBold: true, Color: "#ff4444"));
    //            data.Add("Message", new ReportField("Waiting for production plan..."));
    //            return data;
    //        }

    //        // 2. Unificación del Estado (Lógica de prioridad)
    //        // Si la entrada no está disponible (Break, Out of Shift, Starved), mostramos eso.
    //        // Si la entrada está disponible, mostramos lo que hace la salida (Producing, AU, etc.)
    //        string unifiedStatus = _inboundState is LineInletAvailable
    //                               ? _outboundState?.StateName ?? string.Empty
    //                               : _outboundState is ChangeOverState ? _outboundState?.StateName ?? string.Empty : _inboundState?.StateName ?? string.Empty;

    //        data.Add("Current SKU", new ReportField(_currentOrder.SkuName, IsBold: true));
    //        data.Add("Status", new ReportField(unifiedStatus.ToUpper(), IsBold: true));

    //        // 3. Progreso de Masa (Solo si hay orden)
    //        double progress = _targetMassKg > 0 ? (_currentMasProduced / _targetMassKg) * 100 : 0;

    //        // Formateamos a 1 decimal para que el operario no vea ruido visual
    //        data.Add("Production", new ReportField($"{_currentMasProduced:F1} / {_targetMassKg:F1} Kg"));
    //        data.Add("Efficiency", new ReportField($"{progress:F1}%"));

    //        // 4. Velocidad (Solo mostramos si es mayor a 0 para no confundir)
    //        data.Add("Current Speed", new ReportField($"{_currentflowRateKg:F2} Kg/s"));

    //        return data;
    //    }
    //    public ShiftType ShiftType { get; private set; }
    //    private Queue<ProductionOrder> _productionQueue = new();
    //    public TimeSpan AuCheckInterval { get; private set; }

    //    private List<Guid> _preferredMixers = new();
    //    // Constructor limpísimo
    //    public PackagingLine(Guid id, string name, ProcessEquipmentType type, FocusFactory factory, TimeSpan auCheckInterval)
    //        : base(id, name, type, factory)
    //    {
    //        AuCheckInterval = auCheckInterval;
    //        foreach (LineStateCategory cat in Enum.GetValues(typeof(LineStateCategory)))
    //        {
    //            _stateAccumulator[cat] = 0;
    //        }
    //    }
    //    public void AccumulateTime(LineStateCategory category, double seconds = 1.0)
    //    {
    //        _stateAccumulator[category] += seconds;
    //    }

    //    public IReadOnlyDictionary<LineStateCategory, double> FullReport => _stateAccumulator;

    //    public void AssignProductionPlan(IEnumerable<ProductionOrder> orders, IEnumerable<Guid> preferredMixers, ShiftType shift)
    //    {
    //        // 1. Limpiamos cola anterior si hubiera
    //        _productionQueue.Clear();
    //        _preferredMixers.Clear();

    //        // 2. Encolamos las órdenes ordenadas por secuencia
    //        foreach (var order in orders.OrderBy(x => x.OrderSequence))
    //        {
    //            _productionQueue.Enqueue(order);
    //        }

    //        // 3. Asignamos preferencias y turno
    //        _preferredMixers.AddRange(preferredMixers);
    //        ShiftType = shift;
    //    }

    //    /// <summary>
    //    /// Obtiene la siguiente orden a procesar.
    //    /// </summary>

    //    public void ClearPlan()
    //    {
    //        _productionQueue.Clear();
    //        _preferredMixers.Clear();
    //        // Resetear cualquier estado de orden en curso si es necesario
    //    }


    //    private ProductionOrder? _currentOrder;
    //    private ProductionOrder? _nextOrder;

    //    // Propiedades públicas (Read-only) para que los estados y la UI las consulten
    //    public ProductionOrder? CurrentOrder => _currentOrder;
    //    public ProductionOrder? NextOrder => _nextOrder;

    //    public void ProductChange(DateTime InitialDate)
    //    {
    //        if (_productionQueue.Count == 0)
    //        {
    //            TransitionOutbound(new LineNotScheduled(this));
    //            TransitionInBound(new NotScheduleInlet(this));
    //            return;

    //        }
    //        GetNextOrder();
    //        int currentShiftIndex = GetCurrentShiftIndex(InitialDate);
    //        bool isScheduledNow = IsLineScheduledForShift(currentShiftIndex, ShiftType);
    //        if (isScheduledNow)
    //        {
    //            if (IsOnPlannedBreak(InitialDate))
    //            {
    //                TransitionInBound(new PlannedDowntimeState(this));
    //            }
    //            else
    //            {
    //                TransitionInBound(new ReadyToProduce(this));
    //            }
    //        }
    //        else
    //        {
    //            TransitionInBound(new OutOfShiftState(this));
    //        }
    //        CalculateAuTime();
    //        TransitionOutbound(new ProducingLineState(this));
    //        CalculateCurrentOrderRequirements();
    //        CurrentWipTank = GetWipToProduce(_currentOrder!);
    //        SendToWipCurrentOrder();
    //    }
    //    public WipTank? CurrentWipTank {  get; private set; }
    //    public WipTank GetWipToProduce(ProductionOrder _Order)
    //    {
    //        if (_Order == null) return null!;
    //        var inletWipTanks = Inputs.SelectMany(x => x.Inputs.OfType<WipTank>()).ToList();

    //        foreach (var wip in inletWipTanks)
    //        {
    //            var manufactureEquipmnets = wip.Inputs.SelectMany(x => x.Inputs.OfType<EquipmentManufacture>()).ToList();
    //            if (manufactureEquipmnets.Any())
    //            {
    //                foreach (var me in manufactureEquipmnets)
    //                {
    //                    if (_Order != null && me.Materials.Any(x => x.Id == _Order.MaterialId))
    //                    {
    //                        return wip;




    //                    }
    //                }
    //            }
    //            manufactureEquipmnets = wip.Inputs.OfType<EquipmentManufacture>().ToList();
    //            if (manufactureEquipmnets.Any())
    //            {
    //                foreach (var me in manufactureEquipmnets)
    //                {
    //                    if (_Order != null && me.Materials.Any(x => x.Id == _Order.MaterialId))
    //                    {
    //                        return wip;




    //                    }
    //                }
    //            }


    //        }
    //        return null!;

    //    }
    //    public void DetachWip()
    //    {
    //        if (CurrentWipTank == null) return;
    //        _currentOrder = null!;
    //        CurrentWipTank?.DetachLine(this);

    //    }
    //    void SendToWipCurrentOrder()
    //    {
    //        CurrentWipTank?.ReceiveRequirementFromLine(this);
    //    }
    //    public override void CheckInitialStatus(DateTime InitialDate)
    //    {
    //        ProductChange(InitialDate);
    //    }

    //    public void GetNextOrder()
    //    {
    //        _currentOrder = _productionQueue.Dequeue();

    //        // Obtenemos el segundo sin alterar la cola
    //        _nextOrder = _productionQueue.Count > 0 ? _productionQueue.Peek() : null!;
    //    }
    //    public int GetCurrentShiftIndex(DateTime currentTime)
    //    {
    //        var time = TimeOnly.FromDateTime(currentTime);

    //        if (time >= new TimeOnly(6, 0) && time < new TimeOnly(14, 0)) return 1;
    //        if (time >= new TimeOnly(14, 0) && time < new TimeOnly(22, 0)) return 2;

    //        // El turno 3 cruza la medianoche (22:00 a 05:59:59)
    //        return 3;
    //    }
    //    public bool IsLineScheduledForShift(int shiftIndex, ShiftType type)
    //    {
    //        return type switch
    //        {
    //            ShiftType.Shift_1_2_3 => true,
    //            ShiftType.Shift_1_2 => shiftIndex == 1 || shiftIndex == 2,
    //            ShiftType.Shift_1_3 => shiftIndex == 1 || shiftIndex == 3,
    //            ShiftType.Shift_2_3 => shiftIndex == 2 || shiftIndex == 3,
    //            ShiftType.Shift_1 => shiftIndex == 1,
    //            ShiftType.Shift_2 => shiftIndex == 2,
    //            ShiftType.Shift_3 => shiftIndex == 3,
    //            _ => false
    //        };
    //    }



    //    private double _targetMassKg;
    //    private double _flowRateKgPerSec;
    //    private double _currentflowRateKg;
    //    public double _currentMasProduced = 0;
    //    public double CurrentMasProduced => _currentMasProduced;
    //    public double CurrentMassPending => _targetMassKg - _currentMasProduced;

    //    public double CurrentFlowRateKg => _currentflowRateKg;
    //    public void Produce()
    //    {
    //        _currentflowRateKg = _flowRateKgPerSec;
    //        CurrentWipTank?.SetOutletFlow(_currentflowRateKg);
    //        _currentMasProduced += _currentflowRateKg;
    //    }
    //    public override void Notify()
    //    {

    //        CurrentWipTank?.Update();
    //    }
    //    public void NotProduce()
    //    {
    //        _currentflowRateKg = 0;
    //        CurrentWipTank?.SetOutletFlow(_currentflowRateKg);
    //    }
    //    private void CalculateCurrentOrderRequirements()
    //    {
    //        if (_currentOrder == null) return;

    //        // 1. Masa total a producir
    //        _targetMassKg = _currentOrder.MassToPack;

    //        _currentMasProduced = 0;
    //        _flowRateKgPerSec = _currentOrder.FlowRatePersec;
    //    }
    //    private double _productionBudgetRemaining;
    //    private double _stopBudgetRemaining;

    //    public double ProducingReimaingTime => _productionBudgetRemaining;
    //    public double ProducingByAuReimaingTime => _stopBudgetRemaining;

    //    void SetAuBudget(double productionTime, double stopTime)
    //    {
    //        _productionBudgetRemaining = productionTime;
    //        _stopBudgetRemaining = stopTime;
    //    }
    //    public void CalculateAuTime()
    //    {
    //        // En este estado exacto, la línea está calculando su "hoja de ruta" 
    //        // para el intervalo actual de AU. No consume masa todavía.

    //        // 1. Obtenemos el AU del SKU actual (ej. 0.85 para 85%)
    //        double auFactor = CurrentOrder?.AU ?? 0;

    //        // 2. Definimos el intervalo de cálculo (ej. 3600 segundos = 1 hora)
    //        double totalIntervalSeconds = AuCheckInterval.TotalSeconds;

    //        Random _random = new Random();
    //        var time = _random.Next((int)totalIntervalSeconds);




    //        // 3. Calculamos tiempos maestros
    //        double productionTime = time * auFactor / 100;
    //        double stopTime = time - productionTime;

    //        // 4. Inyectamos estos tiempos en la línea para que los use el CheckStatus
    //        SetAuBudget(productionTime, stopTime);
    //    }
    //    public override void Update()
    //    {
    //        if (_inboundState is ReadyToProduce)
    //        {
    //            if (CurrentWipTank?.OutboundState is TankAvailableState)
    //            {
    //                CheckReinitProductionAtInlet();
    //            }
    //            else if (CurrentWipTank?.OutboundState is TankLoLevelState)
    //            {
    //                TransitionInBound(new StarvedByInlet(this));

    //            }
    //        }
    //        else if (_inboundState is StarvedByInlet)
    //        {
    //            if (CurrentWipTank?.OutboundState is TankLoLevelState)
    //            {
    //                TransitionInBound(new StarvedByInlet(this));
    //            }
    //            else if (CurrentWipTank?.OutboundState is TankAvailableState)
    //            {
    //                CheckReinitProductionAtInlet();
    //            }
    //        }
    //        else
    //        {
    //            if (CurrentWipTank?.OutboundState is TankLoLevelState)
    //            {
    //                TransitionInBound(new StarvedByInlet(this));

    //            }
    //        }
    //    }
    //    void CheckReinitProductionAtInlet()
    //    {
    //        int currentShiftIndex = GetCurrentShiftIndex(CurrentDate);
    //        bool isScheduledNow = IsLineScheduledForShift(currentShiftIndex, ShiftType);

    //        if (isScheduledNow)
    //        {
    //            if (IsOnPlannedBreak(CurrentDate))
    //            {
    //                TransitionInBound(new PlannedDowntimeState(this));
    //                return;
    //            }
    //            TransitionInBound(new LineInletAvailable(this));
    //        }
    //        else
    //        {
    //            // Si el break terminó justo cuando también terminaba el turno,
    //            // pasamos al estado de espera de personal.
    //            TransitionInBound(new OutOfShiftState(this));
    //        }
    //    }

    //    public void TransitionInBound(LineInletState newState)
    //     => TransitionInBoundInternal(newState);

    //    // Solo acepta estados de salida para la línea
    //    public void TransitionOutbound(LineOutletState newState)
    //        => TransitionOutboundInternal(newState);

    //    public override void InitialUpdate()
    //    {
    //        if (CurrentOrder == null) return;
    //    }
    //}
}
