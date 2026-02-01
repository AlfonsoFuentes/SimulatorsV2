using QWENShared.BaseClases.Equipments;
using QWENShared.BaseClases.Material;
using QWENShared.Enums;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Lines;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Operators;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Pumps;
using Simulator.Shared.NuevaSimlationconQwen.ManufacturingOrders;
using Simulator.Shared.NuevaSimlationconQwen.Reports;

namespace Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers
{
    public partial class ProcessMixer : ManufaturingEquipment, ISetMaterialsAtOutlet, ILiveReportable
    {

        public List<BatchReport> BatchReports { get; set; } = new List<BatchReport>();
        public BatchReport? CurrentBatchReport { get; set; } = null!;
        public List<ProcessLine> PreferredLines { get; set; } = new List<ProcessLine>();
        public override Amount CurrentLevel { get; set; } = new Amount(0, MassUnits.KiloGram);

        public List<ProcessPump> InletPumps => InletEquipments.OfType<ProcessPump>().ToList();
        public List<ProcessOperator> InletOperators => InletEquipments.OfType<ProcessOperator>().ToList();

        public List<ProcessPump> OutletPumps => OutletEquipments.OfType<ProcessPump>().ToList();
        public ProcessPump? OutletPump => OutletPumps.FirstOrDefault();
        public bool IsMixerFree => CurrentManufactureOrder == null && ManufacturingOrders.Count == 0;
        public virtual void SetMaterialsAtOutlet(IMaterial material)
        {
            foreach (var outlet in OutletPumps)
            {
                outlet.AddMaterial(material);

            }
        }
        public override void OnInit(DateTime currentdate)
        {
            base.OnInit(currentdate);

            InletState = new MixerInletWaitingForManufactureOrderState(this);
            OutletState = new MixerOuletWaitingState(this);
        }
        public override void ReceiveManufactureOrderFromWIP(ITankManufactureOrder order)
        {
            CurrentBatchReport = new(this);
            MixerManufactureOrder newOrderMixer = new MixerManufactureOrder(this, order, CurrentBatchReport);
            var op = InletOperators.FirstOrDefault();
            if (op != null)
            {
                // Calculamos la duración manual proyectada (Lavado + Preparación)
                // Usamos la receta vinculada al material de la orden
                var recipe = EquipmentMaterials.FirstOrDefault(m => m.Material.Id == order.Material.Id);
                if (recipe != null)
                {

                    Amount manualDuration = GetWashoutTime(LastMaterial, recipe.Material) + recipe.BatchCycleTime;

                    // Añadimos la reserva a la agenda del operario inmediatamente
                    op.Agenda.Add(new OperatorWorkTask(this.Id, newOrderMixer.Id, manualDuration));
                }
            }
            CurrentBatchReport.DateReceivedToInitBatch = this.CurrentDate;
            CurrentBatchReport.BatchSize = newOrderMixer.BatchSize;
            CurrentBatchReport.BatchCycleTime = newOrderMixer.BatchCycleTime;


            BatchReports.Add(CurrentBatchReport);
            ManufacturingOrders.Enqueue(newOrderMixer);
            order.AddMixerManufactureOrder(newOrderMixer);

        }
        Amount GetWashoutTime(IMaterial? materialCurrent, IMaterial materialNext)
        {


            Amount washoutTime = new Amount(0, TimeUnits.Minute);
            if (materialCurrent != null)
            {

                var washoutDef = WashoutTimes
                                .FirstOrDefault(x => x.ProductCategoryCurrent == materialCurrent.ProductCategory &&
                                                   x.ProductCategoryNext == materialNext.ProductCategory);

                if (washoutDef != null)
                {
                    washoutTime = washoutDef.MixerWashoutTime;
                }
            }
            return washoutTime;
        }
        public void InitBatchDate()
        {
            CurrentBatchReport?.InitRealBatchDate(this.CurrentDate);
        }
        public bool InitBatchFromQueue()
        {
            if (CurrentManufactureOrder == null && CurrentTransferRequest == null && ManufacturingOrders.Count > 0)
            {
                CurrentLevel = new Amount(0, MassUnits.KiloGram);

                CurrentManufactureOrder = ManufacturingOrders.Dequeue();

                return true;
            }
            return false;
        }


