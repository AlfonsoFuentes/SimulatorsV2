using Simulator.Shared.Enums.HCEnums.Enums;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Lines;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Operators;
using Simulator.Shared.NuevaSimlationconQwen.Equipments.Pumps;
using Simulator.Shared.NuevaSimlationconQwen.ManufacturingOrders;
using Simulator.Shared.NuevaSimlationconQwen.Materials;
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

            CurrentBatchReport.DateReceivedToInitBatch = this.CurrentDate;
            CurrentBatchReport.BatchSize = newOrderMixer.BatchSize;
            CurrentBatchReport.TheroticalBatchTime = newOrderMixer.BatchTime;


            BatchReports.Add(CurrentBatchReport);
            ManufacturingOrders.Enqueue(newOrderMixer);
            order.AddMixerManufactureOrder(newOrderMixer);

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

        //public bool IsCurrentStepFeederAvailable()
        //{
        //    if (CurrentManufactureOrder == null || CurrentManufactureOrder.CurrentStep == null || !CurrentManufactureOrder.CurrentStep.RawMaterialId.HasValue) return false;
        //    var IsOperator = IsMaterialFeederOperator(CurrentManufactureOrder.CurrentStep.RawMaterialId.Value);
        //    var newFeeder = IsMaterialFeederAvailableForMixer(CurrentManufactureOrder.CurrentStep.RawMaterialId.Value);
        //    if (IsOperator && newFeeder == null)
        //    {
        //        //if (ProcessOperator != null && !ProcessOperator.OperatorHasNotRestrictionToInitBatch && ProcessOperator.OcuppiedBy == this)
        //        //{
        //        //    //El operador es el feeder  no hay que asignar ni encolar

        //        //}
        //        return true;
        //    }
        //    if (newFeeder != null)
        //    {

        //        SetFeeder(newFeeder);
        //        //Es bomba pero no es el operador o el operador es asignable porque no esta en uso en el mixer

        //        Feeder?.ActualFlow = Feeder.Flow;
        //        Feeder?.OcuppiedBy = this;
        //        Feeder?.OutletState = new FeederIsInUseByAnotherEquipmentState(Feeder);

        //        return true;

        //    }
        //    EnqueueForMaterialFeeder(CurrentManufactureOrder.CurrentStep.RawMaterialId.Value);


        //    return false;
        //}
        // En tu clase ProcessMixer.cs

        //public void ReleseCurrentMassStep()
        //{
        //    if (Feeder != null)
        //    {
        //        // 1. IMPORTANTE: Limpiar la ocupación en el tanque físico
        //        // Esto es lo que le dice al tanque "Ya no me uses"
        //        Feeder.OcuppiedBy = null!;

        //        // 2. Cambiar el estado del tanque a Disponible
        //        if (Feeder.OutletState is FeederIsInUseByAnotherEquipmentState)
        //        {
        //            Feeder.OutletState = new FeederAvailableState(Feeder);
        //        }

        //        // 3. Notificar al siguiente en la cola (en este caso, al Mixer B)
        //        Feeder.NotifyNextWaitingEquipment();

        //        // 4. Limpiar la referencia en el Mixer
        //        Feeder = null!;
        //    }
        //}
        //public void ReleseCurrentMassStep()
        //{

        //    if (Feeder != null)
        //    {
        //        ReleaseFeeder(Feeder);

        //    }
        //}
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

                // --- PARCHE CRÍTICO ---
                // Si el paso actual NO es manual (es decir, requiere una bomba/feeder real),
                // el operario ya fue capturado como personal, así que debemos liberar el slot de Feeder
                // para que la bomba de materia prima pueda ocuparlo.
                if (!IsCurrentStepManualAddition())
                {
                    if (Feeder == op)
                    {
                        SetFeeder(null!); // Vaciamos el slot de masa
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
            Amount totalWait = new Amount(0, TimeUnits.Minute);

            // 1. Tiempo del Lote Activo (Si hay uno procesándose)
            if (CurrentManufactureOrder != null)
            {
                totalWait += CurrentManufactureOrder.PendingBatchTime.ConvertedTo(TimeUnits.Minute);
            }

            // 2. Tiempo de Transferencia Activa (Si el mixer está bombeando masa ahora)
            if (CurrentTransferRequest != null)
            {
                var pendingMass = CurrentTransferRequest.PendingToReceive;
                var flowValue = (OutletPump?.Flow != null && OutletPump.Flow.Value > 0)
                                ? OutletPump.Flow.GetValue(MassFlowUnits.Kg_min)
                                : 1.0;

                totalWait += new Amount(pendingMass.GetValue(MassUnits.KiloGram) / flowValue, TimeUnits.Minute);
            }

            // --- PARCHE CORREGIDO: Sumar órdenes en cola ---
            // Iteramos por las órdenes que están esperando su turno en este Mixer
            foreach (var queuedOrder in ManufacturingOrders)
            {
                // Buscamos la receta (IEquipmentMaterial) asociada al material de la orden en este mixer
                var recipe = EquipmentMaterials.FirstOrDefault(x => x.Material.Id == queuedOrder.Material.Id);

                if (recipe != null)
                {
                    // Así estaba... (Error: queuedOrder no tiene .Recipe)
                    // totalWait += queuedOrder.Recipe.BatchCycleTime;

                    // 1. Sumamos el ciclo de batch completo (BatchCycleTime)
                    totalWait += recipe.BatchCycleTime;

                    // 2. Sumamos el tiempo de transferencia proyectado para esa orden
                    // $$Minutes = \frac{BatchSize}{Flow}$$
                    var flow = (OutletPump?.Flow != null && OutletPump.Flow.Value > 0)
                               ? OutletPump.Flow.GetValue(MassFlowUnits.Kg_min)
                               : 1.0;

                    double transferMinutes = queuedOrder.BatchSize.GetValue(MassUnits.KiloGram) / flow;
                    totalWait += new Amount(transferMinutes, TimeUnits.Minute);
                }
            }
          

            return totalWait;
           
        }
       
    }

}
