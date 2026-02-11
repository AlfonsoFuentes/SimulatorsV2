using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{
    public class NewMixer : NewManufacture
    {
        public Dictionary<ProductDefinition, Amount> TheoricalBatchTime { get; private set; } = new();
        public double _DischargeRate => OutletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
        public NewPump? OutletPump { get; private set; }
        public NewPump? WashingPump { get; set; }
        public Amount CurrentLevel => new Amount(_currentLevel, MassUnits.KiloGram);
        public double _currentLevel;
        public Amount OperatorTimeDisabled { get; private set; } = new Amount(0, TimeUnits.Second);
        public OperatorEngagementType _OperatorOperationType { get; private set; } = OperatorEngagementType.Infinite;
        public void SetOperationOperatorTime(OperatorEngagementType type, Amount _TimeOperatorOcupy)
        {
            _OperatorOperationType = type;
            OperatorTimeDisabled = _TimeOperatorOcupy;
        }
        public NewRecipedInletTank? DestinationVessel { get; private set; }
        public WashoutMatrix WashoutRules { get; private set; }
        public ProductDefinition? LastMaterialProcessed { get; set; }
        public NewBatchManager BatchManager { get; private set; }
        public NewStepLink? CurrentStep => BatchManager.CurrentStep;
        public NewMixer(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory,
            WashoutMatrix _WashoutRules)
           : base(id, name, type, focusFactory)
        {

            WashoutRules = _WashoutRules;
            BatchManager = new NewBatchManager(this);
        }
        public override void CheckInitialStatus(DateTime initialDate)
        {
            base.CheckInitialStatus(initialDate);
            OutletPump = Outputs.OfType<NewPump>().FirstOrDefault();
            WashingPump = InletPumps.FirstOrDefault(x => x.IsForWashing);

        }
        public override void ReceiveStartCommand(NewRecipedInletTank wipTank)
        {
            if (wipTank?.CurrentMaterial == null) return;

            // 1. Determinar el "Predecesor" (Esto lo hiciste perfecto)
            ProductDefinition? predecessor;

            if (OutletState is not NewManufactureAvailableState)
            {
                predecessor = _wipTankQueue.Count == 0
                    ? CurrentMaterial
                    : _wipTankQueue.Last().CurrentMaterial;

                // Alimentamos la cinta del Manager
                BatchManager.AddOrder(wipTank.CurrentMaterial, predecessor, wipTank);

                // Guardamos el tanque en la cola física
                _wipTankQueue.Enqueue(wipTank);
            }
            else
            {
                predecessor = LastMaterialProcessed;

                // Alimentamos la cinta del Manager
                BatchManager.AddOrder(wipTank.CurrentMaterial, predecessor, wipTank);

                // OJO AQUÍ: En lugar de asignar CurrentMaterial a mano...
                // ...le pedimos al Mixer que ejecute lo que sea que esté de primero en la cinta.

                // Primero vinculamos el tanque (esto es físico, no de tiempo)
                DestinationVessel = wipTank;
                wipTank.AssignedMixer = this;
                // ¡Disparamos la lógica!
                ExecuteStartLogic(wipTank);
            }
        }
        public void RealeaseWipQueue()
        {
            DestinationVessel!.ReceiveStopDischargFromMixer();

            if (_wipTankQueue.Count > 0)
            {
                DestinationVessel = _wipTankQueue.Dequeue();
                CurrentMaterial = DestinationVessel.CurrentMaterial;

            }
        }
        public override void ReceiveStopCommand()
        {

            if (BatchManager.ActivePipeline.Count == 0)
                TransitionOutletState(new NewManufactureAvailableState(this));

        }

        protected override void ExecuteStartLogic(NewRecipedInletTank wipTank)
        {
            TransitionOutletState(new NewManufactureBatchingState(this));
            CurrentMaterial = wipTank.CurrentMaterial;
            BatchManager.StartPipeline();
        }
        public override void ExecuteProcess()
        {
            if (Name.Contains("G"))
            {

            }
            // CASO A: ESTOY TRABAJANDO (Luz Verde)
            // El equipo avanza física y lógicamente.
            if (GlobalState.IsOperational)
            {
                base.ExecuteProcess();
            }
            // CASO B: NO ESTOY TRABAJANDO (Cualquier Luz Roja/Naranja)
            // Ya sea por Mantenimiento, por Espera (MasterWaiting) o Bloqueo (SlaveBlocked).
            else
            {
                // 1. Verificamos si tengo una orden activa que se está retrasando
                var currentLink = BatchManager.CurrentStep;

                if (currentLink != null)
                {
                    // 2. LA MAGIA DEL TIEMPO ELÁSTICO
                    // Como el reloj avanza y yo no, empujo la fecha de entrega hacia el futuro.
                    currentLink.AccumulatedStarvation += 1;
                }
            }
        }
        public bool NeedsWashing(ProductDefinition? _last, ProductDefinition? _new)
        {
            if (_last == null || _new == null) return false;
            return _last.Id != _new.Id;
        }
        protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
        {
            var batch = BatchManager.CurrentBatch;
            var currentStep = BatchManager.CurrentStep;

            // --- 1. BLOQUE DE PRODUCTO (Contexto del Lote) ---
            if (batch != null)
            {
                reportList.Add(new NewInstantaneousReport("Active Batch", batch.ProductName, IsBold: true, Color: "#673AB7")); // Morado

                // Tiempos Amount (Teórico vs Real)
                double theoricalMin = batch.BatchTeorico / 60.0;
                double realMin = batch.BatchReal / 60.0;
                double starvedMin = (batch.BatchReal - batch.BatchTeorico) / 60.0;
                double willbefre = MixerWillbeFreeAt.GetValue(TimeUnits.Minute);
                reportList.Add(new NewInstantaneousReport("Time Theoretical", $"{theoricalMin:F1} min", Color: "#673AB7"));
                reportList.Add(new NewInstantaneousReport("Time Accumulated", $"{realMin:F1} min", IsBold: true, Color: "#673AB7"));

                if (starvedMin > 0.1)
                {
                    reportList.Add(new NewInstantaneousReport("Starved Delay", $"{starvedMin:F1} min", Color: "#F44336")); // Rojo si hay retraso

                }
                reportList.Add(new NewInstantaneousReport("Will be available in", $"{willbefre:F1} min", IsBold: false, Color: "#673AB7"));
            }
            else
            {
                reportList.Add(new NewInstantaneousReport("Product", "IDLE / STANDBY", Color: "#9E9E9E")); // Gris
            }

            // --- 2. BLOQUE FÍSICO (Nivel y Rendimiento) ---
            reportList.Add(new NewInstantaneousReport("Current Level", $"{_currentLevel:F1} kg", IsBold: true, Color: "#2196F3")); // Azul

            // Lógica de Semáforo para Utilización (AU)
            double util = this.Utilization;
            string colorUtil = util >= 80 ? "#4CAF50" : (util >= 50 ? "#FFC107" : "#F44336");
            reportList.Add(new NewInstantaneousReport("Utilization (AU)", $"{util:F1}%", IsBold: true, Color: colorUtil));

            // --- 3. BLOQUE DE EJECUCIÓN (El Paso Actual) ---
            if (currentStep != null)
            {
                reportList.Add(new NewInstantaneousReport("Current Step", currentStep.GetSubStatusMessage(), IsBold: true, Color: "#009688")); // Teal

                // Fecha estimada de liberación de ESTA orden
                reportList.Add(new NewInstantaneousReport("Batch Ends At", CurrentBatchReleaseTime.ToShortTimeString(), Color: "#009688"));
            }

            // --- 4. BLOQUE LOGÍSTICO (Cola y Destino) ---
            if (DestinationVessel != null)
            {
                reportList.Add(new NewInstantaneousReport("Destination", $"-> {DestinationVessel.Name}", Color: "#FF9800")); // Naranja
            }

            if (_wipTankQueue.Count > 0)
            {
                reportList.Add(new NewInstantaneousReport("Pipeline Queue", $"{_wipTankQueue.Count} Batches Waiting", IsBold: true, Color: "#E91E63")); // Rosa

                // Cuándo quedará libre el mixer de TODA la cola
                reportList.Add(new NewInstantaneousReport("Mixer Free At", MixerProjectedFreeTime.ToShortTimeString(), Color: "#E91E63"));
            }
        }
        public DateTime MixerProjectedFreeTime => BatchManager.MixerProjectedFreeTime;

        // Propiedad privada auxiliar (opcional, o puedes poner la lógica directa abajo)
        private TimeSpan _mixerTimeSpan => MixerProjectedFreeTime - CurrentDate;

        public Amount MixerWillbeFreeAt
        {
            get
            {
                double seconds = _mixerTimeSpan.TotalSeconds;

                // PROTECCIÓN: Si ya pasó la fecha (es negativo), devolvemos 0.
                // Significa "Estoy libre YA".
                if (seconds < 0) seconds = 0;

                return new Amount(seconds, TimeUnits.Second);
            }
        }
        public override DateTime CurrentBatchReleaseTime
        {
            get
            {

                return BatchManager.CurrentBatchProjectedEnd;     //necesito ayuda para saber el batche actual que fecha se libera
            }
        }
    }









}