        public TransferFromMixertoWIPOrder? CurrentTransferRequest { get; set; } = null;

        public void ReceiveTransferFinalizedFromWIP()
        {
            //Recibe esta informacion la procesa el manejador de estados
            if (CurrentTransferRequest != null)
            {

                CurrentManufactureOrder = null!;
                CurrentLevel = ZeroMass;
                CurrentTransferRequest = null;
                OutletState = new MixerOuletWaitingState(this);
                InletState = new MixerInletWaitingForManufactureOrderState(this);

            }

        }


        public void ReceiveReportOfMassDelivered(Amount massdelivered)
        {
            //Recibe esta informacion la procesa el manejador de estados
            if (CurrentTransferRequest != null)
            {
                CurrentLevel -= massdelivered;
            }

        }

        public bool IsMustWashTank()
        {
            if (CurrentManufactureOrder == null) return false;


            if (LastMaterial == null)
            {

                LastMaterial = CurrentManufactureOrder.Material;
                return false;
            }
            if (CurrentManufactureOrder.Material == null) return false;
            if (CurrentManufactureOrder.Material.Id == LastMaterial.Id) return false;

            var washDef = WashoutTimes
                .FirstOrDefault(x => x.ProductCategoryCurrent == CurrentManufactureOrder.Material?.ProductCategory &&
                                   x.ProductCategoryNext == LastMaterial.ProductCategory);


            if (washDef != null)
            {

                return true;
            }

            return false;
        }

        public Amount GetWashoutTime()
        {
            if (LastMaterial != null)
            {
                var washDef = WashoutTimes
                .FirstOrDefault(x => x.ProductCategoryCurrent == CurrentManufactureOrder.Material?.ProductCategory &&
                                   x.ProductCategoryNext == LastMaterial.ProductCategory);
                if (washDef != null)
                {
                    LastMaterial = CurrentManufactureOrder.Material;
                    return washDef.MixerWashoutTime;
                }
            }

            return new Amount(0, TimeUnits.Second);
        }



