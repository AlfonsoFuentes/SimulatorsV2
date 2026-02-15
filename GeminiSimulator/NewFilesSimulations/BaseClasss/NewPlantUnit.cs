using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.BaseClasss
{
    public abstract class NewPlantUnit 
    {
        // --- IDENTIFICATION ---

        public override string ToString()
        {
            return Name;
        }
        public Guid Id { get; private set; }
        public string Name { get; private set; }
        public ProcessEquipmentType Type { get; private set; }
        public FocusFactory FocusFactory { get; private set; }

        // --- KPI & METRICS ---
        public int PendingRequestsCount => _reservationQueue.Count; // CORREGIDO: Usa _reservationQueue
        public double SecondsTotal { get; private set; }
        public double SecondsAvailable { get; private set; }
        public double SecondsUtilized { get; private set; }
        private readonly Dictionary<string, ShiftMetric> _historicalMetrics = new();

        public double Availability => SecondsTotal == 0 ? 0 : (SecondsAvailable / SecondsTotal) * 100.0;
        public double Utilization => SecondsAvailable == 0 ? 0 : (SecondsUtilized / SecondsTotal) * 100.0;

        // --- TOPOLOGY ---
        private readonly HashSet<Guid> _inputIds = new();
        private readonly HashSet<Guid> _outputIds = new();
        private readonly List<NewPlantUnit> _connectedInputs = new();
        private readonly List<NewPlantUnit> _connectedOutputs = new();
        public IReadOnlyList<NewPlantUnit> Inputs => _connectedInputs;
        public IReadOnlyList<NewPlantUnit> Outputs => _connectedOutputs;
        // --- CAPABILITIES ---
        protected Dictionary<ProductDefinition, Amount> _productCapabilities = new();
        public Dictionary<ProductDefinition, Amount> ProductCapabilities => _productCapabilities;
        public List<ProductDefinition> SupportedProducts => _productCapabilities.Keys.ToList();
        public ProductDefinition? CurrentMaterial { get; set; }
        private List<PlannedDownTimeWindow> _scheduledBreaks = new();

        // --- STATE MACHINES ---
        public IGlobalStated GlobalState { get; protected set; } = null!;
        public INewInletState? InletState { get; protected set; }
        public INewOutletState? OutletState { get; protected set; }
        public ICaptureState CaptureState { get; protected set; } = null!;

        public DateTime CurrentDate { get; private set; }
        public virtual DateTime AvailableAt { get; set; } = DateTime.MinValue;
        public virtual Amount RemainingSeconds=> new Amount(pendingSecond, TimeUnits.Second);
        double pendingSecond;
        public void SetRemainingSeconds(double  seconds)
        {
            pendingSecond=seconds;

        }


        // --- RESOURCE LOCKING VARIABLES ---
        private NewPlantUnit? _currentOwner;
        public NewPlantUnit? CurrentOwner => _currentOwner;

        // NOTA: _capturedResource ya no es necesario con la nueva lógica, pero lo dejo si lo usas en UI
       

       
        protected readonly List<NewPlantUnit> _reservationQueue = new();

        private readonly HashSet<NewPlantUnit> _holdingResources = new();
        public IReadOnlyCollection<NewPlantUnit> HoldingResources => _holdingResources;

        // --- CONSTRUCTOR ---
        public NewPlantUnit(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory)
        {
            Id = id;
            Name = name;
            Type = type;
            FocusFactory = focusFactory;
            CaptureState = new NewUnitAvailableToCapture(this);
            GlobalState = new GlobalState_Operational(this);
        }

        // =========================================================
        // RESOURCE MANAGEMENT (CORE FIXES)
        // =========================================================

        public bool RequestAccess(NewPlantUnit requester)
        {
            // 1. Si ya soy el dueño, pase.
            if (_currentOwner == requester) return true;

            // 2. Verificamos turno en la cola de RESERVAS
            bool isQueueEmpty = _reservationQueue.Count == 0;
            bool amIFirst = _reservationQueue.Count > 0 && _reservationQueue[0].Id == requester.Id;

            // Si está libre Y (no hay fila O soy el primero)
            if (_currentOwner == null && (isQueueEmpty || amIFirst))
            {
                // 1. Te anotas en el libro de registro (Ahora eres el #0)
                Reserve(requester);

                // 2. Tomas el control físico
                AssignResource(requester);
                return true;
            }

            // 3. RETORNO FALSE PURO
            // NO reservamos automáticamente. El Mixer debe llamar a Reserve() manualmente.
            return false;
        }

        public void Reserve(NewPlantUnit requester)
        {
            if (!_reservationQueue.Any(x => x.Id == requester.Id))
            {
                _reservationQueue.Add(requester);
            }
        }

        public void CancelReservation(NewPlantUnit requester)
        {
            var item = _reservationQueue.FirstOrDefault(x => x.Id == requester.Id);
            if (item != null) _reservationQueue.Remove(item);
        }

        public void ReleaseAccess(NewPlantUnit requester)
        {
            if (_currentOwner == requester)
            {
                // 1. Rompemos el vínculo físico
                requester.RemoveHoldingResource(this);
                _currentOwner = null;

                // 2. Borramos la reserva (él era el #0, ahora la lista se recorre)
                CancelReservation(requester);

                // 3. Avisamos que el siguiente puede pasar
                ProcessNextInQueue();
            }
            else
            {
                // Si alguien que estaba esperando se arrepiente, solo quitamos su turno
                CancelReservation(requester);
            }
        }

        private void ProcessNextInQueue()
        {
            // Usamos _reservationQueue (la lista unificada)
            if (_reservationQueue.Count > 0)
            {
                // Miramos al nuevo primero (Mixer B), pero NO lo sacamos todavía.
                // Se quedará ahí "bloqueando" la lista mientras trabaja para el cálculo de tiempos.
                var nextCandidate = _reservationQueue[0];

                if (nextCandidate is NewPlantUnit unit)
                {
                    AssignResource(unit);
                }
            }
            else
            {
                // Nadie esperando
                _currentOwner = null;
                TransitionCaptureState(new NewUnitAvailableToCapture(this));
            }
        }

        private void AssignResource(NewPlantUnit newOwner)
        {
            _currentOwner = newOwner;
            newOwner.AddHoldingResource(this);

            // Despertador (Nueva Arquitectura)
            if (newOwner.GlobalState is GlobalState_MasterWaiting waitingState
                && waitingState.TargetResource == this)
            {
                newOwner.TransitionGlobalState(new GlobalState_Operational(newOwner));
            }

            TransitionCaptureState(new NewUnitCapturedBy(this));
        }

       

        // --- HELPERS DE RELACIONES ---
        // (Estaban comentados en tu código, aquí se habilitan para que funcione AssignResource)
        public void AddHoldingResource(NewPlantUnit resource) => _holdingResources.Add(resource);
        public void RemoveHoldingResource(NewPlantUnit resource) => _holdingResources.Remove(resource);
     


        public void AddInlet(Guid fromId) { if (!_inputIds.Contains(fromId)) _inputIds.Add(fromId); }
        public void AddOutlet(Guid toId) { if (!_outputIds.Contains(toId)) _outputIds.Add(toId); }

        public void PropagateMaterialToOutputPumpss()
        {
            foreach (var product in SupportedProducts)
            {
                foreach (var output in Outputs)
                {
                    if (output is NewPump pump)
                    {
                        pump.SetProductCapability(product, new Amount(0, MassFlowUnits.Kg_sg));
                    }
                }
            }
        }

        public void AddPlannedDownTime(TimeSpan start, TimeSpan end) => _scheduledBreaks.Add(new PlannedDownTimeWindow(start, end));

        public void SetProductCapability(ProductDefinition product, Amount capacity)
        {
            if (!_productCapabilities.ContainsKey(product)) _productCapabilities.Add(product, capacity);
            else _productCapabilities[product] = capacity;
        }

        public void Calculate(DateTime currentTime)
        {
            if (Name.Contains("4") || Name.Contains("G"))
            {

            }
            CurrentDate = currentTime;
            GlobalState?.CheckTransitions();
            ExecuteProcess();
            CheckStatus();
            CaptureState?.Calculate();
            UpdateKpis();
        }

        private void UpdateKpis()
        {
            // (Tu lógica original de KPIs aquí...)
            // Copiar tal cual la tenías, es correcta.
            var context = GetOperationalContext(CurrentDate);
            if (!_historicalMetrics.TryGetValue(context.Key, out var shiftMetric))
            {
                shiftMetric = new ShiftMetric(context.Label, context.OpDate, context.ShiftId);
                _historicalMetrics.Add(context.Key, shiftMetric);
            }
            SecondsTotal += 1.0;
            shiftMetric.AddTotal(1.0);

            if (GlobalState.IsOperational)
            {
                SecondsAvailable += 1.0;
                shiftMetric.AddAvailable(1.0);
                bool inletWorking = InletState?.IsProductive ?? false;
                bool outletWorking = OutletState?.IsProductive ?? false;
                if (inletWorking || outletWorking)
                {
                    SecondsUtilized += 1.0;
                    shiftMetric.AddUtilized(1.0);
                }
            }
        }

        // ... (Incluye aquí GetOperationalContext, ShiftContext record, CheckStatus, etc.) ...

        private record ShiftContext(string Key, string Label, DateTime OpDate, int ShiftId);
        private ShiftContext GetOperationalContext(DateTime now)
        {
            // (Tu lógica original de turnos...)
            int hour = now.Hour;
            DateTime operationalDate = now.Date;
            int shiftId = 0;
            if (hour >= 6 && hour < 14) shiftId = 1;
            else if (hour >= 14 && hour < 22) shiftId = 2;
            else
            {
                shiftId = 3;
                if (hour < 6) operationalDate = operationalDate.AddDays(-1);
            }
            string key = $"{operationalDate:yyyy-MM-dd}_S{shiftId}";
            string label = $"{operationalDate:MMM-dd} Shift {shiftId}";
            return new ShiftContext(key, label, operationalDate, shiftId);
        }

        public virtual void ExecuteProcess()
        {
            if (GlobalState.IsOperational)
            {
                InletState?.Calculate();
                OutletState?.Calculate();
            }
        }

        public void CheckStatus()
        {
            // (Tu lógica original de CheckStatus...)
            if (GlobalState.IsOperational)
            {
                var brokenResource = _holdingResources.FirstOrDefault(x => !x.GlobalState.IsOperational);
                if (brokenResource != null)
                {
                    TransitionGlobalState(new GlobalState_MasterWaiting(this, brokenResource));
                    return;
                }
                InletState?.CheckTransitions();
                OutletState?.CheckTransitions();
                CaptureState?.CheckTransitions();
            }
            else
            {
                GlobalState.CheckTransitions();
            }
        }

        public bool IsOnPlannedBreak(DateTime currentTime)
        {
            TimeOnly time = TimeOnly.FromDateTime(currentTime);
            return _scheduledBreaks.Any(window => window.IsInside(time));
        }

        public void TransitionGlobalState(IGlobalStated newState) => GlobalState = newState;
        public void TransitionInletState(INewInletState newState) => InletState = newState;
        public void TransitionOutletState(INewOutletState newState) => OutletState = newState;
        public void TransitionCaptureState(ICaptureState newState) => CaptureState = newState;

        public void WireUp(Dictionary<Guid, NewPlantUnit> allUnits)
        {
            _connectedInputs.Clear();
            foreach (var id in _inputIds) if (allUnits.TryGetValue(id, out var unit)) _connectedInputs.Add(unit);
            _connectedOutputs.Clear();
            foreach (var id in _outputIds) if (allUnits.TryGetValue(id, out var unit)) _connectedOutputs.Add(unit);
        }

        public virtual List<NewInstantaneousReport> Report
        {
            get
            {
                var list = new List<NewInstantaneousReport>
                {
                    new NewInstantaneousReport("Name", Name, IsBold: true, FontSize: "h6"),
                    new NewInstantaneousReport("Status", State, IsBold: true, Color: StateColor),
                };
                AddSpecificReportData(list);
                return list;
            }
        }

        public virtual string State
        {
            get
            {
                var parts = new List<string>();
                if(GlobalState.IsOperational)
                {
                    if (InletState != null) parts.Add(InletState.StateLabel);
                    if (OutletState != null) parts.Add(OutletState.StateLabel);
                    if (parts.Count == 0) return "Idle";
                }
                else
                {
                    parts.Add(GlobalState.StateLabel);
                }
               
                return string.Join(" | ", parts);
            }
        }

        public string StateColor
        {
            get
            {
                if (GlobalState is not GlobalState_Operational) return GlobalState.HexColor;
                return InletState?.HexColor ?? OutletState?.HexColor ?? "#9E9E9E";
            }
        }

        protected virtual void AddSpecificReportData(List<NewInstantaneousReport> reportList) { }
        public virtual void CheckInitialStatus(DateTime initialDate)
        {
            CurrentDate = initialDate;
            GlobalState = new GlobalState_Operational(this);
            GlobalState.CheckTransitions();
        }
        // En NewPlantUnit.cs
        public void TryReleaseFromBlock(NewPlantUnit caller)
        {
            if (GlobalState is GlobalState_SlaveBlocked blockedState)
            {
                // ¡VALIDACIÓN DE SEGURIDAD!
                // Solo te hago caso si tú eres quien me bloqueó.
                if (blockedState.Blocker == caller)
                {
                    if (IsOnPlannedBreak(CurrentDate))
                        TransitionGlobalState(new GlobalState_Maintenance(this));
                    else
                        TransitionGlobalState(new GlobalState_Operational(this));
                }
            }
        }
    }

    // ... Interfaces (IResourceRequester, etc.) se mantienen igual ...
    
    
    public interface IStateLabels
    {
        string StateLabel { get; }
        string HexColor { get; }
    }

    public interface IState : IStateLabels
    {
        void CheckTransitions();
    }





    public interface ICalculationState : IState
    {
        void Calculate();

        bool IsProductive { get; }
    }
    public interface INewInletState : ICalculationState { }

    public interface INewOutletState : ICalculationState
    {
        string SubStateName { get; }
    }




}


