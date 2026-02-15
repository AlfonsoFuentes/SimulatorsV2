using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{
    public class NewMixer : NewManufacture
    {
        public List<NewLine> PreferedLines { get; private set; } = new List<NewLine>();
        public void AddPreferedLine(NewLine preferedLine)
        {
            PreferedLines.Add(preferedLine);
        }
        public List<NewStepLink> ExecutionHistory => BatchManager.BatchRecordHistory.ToList();
        public Dictionary<ProductDefinition, Amount> TheoricalBatchTime { get; private set; } = new();
        public double _DischargeRate => OutletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
        public NewPump? OutletPump { get; private set; }
        public NewPump? WashingPump { get; set; }
        public Amount CurrentLevel => new Amount(_currentLevel, MassUnits.KiloGram);
        public void SetCurrentLevel(double level) => _currentLevel = level;
        private double _currentLevel;
        public Amount OperatorTimeDisabled { get; private set; } = new Amount(0, TimeUnits.Second);
        public OperatorEngagementType OperatorOperationType { get; private set; } = OperatorEngagementType.Infinite;

        public NewRecipedInletTank? DestinationVessel { get; set; }
        public WashoutMatrix WashoutRules { get; private set; }
        public ProductDefinition? LastMaterialProcessed { get; set; }
        public NewBatchManager BatchManager { get; private set; }

        public NewMixer(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory,
            WashoutMatrix _WashoutRules, TransferBatchToMixerCalculation TransferBatchToMixerCalculation, OperatorEngagementType OperatorEngagementType, Amount OperatorTimeDisabled)
           : base(id, name, type, focusFactory)
        {

            WashoutRules = _WashoutRules;
            BatchManager = new NewBatchManager(this, TransferBatchToMixerCalculation, OperatorEngagementType, OperatorTimeDisabled);
            this.OperatorOperationType = OperatorEngagementType;
            this.OperatorTimeDisabled = OperatorTimeDisabled;
        }
        public void SetTransferBatchToMixerCalculation(TransferBatchToMixerCalculation calculationmode)
        {
            BatchManager.SetTransferBatchToMixerCalculation(calculationmode);
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
                predecessor = BatchManager.LastProduct;

                // Alimentamos la cinta del Manager
                BatchManager.AddOrder(wipTank, predecessor);

                // Guardamos el tanque en la cola física

            }
            else
            {
                predecessor = LastMaterialProcessed;
                DestinationVessel = wipTank;

                // Alimentamos la cinta del Manager
                BatchManager.AddOrder(wipTank, predecessor);


                // ¡Disparamos la lógica!
                ExecuteStartLogic(wipTank);
            }
        }
        public void RealeaseWipQueue()
        {
            DestinationVessel!.ReceiveStopDischargFromMixer();


        }
        public override void ReceiveStopCommand()
        {

            if (BatchManager.BatchQueue.Count == 0)
            {
                DestinationVessel = null;
                TransitionOutletState(new NewManufactureAvailableState(this));
            }


        }

        protected override void ExecuteStartLogic(NewRecipedInletTank wipTank)
        {

            BatchManager.StartPipeline();
            if (BatchManager.CurrentBatch != null)
            {
                TransitionOutletState(new NewManufactureBatchingState(this));

            }
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
                BatchManager.AddStarvation();
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

            reportList.Add(new NewInstantaneousReport("Current Level", $"{_currentLevel:F1} kg", IsBold: true, Color: "#2196F3")); // Azul
            reportList.Add(new NewInstantaneousReport("Current Step", SubStatusMessage, IsBold: true, Color: "#009688")); // Teal
            // --- 1. BLOQUE DE PRODUCTO (Contexto del Lote) ---
            if (batch != null)
            {
                reportList.Add(new NewInstantaneousReport("Active Batch", batch.ProductName, IsBold: true, Color: "#673AB7")); // Morado
                reportList.Add(new NewInstantaneousReport("Start Date", batch.StartBatchDate.ToString("g"), Color: "#673AB7"));
                reportList.Add(new NewInstantaneousReport("Theorical End Date", batch.PlannedTheoricalEndBatch.ToString("g"), Color: "#673AB7"));
                reportList.Add(new NewInstantaneousReport("Expected End Date", batch.PlannedEndBatch.ToString("g"), Color: "#673AB7"));
                // Tiempos Amount (Teórico vs Real)
                double theoricalMin = batch.BCT_Theorical.GetValue(TimeUnits.Minute);
                double expectedMin = batch.BCT_Expected.GetValue(TimeUnits.Minute);
                double bctcurrent = batch.BCT_Current.GetValue(TimeUnits.Minute);
                double currentStarved = batch.TimeStarved.GetValue(TimeUnits.Minute);

                reportList.Add(new NewInstantaneousReport("BCT Theoretical", $"{theoricalMin:F1} min", Color: "#673AB7"));
                reportList.Add(new NewInstantaneousReport("BCT Current", $"{bctcurrent:F1} min", IsBold: true, Color: "#673AB7"));
                reportList.Add(new NewInstantaneousReport("Starved Delay", $"{currentStarved:F1} min", Color: "#F44336")); // Rojo si hay retraso
                reportList.Add(new NewInstantaneousReport("BCT Expected", $"{expectedMin:F1} min", Color: "#673AB7"));


                reportList.Add(new NewInstantaneousReport("%AU Batch", $"{batch.AU_Performance:F1} %", Color: "#F44336")); // Rojo si hay retraso
            }
            else
            {
                reportList.Add(new NewInstantaneousReport("Product", "IDLE / STANDBY", Color: "#9E9E9E")); // Gris
            }
            if (BatchManager.BatchRecordHistory.Count > 0)
            {
                reportList.Add(new NewInstantaneousReport("%AU Mixer", $"{BatchManager.AU_Performance:F1} %", Color: "#F44336")); // Rojo si hay retraso
            }

            // --- 2. BLOQUE FÍSICO (Nivel y Rendimiento) ---


            // Lógica de Semáforo para Utilización (AU)



            // --- 4. BLOQUE LOGÍSTICO (Cola y Destino) ---
            if (DestinationVessel != null)
            {
                reportList.Add(new NewInstantaneousReport("Destination", $"-> {DestinationVessel.Name}", Color: "#FF9800")); // Naranja
            }

            if (BatchManager.BatchQueue.Count > 0)
            {
                int VesselCount = 1;
                reportList.Add(new NewInstantaneousReport("Queue", $"{BatchManager.BatchQueue.Count} Vessels Waiting", IsBold: true, Color: "#E91E63")); // Rosa
                foreach (var item in BatchManager.BatchQueue)
                {
                    reportList.Add(new NewInstantaneousReport($"{VesselCount}", item.Vessel.Name, IsBold: true, Color: "#E91E63")); // Rosa
                    VesselCount++;
                }


                // Cuándo quedará libre el mixer de TODA la cola

            }
            double mixerfree = MixerWillbeFreeAt.GetValue(TimeUnits.Minute);
            reportList.Add(new NewInstantaneousReport("Mixer Free At Date", MixerProjectedFreeTime.ToString("g"), Color: "#E91E63"));
            reportList.Add(new NewInstantaneousReport("Mixer Free At Time", $"{mixerfree:F1} min", IsBold: true, Color: "#673AB7"));
        }
        public DateTime MixerProjectedFreeTime => BatchManager.MixerProjectedFreeTime;

        // Propiedad privada auxiliar (opcional, o puedes poner la lógica directa abajo)


        public Amount MixerWillbeFreeAt => BatchManager.TotalTimeUntilFree;
        public DateTime CurrentBatchReleaseTime => BatchManager.CurrentBatchProjectedEndDate;
        public Amount CurrentBatchRealTime => BatchManager.CurrentBatchRealTime;

        public string SubStatusMessage => BatchManager.SubStatusMessage;
        public string StatusMessage => BatchManager.StatusMessage;
        // Añade esto dentro de public class NewMixer
        public double Progress => BatchManager.CurrentBatchProgress;
    }




}