        public bool IsManufacturingRecipeFinished()
        {
            if (CurrentManufactureOrder != null && CurrentManufactureOrder.RecipeSteps.Any())
            {
                CurrentManufactureOrder.CurrentStep = CurrentManufactureOrder.RecipeSteps.Dequeue();

                return false;
            }
            SentNewTransferRequestToWIP();

            return true;
        }
        public bool IsCurrentStepByOperator()
        {
            if (CurrentManufactureOrder == null || CurrentManufactureOrder.CurrentStep == null) return false;
            if (CurrentManufactureOrder.CurrentStep.BackBoneStepType == BackBoneStepType.Adjust ||
                CurrentManufactureOrder.CurrentStep.BackBoneStepType == BackBoneStepType.Analisys ||
                CurrentManufactureOrder.CurrentStep.BackBoneStepType == BackBoneStepType.Connect_Mixer_WIP ||
                CurrentManufactureOrder.CurrentStep.BackBoneStepType == BackBoneStepType.Transfer_To_Drop)
            {

                return true;
            }
            return false;

        }
        public bool IsCurrentStepDifferentThanAdd()
        {
            if (CurrentManufactureOrder == null || CurrentManufactureOrder.CurrentStep == null) return false;
            if (CurrentManufactureOrder.CurrentStep.BackBoneStepType != BackBoneStepType.Add)
            {

                return true;
            }
            return false;

        }
        public bool IsCurrentStepIsAdd()
        {
            if (CurrentManufactureOrder == null || CurrentManufactureOrder.CurrentStep == null) return false;
            if (CurrentManufactureOrder.CurrentStep.BackBoneStepType == BackBoneStepType.Add)
            {
                return true;
            }

            return false;
        }
        public bool IsCurrentStepFeederAvailable()
        {
            if (CurrentManufactureOrder?.CurrentStep?.RawMaterialId == null) return false;
            SetFeeder(null!);
            var materialId = CurrentManufactureOrder.CurrentStep.RawMaterialId.Value;
            var IsOperatorSource = IsMaterialFeederOperator(materialId);
            var newFeeder = IsMaterialFeederAvailableForMixer(materialId);

            // Caso A: El material es manual (el operario ES la fuente de masa)
            if (IsOperatorSource)
            {
                if (CapturedOperator != null && CapturedOperator.OcuppiedBy == this)
                {
                    SetFeeder(CapturedOperator);
                    Feeder!.ActualFlow = new Amount(1, MassFlowUnits.Kg_sg);
                    Feeder!.OcuppiedBy = this;
                    Feeder!.OutletState = new FeederIsInUseByAnotherEquipmentState(Feeder);
                    return true;
                }

                return false;
            }

            // Caso B: El material es automático (Bomba/Agua)
            if (newFeeder != null)
            {
                SetFeeder(newFeeder);
                Feeder!.ActualFlow = Feeder.Flow;
                Feeder!.OcuppiedBy = this;
                Feeder!.OutletState = new FeederIsInUseByAnotherEquipmentState(Feeder);
                return true;
            }

            // Si llegamos aquí, la bomba de agua/materia prima está ocupada.
            // DEBEMOS limpiar el Feeder (por si tenía al operario pegado) antes de encolar.

            EnqueueForMaterialFeeder(materialId);

            return false;
        }
        public override void ReleaseFeeder(IManufactureFeeder feederToRelease)
        {
            if (feederToRelease != null)
            {
                // --- PARCHE DE SEGURIDAD PARA EL OPERARIO ---
                // Si el feeder es el operario que tengo capturado como personal...
                if (feederToRelease is ProcessOperator op && CapturedOperator == op)
                {
                    // Solo desconectamos la manguera de "masa"
                    // pero NO limpiamos OcuppiedBy ni notificamos a otros mixers.
                    if (this.Feeder == feederToRelease)
                    {


                        SetFeeder(null!);

                    }
                    return; // Salimos sin liberar al humano
                }

                // --- LIBERACIÓN NORMAL (Para Bombas y Tanques) ---
                feederToRelease.OcuppiedBy = null!;
                feederToRelease.OutletState = new FeederAvailableState(feederToRelease);
                feederToRelease.ActualFlow = ZeroFlow;

                // Notificar a la cola solo si el recurso quedó realmente libre
                feederToRelease.NotifyNextWaitingEquipment();

                if (this.Feeder == feederToRelease)
                {
                    SetFeeder(null!);
                }
            }
        }
        public int GetManufactureOrderCount()
        {
            int count = 0;
            if (this.CurrentManufactureOrder != null) count++;
            count += this.ManufacturingOrders.Count; // Asumiendo que tienes una lista de espera
            return count;
        }
        public void ReleaseCapturedOperator()
        {
            if (CapturedOperator != null)
            {

                var op = CapturedOperator;
                op.OcuppiedBy = null!;
                op.OutletState = new FeederAvailableState(op);
                op.NotifyNextWaitingEquipment();
                CapturedOperator = null;
            }
        }


        void SentNewTransferRequestToWIP()
        {
            if (CurrentManufactureOrder != null)
            {
                CurrentManufactureOrder.WIPOrder.RemoveManufactureOrdersFromMixers(CurrentManufactureOrder);

                TransferFromMixertoWIPOrder newTransferOrder =
                    new TransferFromMixertoWIPOrder(this, CurrentManufactureOrder.WIPOrder.Tank, CurrentManufactureOrder.BatchSize,
                    OutletPump?.Flow ?? new Amount(0, MassFlowUnits.Kg_min));
                CurrentManufactureOrder.WIPOrder.Tank.ReceiveTransferRequestFromMixer(newTransferOrder);
            }
        }




