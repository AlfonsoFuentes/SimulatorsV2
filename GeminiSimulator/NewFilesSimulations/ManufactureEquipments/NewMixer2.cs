using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Operators;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.Enums;
using System.Collections;
using System.Diagnostics;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{
    //public class NewMixer2 : NewManufacture
    //{
    //    public override void CheckInitialStatus(DateTime initialDate)
    //    {
    //        base.CheckInitialStatus(initialDate);
    //        OutletPump = Outputs.OfType<NewPump>().FirstOrDefault();
    //        WashingPump = InletPumps.FirstOrDefault(x => x.IsForWashing);

    //    }
    //    public double ManualFillingFlowRate { get; set; } = 0.5;

    //    // --- PROPIEDADES ---
    //    public Amount Capacity { get; private set; } = null!;
    //    public double _DischargeRate => OutletPump?.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg) ?? 0;
    //    public NewPump? OutletPump { get; private set; }
    //    public NewPump? WashingPump { get; set; }
    //    public Amount CurrentLevel => new Amount(_currentLevel, MassUnits.KiloGram);
    //    public double _currentLevel;

    //    public double BatchTime => CurrentMaterial == null ? 0 : TheoricalBatchTime[CurrentMaterial].GetValue(TimeUnits.Minute);
    //    public Dictionary<ProductDefinition, Amount> TheoricalBatchTime { get; private set; } = new();


    //    public double _currentBatchSize = 0;
    //    public NewPump? _currentPump = null!;
    //    public NewRecipedInletTank? DestinationVessel { get; private set; }
    //    public int CurrentTotalSteps => CurrentMaterial?.RecipeSteps?.Count ?? 0;

    //    // --- CONFIGURACIÓN OPERARIO ---
    //    public Amount OperatorTimeDisabled { get; private set; } = new Amount(0, TimeUnits.Second);
    //    public OperatorEngagementType _OperatorOperationType { get; private set; } = OperatorEngagementType.Infinite;
    //    public void SetOperationOperatorTime(OperatorEngagementType type, Amount _TimeOperatorOcupy)
    //    {
    //        _OperatorOperationType = type;
    //        OperatorTimeDisabled = _TimeOperatorOcupy;
    //    }
    //    public WashoutMatrix WashoutRules { get; private set; }

    //    // Cola de pasos
    //    Queue<RecipeStep> _CurrentRecipeSteps = new();
    //    private readonly MixerOrderManager _orderManager;

    //    // Propiedad pública para que la UI de Blazor pueda ver la cola (Read-Only)
    //    public MixerOrderManager OrderManager => _orderManager;
    //    // En NewMixer.cs

    //    protected override void AddSpecificReportData(List<NewInstantaneousReport> reportList)
    //    {
    //        // 1. MATERIAL ACTUAL
    //        if (CurrentMaterial != null)
    //        {
    //            reportList.Add(new NewInstantaneousReport("Product", CurrentMaterial.Name, IsBold: true, Color: "#673AB7")); // Morado
    //            reportList.Add(new NewInstantaneousReport("BCT Theorical", $"{Math.Round(TheoricalBatchTime[CurrentMaterial].GetValue(TimeUnits.Minute), 1)}, min", IsBold: false, Color: "#673AB7")); // Morado
    //            reportList.Add(new NewInstantaneousReport("BCT Real", $"{Math.Round(_orderManager.CurrentBatchTime.GetValue(TimeUnits.Minute))}, min", IsBold: false, Color: "#673AB7")); // Morado
    //            reportList.Add(new NewInstantaneousReport("Starved time", $"{Math.Round(_orderManager.CurrentBatchStarved.GetValue(TimeUnits.Minute))}, min", IsBold: false, Color: "#673AB7")); // Morado
    //        }
    //        else
    //        {
    //            reportList.Add(new NewInstantaneousReport("Product", "Empty", Color: "#BDBDBD"));
    //        }

    //        // 2. NIVEL / BATCH
    //        reportList.Add(new NewInstantaneousReport("Batch Level", $"{_currentLevel:F1} kg", Color: "#2196F3"));
    //        double util = this.Utilization;

    //        // Color Semáforo:
    //        // Verde > 80%, Amarillo > 50%, Rojo < 50%
    //        string colorUtil = util >= 80 ? "#4CAF50" : (util >= 50 ? "#FFC107" : "#F44336");

    //        reportList.Add(new NewInstantaneousReport("% AU", $"{util:F1}%", IsBold: true, Color: colorUtil));
    //        // 3. DESTINO (Para quién estoy produciendo)
    //        if (DestinationVessel != null)
    //        {
    //            reportList.Add(new NewInstantaneousReport("Target", $"-> {DestinationVessel.Name}", IsBold: true, Color: "#FF9800"));
    //        }

    //        // 4. COLA DE ESPERA (WIPs esperando turno)
    //        // Necesitas exponer _wipTankQueue o usar PendingRequestsCount
    //        if (_wipTankQueue.Count > 0)
    //        {
    //            reportList.Add(new NewInstantaneousReport("Queue", $"{_wipTankQueue.Count} Tanks Waiting", Color: "#E91E63"));

    //            // Opcional: Mostrar el nombre del siguiente
    //            var next = _wipTankQueue.Peek();
    //            reportList.Add(new NewInstantaneousReport("Next", next.Name, FontSize: "0.75rem"));
    //        }

    //        // 5. PASO ACTUAL (Receta)
    //        if (_orderManager.CurrentActivity != null)
    //        {
    //            if (_orderManager.CurrentActivity.Type == MixerActivityType.RecipeStepAdd ||
    //                _orderManager.CurrentActivity.Type == MixerActivityType.RecipeStepTime ||
    //                _orderManager.CurrentActivity.Type == MixerActivityType.Washout)
    //            {
    //                reportList.Add(new NewInstantaneousReport("Step Status", OutletState?.SubStateName ?? string.Empty, Color: "#009688"));
    //            }
    //            else
    //            {
    //                reportList.Add(new NewInstantaneousReport("Step Status", _orderManager.CurrentActivity?.ToString() ?? string.Empty, Color: "#009688"));
    //            }
    //            reportList.Add(new NewInstantaneousReport("Time to Get Free", $"{Math.Round(MixerWillbeFreeAt.GetValue(TimeUnits.Minute), 1)}, min", Color: "#009688"));
    //        }



    //    }
    //    // --- CONSTRUCTOR ---
    //    public NewMixer2(Guid id, string name, ProcessEquipmentType type, FocusFactory focusFactory,
    //         WashoutMatrix _WashoutRules)
    //        : base(id, name, type, focusFactory)
    //    {

    //        WashoutRules = _WashoutRules;
    //        _orderManager = new MixerOrderManager(this);
    //    }
    //    // En NewMixer.cs

    //    public override void ExecuteProcess()
    //    {
    //        if (Name.Contains("G"))
    //        {

    //        }
    //        // CASO A: ESTOY TRABAJANDO (Luz Verde)
    //        // El equipo avanza física y lógicamente.
    //        if (GlobalState.IsOperational)
    //        {
    //            base.ExecuteProcess();
    //        }
    //        // CASO B: NO ESTOY TRABAJANDO (Cualquier Luz Roja/Naranja)
    //        // Ya sea por Mantenimiento, por Espera (MasterWaiting) o Bloqueo (SlaveBlocked).
    //        else
    //        {
    //            // 1. Verificamos si tengo una orden activa que se está retrasando
    //            var currentLink = _orderManager.CurrentActivity;

    //            if (currentLink != null)
    //            {
    //                // 2. LA MAGIA DEL TIEMPO ELÁSTICO
    //                // Como el reloj avanza y yo no, empujo la fecha de entrega hacia el futuro.
    //                currentLink.AccumulatedStarvation += 1;
    //            }
    //        }
    //    }
    //    public override void ReceiveStartCommand(NewRecipedInletTank wipTank)
    //    {
    //        if (wipTank?.CurrentMaterial == null) return;

    //        // 1. Determinar el "Predecesor" (Esto lo hiciste perfecto)
    //        ProductDefinition? predecessor;

    //        if (OutletState is not NewManufactureAvailableState)
    //        {
    //            predecessor = _wipTankQueue.Count == 0
    //                ? CurrentMaterial
    //                : _wipTankQueue.Last().CurrentMaterial;

    //            // Alimentamos la cinta del Manager
    //            CreateOrder(wipTank.CurrentMaterial, predecessor);

    //            // Guardamos el tanque en la cola física
    //            _wipTankQueue.Enqueue(wipTank);
    //        }
    //        else
    //        {
    //            predecessor = LastMaterialProcessed;

    //            // Alimentamos la cinta del Manager
    //            CreateOrder(wipTank.CurrentMaterial, predecessor);

    //            // OJO AQUÍ: En lugar de asignar CurrentMaterial a mano...
    //            // ...le pedimos al Mixer que ejecute lo que sea que esté de primero en la cinta.

    //            // Primero vinculamos el tanque (esto es físico, no de tiempo)
    //            DestinationVessel = wipTank;

    //            // ¡Disparamos la lógica!
    //            ExecuteStartLogic(wipTank);
    //        }
    //    }
    //    public void CreateOrder(ProductDefinition material, ProductDefinition? lastMaterial)
    //    {
    //        // 1. Identificador único para este lote (Batch)
    //        Guid batchId = Guid.NewGuid();


    //        // Solo asignamos tiempo si la configuración es 'StartOnDefinedTime'

    //        if (_OperatorOperationType != OperatorEngagementType.Infinite)
    //        {
    //            if (AssignedOperator!.RequestAccess(this))
    //            {
    //                if (_OperatorOperationType == OperatorEngagementType.StartOnDefinedTime)
    //                {
    //                    AssignedOperator.TransitionCaptureState(new NewUnitTimedTaskState(AssignedOperator, this, OperatorTimeDisabled));
    //                }
    //            }
    //            else
    //            {
    //                AssignedOperator.Reserve(this);
    //            }


    //        }

    //        // B. Tiempo de Lavado
    //        // Comparamos el material que llega con el último que estuvo (o estará) en el equipo
    //        double washoutTime = 0;
    //        if (NeedsWashing(lastMaterial, material))
    //        {
    //            washoutTime = WashoutRules
    //               .GetMixerWashout(lastMaterial!.Category, material.Category)
    //               .GetValue(TimeUnits.Second);
    //        }

    //        // C. Tiempo de Transferencia (Descarga)
    //        // Basado en la capacidad del producto y la velocidad de descarga (_DischargeRate)
    //        double batchCapacityKilos = _productCapabilities[material].GetValue(MassUnits.KiloGram);
    //        double transferTime = _DischargeRate == 0 ? 0 : batchCapacityKilos / _DischargeRate;

    //        // 6. REGISTRO EN EL MANAGER
    //        // Enviamos el startAnchor para que la cinta se cree desplazada en el tiempo si es necesario
    //        _orderManager.AddOrder(
    //            batchId,
    //            AssignedOperator,   ///aqui no debe ser un tiempo definido si no un link al operario el operario nos dara la fecha final cuando estara libre
    //            washoutTime,
    //            material.RecipeSteps,
    //            transferTime, material
    //        );
    //    }
    //    protected override void ExecuteStartLogic(NewRecipedInletTank wipTank)
    //    {
    //        if (wipTank == null || wipTank.CurrentMaterial == null) return;

    //        // 1. VINCULACIÓN FÍSICA
    //        // El 'Iron' (hierro) se conecta: el tanque se pone en el mixer
    //        DestinationVessel = wipTank;
    //        CurrentMaterial = wipTank.CurrentMaterial;

    //        // 2. CONFIGURACIÓN DE CAPACIDAD
    //        // Estos valores son necesarios para que el simulador sepa cuánto pesar
    //        Capacity = _productCapabilities[CurrentMaterial];
    //        _currentBatchSize = Capacity.GetValue(MassUnits.KiloGram);

    //        // 3. RESET DE PUNTEROS FÍSICOS
    //        _currentLevel = 0;
    //        // Ya no llenamos _CurrentRecipeSteps aquí, porque los pasos 
    //        // están dentro de la cinta del OrderManager.

    //        // 4. DISPARO DE LA CINTA
    //        // En lugar de llamar a 'CheckWashingAndStart', llamamos a AdvanceToNextStep.
    //        // Este método mirará la ActivePipeline y dirá: 
    //        // "Ah, lo primero es el Operario" o "Lo primero es el Lavado".
    //        AdvanceToNextStep();
    //    }
    //    private Guid? _lastExecutedBatchId; // Para saber qué lote procesamos por última vez
    //    public void AdvanceToNextStep()
    //    {
    //        // 1. Consultamos el "frente" de la cinta en el Manager
    //        var activity = _orderManager.CurrentActivity;

    //        // 2. Si la cinta está vacía, el Mixer vuelve a estar disponible
    //        if (activity == null)
    //        {
    //            TransitionOutletState(new NewManufactureAvailableState(this));
    //            return;
    //        }
    //        UpdateMixerBatchContext(activity);

    //        // 3. Sincronización del Contexto (Batch)
    //        // Antes de cambiar de estado, nos aseguramos de que el Mixer 
    //        // sepa qué Batch y qué Material está procesando.


    //        // 4. El Gran Selector (Switch por Enum)
    //        switch (activity.Type)
    //        {
    //            case MixerActivityType.OperatorSetup:
    //                CheckOperator(activity);
    //                break;

    //            case MixerActivityType.Washout:
    //                {
    //                    CheckWashingAndStart(activity);


    //                }

    //                break;

    //            case MixerActivityType.RecipeStepAdd:

    //                {
    //                    CheckAddingMass(activity!);

    //                }
    //                break;

    //            case MixerActivityType.Transfer:
    //                CheckTransfer(activity!);


    //                break;
    //            case MixerActivityType.RecipeStepTime:
    //                {
    //                    TransitionOutletState(new NewMixerProcessingTime(this, activity!));

    //                }
    //                break;

    //        }
    //    }
    //    public void FinishCurrentStep()
    //    {
    //        // 1. Sacamos el papelito de la cinta (Dequeue)
    //        _orderManager.CompleteCurrentActivity();

    //        // 2. Avanzamos al siguiente
    //        AdvanceToNextStep();
    //    }
    //    private void UpdateMixerBatchContext(MixerActivityLink activity)
    //    {
    //        // 1. ¿Es la primera vez o cambió el lote?
    //        if (_lastExecutedBatchId != activity.BatchId)
    //        {
    //            _lastExecutedBatchId = activity.BatchId;

    //            // 2. Sincronización con la cola física
    //            // Si hay un tanque esperando, el primero de la fila (Peek) 
    //            // es el dueño de esta nueva actividad en la cinta.
    //            if (_wipTankQueue.Count > 0)
    //            {
    //                var currentWip = _wipTankQueue.Peek();

    //                // 3. Actualizamos el "hierro" del Mixer con los datos del nuevo producto
    //                CurrentMaterial = currentWip.CurrentMaterial;

    //                // Actualizamos capacidad y tamaño de batch para la física
    //                Capacity = _productCapabilities[CurrentMaterial!];
    //                _currentBatchSize = Capacity.GetValue(MassUnits.KiloGram);

    //                // (Opcional) Aquí podrías registrar en un log: "Iniciando Batch: {activity.BatchId}"
    //            }
    //        }
    //    }
    //    void CheckOperator(MixerActivityLink activity)
    //    {
    //        // CASO 1: Operador Virtual (Infinito)
    //        // No requiere gestión, pasa directo.
    //        if (_OperatorOperationType == OperatorEngagementType.Infinite)
    //        {
    //            FinishCurrentStep();
    //            return;
    //        }

    //        // CASO 2: YA LO TENGO (Soy el Dueño)
    //        // El operario ya fue asignado a mí en algún paso anterior o por AssignResource.
    //        if (AssignedOperator?.CurrentOwner == this)
    //        {
    //            FinishCurrentStep();
    //            return;
    //        }

    //        // CASO 3: NO LO TENGO (Está ocupado con otro)
    //        // Aquí aplicamos la Nueva Arquitectura.
    //        if (AssignedOperator != null)
    //        {
    //            // A. ¡PIDO TURNO! (Vital)
    //            // Me aseguro de estar en su cola de reservas. 
    //            // Si no hago esto, cuando se libere no vendrá a mí.
    //            AssignedOperator.Reserve(this);

    //            // B. ME DUERMO (MasterWaiting)
    //            // "Estoy esperando a mi recurso (El Operador)".
    //            // Cuando el operario se libere, ejecutará AssignResource(mí),
    //            // verá que estoy en este estado y me despertará (Operational).
    //            TransitionGlobalState(
    //                new GlobalState_MasterWaiting(this, AssignedOperator)
    //            );
    //        }
    //    }
    //    void CheckWashingAndStart(MixerActivityLink activity)
    //    {
    //        // 1. ¿Necesito Lavar?
    //        if (NeedsWashing(LastMaterialProcessed, CurrentMaterial))
    //        {
    //            if (WashingPump != null)
    //            {
    //                // INTENCIÓN: Configuro mi cerebro para "Modo Lavado".
    //                TransitionOutletState(new NewMixerManagingWashing(this, activity));

    //                // REALIDAD: ¿La bomba está libre para mí?
    //                if (!WashingPump.RequestAccess(this))
    //                {
    //                    // A. ¡PIDO TURNO! (Vital para que me avisen luego)
    //                    WashingPump.Reserve(this);

    //                    // B. ME DUERMO (Estado Naranja)
    //                    // "Estoy esperando a mi recurso (La Bomba de Lavado)".
    //                    // La bomba me despertará (Operational) cuando termine con el otro.
    //                    TransitionGlobalState(
    //                        new GlobalState_MasterWaiting(this, WashingPump)
    //                    );
    //                }

    //                // Si RequestAccess devolvió true, sigo Operacional (Verde).
    //                // El estado NewMixerManagingWashing descontará el tiempo en el Calculate().

    //                return;
    //            }
    //        }

    //        // 2. Si no hay lavado, pasamos directo al siguiente paso.
    //        FinishCurrentStep();
    //    }
    //    void CheckAddingMass(MixerActivityLink activity)
    //    {
    //        var ingredientId = activity?.StepDefinition?.IngredientId;

    //        // 1. BUSCAR BOMBAS VÁLIDAS
    //        var validPumps = InletPumps
    //            .Where(p => p.SupportedProducts.Any(prod => prod.Id == ingredientId))
    //            .ToList();

    //        // CASO A: HAY BOMBAS COMPATIBLES (Automático)
    //        if (validPumps.Any())
    //        {
    //            // 2. ESTRATEGIA DE SELECCIÓN (Optimizada)
    //            // Ordenamos por:
    //            //  A. ¿Está Libre? (Operational + CaptureAvailable) -> Pone los True primero.
    //            //  B. ¿Quién tiene menos cola? -> Desempata por carga de trabajo.
    //            var selectedPump = validPumps
    //                .OrderByDescending(p => p.GlobalState.IsOperational && p.CaptureState is NewUnitAvailableToCapture)
    //                .ThenBy(p => p.PendingRequestsCount)
    //                .First(); // Seguro llamar First() porque validPumps.Any() es true.

    //            _currentPump = selectedPump;

    //            // 3. INTENCIÓN (Outlet State)
    //            // Configuramos el cerebro para "Modo Bombeo".
    //            TransitionOutletState(new NewMixerFillingWithPump(this, activity!));

    //            // 4. REALIDAD (Global State)
    //            // Intentamos tomar posesión física.
    //            if (!_currentPump.RequestAccess(this))
    //            {
    //                // FALLO: La bomba está ocupada.

    //                // A. ¡PIDO TURNO! (Vital)
    //                _currentPump.Reserve(this);

    //                // B. ME DUERMO (MasterWaiting)
    //                // "Estoy esperando a mi recurso (La Bomba)".
    //                // Cuando la bomba se libere, ejecutará AssignResource(mí) y me despertará.
    //                TransitionGlobalState(
    //                    new GlobalState_MasterWaiting(this, _currentPump)
    //                );
    //            }

    //            // ÉXITO: Si RequestAccess es true, seguimos en Verde (Operational).
    //            // El estado FillingWithPump empezará a sumar flujo en el Calculate().
    //        }
    //        // CASO B: NO HAY BOMBAS (Manual)
    //        else
    //        {
    //            // INTENCIÓN: Adición Manual (Operario Simulado).
    //            TransitionOutletState(new NewMixerFillingManual(this, activity!));

    //            // No tocamos el GlobalState. Seguimos Operational (Verde).
    //            // (Asumimos que el operario manual siempre está listo o es "infinito" en este contexto).
    //        }
    //    }
    //    void CheckTransfer(MixerActivityLink activity) // Corregí el typo "Trasnfer" -> "Transfer"
    //    {
    //        // 1. GESTIÓN DEL OPERARIO
    //        // Si el operario estaba "atrapado" para todo el batch, este es el momento de liberarlo
    //        // (Asumiendo que la transferencia es automática o que el operario ya cumplió su parte).
    //        if (_OperatorOperationType == OperatorEngagementType.FullBatch)
    //        {
    //            AssignedOperator?.ReleaseAccess(this);
    //        }

    //        if (DestinationVessel != null)
    //        {
    //            // 2. INTENCIÓN (Outlet State)
    //            // Configuro mi cerebro: "Estoy Descargando".
    //            TransitionOutletState(new NewMixerDischarging(this, activity));

    //            // 3. REALIDAD (Global State)
    //            // Intento tomar posesión del Tanque.
    //            if (!DestinationVessel.RequestAccess(this))
    //            {
    //                // A. ¡PIDO TURNO! (Vital)
    //                // Me anoto en la lista del tanque.
    //                DestinationVessel.Reserve(this);

    //                // B. ME DUERMO (MasterWaiting - Naranja)
    //                // "Estoy esperando a mi recurso (El Tanque de Destino)".
    //                // Cuando el tanque se vacíe o se libere, me llamará y me despertará.
    //                TransitionGlobalState(
    //                    new GlobalState_MasterWaiting(this, DestinationVessel)
    //                );
    //            }

    //            // Si RequestAccess fue true, seguimos en Verde (Operational) 
    //            // y el Calculate() de NewMixerDischarging empezará a mover fluido.
    //        }
    //    }

    //    // --- AGREGAR ESTOS HELPERS QUE FALTABAN EN TU CÓDIGO PEGADO ---
    //    public ProductDefinition? LastMaterialProcessed { get; set; }
    //    public bool NeedsWashing(ProductDefinition? _last, ProductDefinition? _new)
    //    {
    //        if (_last == null || _new == null) return false;
    //        return _last.Id != _new.Id;
    //    }

    //    public RecipeStep? GetCurrentStep()
    //    {
    //        if (_CurrentRecipeSteps.Count == 0) return null;
    //        return _CurrentRecipeSteps.Dequeue();
    //    }



    //    // CRUCIAL: Liberar operario FullBatch al detenerse
    //    public override void ReceiveStopCommand()
    //    {
    //        // 1. Liberar downstream
    //        DestinationVessel?.SetInletFlow(0);
    //        DestinationVessel?.ReleaseAccess(this);

    //        _orderManager.CompleteCurrentActivity();

    //        // 3. Reset variables
    //        LastMaterialProcessed = CurrentMaterial;
    //        _currentLevel = 0;
    //        CurrentMaterial = null;

    //        // 4. Siguiente en cola o dormir
    //        if (_wipTankQueue.Count > 0)
    //        {
    //            var next = _wipTankQueue.Dequeue();
    //            ExecuteStartLogic(next);
    //            return;
    //        }
    //        TransitionOutletState(new NewManufactureAvailableState(this));
    //    }
    //    public DateTime MixerProjectedFreeTime => _orderManager.MixerProjectedFreeTime;

    //    // Propiedad privada auxiliar (opcional, o puedes poner la lógica directa abajo)
    //    private TimeSpan _mixerTimeSpan => MixerProjectedFreeTime - CurrentDate;

    //    public Amount MixerWillbeFreeAt
    //    {
    //        get
    //        {
    //            double seconds = _mixerTimeSpan.TotalSeconds;

    //            // PROTECCIÓN: Si ya pasó la fecha (es negativo), devolvemos 0.
    //            // Significa "Estoy libre YA".
    //            if (seconds < 0) seconds = 0;

    //            return new Amount(seconds, TimeUnits.Second);
    //        }
    //    }
    //    public override DateTime CurrentBatchReleaseTime
    //    {
    //        get
    //        {
    //            // Aquí conectamos con tu lógica existente de 'ProjectedFreeTime'
    //            // Si el Mixer reserva una Bomba, le dirá a la Bomba: 
    //            // "Te soltaré cuando termine mi lote actual".
    //            return OrderManager.CurrentBatchPlannedEnd;
    //        }
    //    }
        
    //}





    //public class NewMixerFillingWithPump : INewOutletState
    //{

    //    private double _initialLevel;
    //    private string _ingredientName = "";

    //    private readonly NewMixer _mixer;
    //    private readonly MixerActivityLink _activity; // <--- Guardamos la referencia al Link
    //    private double _secondsElapsed = 0;
    //    public bool IsProductive => true;
    //    public string HexColor => "#2196F3";

    //    double pumpNominalFlow = 0;

    //    private double _targetMass;

    //    // CONSTRUCTOR ACTUALIZADO: Recibe MixerActivityLink
    //    public NewMixerFillingWithPump(NewMixer m, MixerActivityLink activity)
    //    {
    //        _mixer = m;
    //        _activity = activity; // Lo guardamos para usarlo después

    //        // Sacamos la definición del paso desde el Link
    //        var stepDef = _activity.StepDefinition;

    //        _ingredientName = stepDef?.IngredientName ?? "Unknown";

    //        // Calculamos la masa basándonos en el Link y el tamaño del batch actual
    //        // Nota: stepDef.TargetPercentage viene del Link
    //        double percentage = stepDef?.TargetPercentage ?? 0;
    //        _targetMass = _mixer._currentBatchSize * percentage / 100.0;

    //        _initialLevel = _mixer._currentLevel;

    //        if (_mixer._currentPump != null)
    //        {
    //            pumpNominalFlow = _mixer._currentPump.NominalFlowRate.GetValue(MassFlowUnits.Kg_sg);
    //        }
           


    //    }

    //    public string StateLabel => $"{_activity.Order}-{_mixer.CurrentTotalSteps} - Adding {_ingredientName}";

    //    double PendingMass => _targetMass - CurrentMass;
    //    public string SubStateName => $"{CurrentMass:F2} / {_targetMass:F2} kg";

    //    public double CurrentMass = 0;
    //    public void Calculate()
    //    {
    //        if (_mixer._currentPump == null) return;

    //        _secondsElapsed += 1.0;

    //        // --- CORRECCIÓN 1: Calcular cuánto necesito pedir ---
     

    //        // Pedimos el flujo nominal de la bomba, o lo que falte si es menos
    //        double requestedFlow = (PendingMass < pumpNominalFlow) ? PendingMass : pumpNominalFlow;

    //        // --- CORRECCIÓN 2: Ordenar a la bomba que trabaje ---
    //        _mixer._currentPump.SetCurrentFlow(requestedFlow);
    //        CurrentMass += requestedFlow;


    //        // ¡Aquí actualizamos la masa! Si no, nunca terminamos.
    //        _mixer._currentLevel += requestedFlow;



    //        // 3. ACTUALIZAMOS EL TICKET

    //    }

    //    public void CheckTransitions()
    //    {
    //        if (PendingMass <= 0.001)
    //        {
    //            _mixer._currentPump?.SetCurrentFlow(0);
    //            _mixer._currentPump?.ReleaseAccess(_mixer);
    //            _mixer._currentPump = null;

    //            // IMPORTANTE: Guardamos el tiempo real en el Link antes de terminar
    //            // Esto sirve para tus reportes luego.


    //            // Finalizamos
    //            _mixer.FinishCurrentStep();
    //        }
    //    }
    //}
    //// En NewMixerFillingManual.cs

    //public class NewMixerFillingManual : INewOutletState
    //{
    //    private double _targetMass;
    //    double flow = 0;
    //    double currentMass = 0;
    //    double PendingMass => _targetMass - currentMass;
    //    string IngredientName = "";

    //    NewMixer _mixer;
    //    private readonly MixerActivityLink _activity; // <--- Referencia

    //    public bool IsProductive => true;
    //    public string HexColor => "#9C27B0"; // Púrpura para manual

    //    // CONSTRUCTOR ACTUALIZADO
    //    public NewMixerFillingManual(NewMixer m, MixerActivityLink activity)
    //    {
    //        _mixer = m;
    //        _activity = activity;

    //        var stepDef = _activity.StepDefinition;
    //        IngredientName = stepDef?.IngredientName ?? "Unknown";

    //        // Usamos el Link para calcular la meta
    //        double percentage = stepDef?.TargetPercentage ?? 0;
    //        _targetMass = _mixer._currentBatchSize * percentage / 100.0;
    //    }

    //    public string StateLabel => $"{_activity.Order}-{_mixer.CurrentTotalSteps} - Manual Add {IngredientName}";
    //    public string SubStateName => $"{currentMass:F2} / {_targetMass:F2} kg";

    //    public void Calculate()
    //    {
    //        // Lógica de flujo manual (ej. 0.5 kg/s definido en el mixer)
    //        double manualRate = _mixer.ManualFillingFlowRate;

    //        flow = PendingMass > manualRate ? manualRate : PendingMass;
    //        currentMass += flow;
    //        _mixer._currentLevel += flow;

    //    }

    //    public void CheckTransitions()
    //    {
    //        if (PendingMass <= 0)
    //        {


    //            _mixer.FinishCurrentStep();
    //        }
    //    }
    //}

    //// En NewMixerProcessingTime.cs

    //public class NewMixerProcessingTime : INewOutletState
    //{
    //    double counter = 0;
    //    double maxtime = 0;
    //    double pendingtime => maxtime - counter;

    //    NewMixer _mixer;
    //    private readonly MixerActivityLink _activity; // <--- Referencia

    //    public bool IsProductive => true;
    //    public string HexColor => "#FFC107"; // Color ámbar para proceso
    //    private double _secondsElapsed = 0;

    //    public NewMixerProcessingTime(NewMixer m, MixerActivityLink activity)
    //    {
    //        _mixer = m;
    //        _activity = activity;

    //        // Obtenemos la duración directamente del Link (que ya tiene la duración planeada en segundos)
    //        // Ojo: activity.DurationSeconds ya es un double en segundos.
    //        maxtime = activity.DurationSeconds;

    //    }

    //    public string StateLabel => $"{_activity.Order}-{_mixer.CurrentTotalSteps} {_activity.StepDefinition?.OperationType}";
    //    public string SubStateName => $"{counter:F0}/{maxtime:F0} s";

    //    public void Calculate()
    //    {
    //        counter++;
    //        _secondsElapsed += 1.0;


    //    }

    //    public void CheckTransitions()
    //    {
    //        if (pendingtime <= 0)
    //        {
    //            // Guardamos el tiempo real (aunque aquí coincida con el planeado)


    //            _mixer.FinishCurrentStep();
    //        }
    //    }
    //}

    //public class NewMixerDischarging : INewOutletState
    //{
    //    private readonly NewMixer _mixer;
    //    private double _dischargeRate;
    //    private double _secondsElapsed = 0;
    //    private readonly MixerActivityLink _activity; // <--- Referencia
    //    public NewMixerDischarging(NewMixer m, MixerActivityLink activity)
    //    {
    //        _mixer = m;
    //        _dischargeRate = _mixer._DischargeRate;
    //        _activity = activity;
    //    }

    //    public bool IsProductive => true;
    //    public string HexColor => "#4CAF50"; // Verde (Running)
    //    public string StateLabel => $"Discharging to {_mixer.DestinationVessel?.Name ?? "Unknown"}";
    //    public string SubStateName => $"{_mixer._currentLevel:F1} kg left";


    //    public void Calculate()
    //    {
    //        // Cálculo del flujo real para este tick
    //        _secondsElapsed += 1.0;

    //        // 1. Intentamos vaciar
    //        // Cálculo del flujo real para este tick
    //        double realFlow = _dischargeRate;

    //        // No sacar más de lo que tengo
    //        if (_mixer._currentLevel < realFlow)
    //            realFlow = _mixer._currentLevel;
    //        // Imaginemos que 'actualFlow' es lo que realmente recibió el tanque destino
    //        // (porque podría estar lleno y rechazar el líquido)
    //        _mixer.DestinationVessel?.SetInletFlow(realFlow);
    //        _mixer._currentLevel -= realFlow;

    //    }

    //    public void CheckTransitions()
    //    {
    //        // Solo paramos si nos vaciamos. 
    //        // Si el tanque se llena, él nos pausará vía GlobalState.
    //        if (_mixer._currentLevel <= 0.01)
    //        {

    //            _mixer.ReceiveStopCommand();
    //        }
    //    }
    //}
    //public class NewMixerManagingWashing : INewOutletState
    //{
    //    private readonly NewMixer _mixer;
    //    private readonly MixerActivityLink _activity; // <--- Referencia al Ticket

    //    private double _secondsElapsed = 0;
    //    private double _targetWashingTime;

    //    // UI Properties
    //    public bool IsProductive => false; // Lavado es tiempo "Muerto" necesario
    //    public string HexColor => "#00BCD4"; // Cyan
    //    public string StateLabel => $"Washing: {_mixer.LastMaterialProcessed?.Name ?? "Equipment"}";

    //    // Muestra cuánto falta en la etiqueta del estado
    //    public string SubStateName => $"{EstimatedRemainingSeconds:F0}s remaining";

    //    // Implementación de la Interfaz para el Scheduler
    //    public double EstimatedRemainingSeconds
    //    {
    //        get
    //        {
    //            double remaining = _targetWashingTime - _secondsElapsed;
    //            return remaining > 0 ? remaining : 0;
    //        }
    //    }

    //    public NewMixerManagingWashing(NewMixer m, MixerActivityLink activity)
    //    {
    //        _mixer = m;
    //        _activity = activity;

    //        // 1. CÁLCULO DEL TIEMPO (Matriz de Lavados)
    //        if (_mixer.LastMaterialProcessed != null && _mixer.CurrentMaterial != null)
    //        {
    //            // Buscamos en la matriz cuánto demora limpiar A para meter B
    //            _targetWashingTime = _mixer.WashoutRules
    //                .GetMixerWashout(_mixer.LastMaterialProcessed.Category, _mixer.CurrentMaterial.Category)
    //                .GetValue(TimeUnits.Second);
    //        }
    //        else
    //        {
    //            // Si es el primer batch o no hay datos, no lavamos.
    //            _targetWashingTime = 0;
    //        }

    //        // 2. INICIALIZAR EL TICKET (Vital para el Scheduler)
    //        // Le decimos al mundo: "Estaré ocupado lavando por X segundos"

    //    }

    //    public void Calculate()
    //    {
    //        // 1. Cronómetro
    //        _secondsElapsed += 1.0;

    //        // 2. Actualizar el Scheduler
    //        // Si por alguna razón nos pasamos del tiempo estimado, 
    //        // estiramos la duración para que el Scheduler no mienta.


    //    }

    //    public void CheckTransitions()
    //    {
    //        // 1. ¿Terminó el tiempo? 
    //        if (_secondsElapsed >= _targetWashingTime)
    //        {
    //            // A. Liberar la bomba de lavado (si la teníamos capturada)
    //            _mixer.WashingPump?.ReleaseAccess(_mixer);

    //            // B. REGISTRAR KPI FINAL
    //            // Guardamos cuánto duró realmente el lavado


    //            // C. ¡ARRANCAR LA RECETA!
    //            _mixer.FinishCurrentStep();
    //        }
    //    }
    //}



}