        public void ReceiveTransferOrderFromWIPToInit(TransferFromMixertoWIPOrder TransferRequest)
        {
            //El manejador de estados del mixer manejara esta informacion
            CurrentTransferRequest = TransferRequest;
            OutletState = new MixerOuletTransferingToWIPState(this);

        }
        public ProcessOperator? CapturedOperator { get; set; }

        public bool TryCaptureOperator()
        {
            var op = InletEquipments.OfType<ProcessOperator>().FirstOrDefault();
            if (op == null) return true;

            if (op.OutletState is FeederAvailableState || op.OcuppiedBy == this)
            {
                op.OcuppiedBy = this;
                op.OutletState = new FeederIsInUseByAnotherEquipmentState(op);
                CapturedOperator = op;


                if (!IsCurrentStepManualAddition())
                {
                    if (Feeder == op)
                    {
                        SetFeeder(null!); // Vaciamos el slot de masa
                    }
                    if (op.Agenda.FirstOrDefault(x => x.OrderId == CurrentManufactureOrder.Id) is OperatorWorkTask task)
                    {
                        op.Agenda.Remove(task);
                    }
                }
                return true;
            }

            op.EnqueueWaitingEquipment(this);
            return false;
        }
        public bool IsCurrentStepManualAddition()
        {
            // 1. Validaciones de seguridad para evitar NullReferenceException
            if (CurrentManufactureOrder?.CurrentStep == null) return false;

            var step = CurrentManufactureOrder.CurrentStep;

            // 2. Si el paso no tiene material (ej. un paso de agitación pura), no es adición manual
            if (!step.RawMaterialId.HasValue) return false;

            // 3. Usamos tu método existente para verificar si este material 
            // se entrega a través de un operario (Manual) o de una bomba (Automático)
            return IsMaterialFeederOperator(step.RawMaterialId.Value);
        }


        public bool IsCapturedOperatorOnBreak()
        {
            if (CapturedOperator == null) return false;

            // Verificamos si el operario entró en parada programada usando la lógica de Equipment
            return CapturedOperator.IsEquipmentInPlannedDownTimeState();
        }
        // Dentro de ProcessMixer.cs

        /// <summary>
        /// PARCHE: Indica cuánto tiempo (en minutos) le falta al mixer para estar libre 
        /// para una nueva orden, incluyendo el tiempo de transferencia actual.
        /// </summary>
        // Dentro de la clase parcial ProcessMixer

        // Dentro de la clase parcial ProcessMixer


        public Amount GetTimeUntilMixerIsReadyForNewOrder()
        {
            Amount physicalWait = new Amount(0, TimeUnits.Minute);

            // 1. ¿Está procesando un lote mecánicamente?
            if (CurrentManufactureOrder != null)
            {
                physicalWait += CurrentManufactureOrder.PendingBatchTime.ConvertedTo(TimeUnits.Minute);
                var pendingMass = CurrentManufactureOrder.BatchSize;
                var flowValue = (OutletPump?.Flow != null && OutletPump.Flow.Value > 0)
                                ? OutletPump.Flow.GetValue(MassFlowUnits.Kg_min)
                                : 1.0;

                physicalWait += new Amount(pendingMass.GetValue(MassUnits.KiloGram) / flowValue, TimeUnits.Minute);
            }

            // 2. ¿Está transfiriendo masa (bombeando) ahora?
            if (CurrentTransferRequest != null)
            {
                var pendingMass = CurrentTransferRequest.PendingToReceive;
                var flowValue = (OutletPump?.Flow != null && OutletPump.Flow.Value > 0)
                                ? OutletPump.Flow.GetValue(MassFlowUnits.Kg_min)
                                : 1.0;

                physicalWait += new Amount(pendingMass.GetValue(MassUnits.KiloGram) / flowValue, TimeUnits.Minute);
            }
            else
            {

            }

            return physicalWait;
        }
    }

}
