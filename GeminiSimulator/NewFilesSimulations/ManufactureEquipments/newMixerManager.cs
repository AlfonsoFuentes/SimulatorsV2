using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.Operators;
using UnitSystem;

//namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
//{
//    public enum MixerActivityType { OperatorSetup, Washout, RecipeStepAdd, RecipeStepTime, Transfer }

//    public interface IScheduleLink
//    {
//        DateTime PlannedInit { get; }
//        DateTime PlannedEnd { get; }
//        double DurationSeconds { get; }
//        MixerActivityType Type { get; } // Necesario para la lógica de inicio
//    }
//    public class MixerActivityLink : IScheduleLink
//    {
//        private readonly MixerScheduleEstimated _parent;
//        private readonly IScheduleLink? _previous;
//        private readonly NewOperator? _operator;
//        public ProductDefinition Product { get; } // <--- NUEVO
//        public RecipeStep? StepDefinition { get; private set; }

//        public MixerActivityType Type { get; }
//        public Guid BatchId { get; set; }
//        public double DurationSeconds { get; set; }        // Duración TOTAL estimada (Física + Starved)
//        public double RealDurationSeconds => DurationSeconds + AccumulatedStarvation;  // Duración final real (para el histórico)
//        public double AccumulatedStarvation { get; set; }  // "Grasa": Tiempo perdido esperando recursos
//        public int Order { get; private set; }
//        public string Name => ToString();
//        public Amount MassTarget { get; private set; }

//        public MixerActivityLink(MixerScheduleEstimated parent, IScheduleLink? previous,
//                                  double duration, MixerActivityType type, ProductDefinition product, Amount masstarget,
//                                  int order = 0, NewOperator? op = null,
//                                  RecipeStep? stepDef = null)
//        {
//            _parent = parent;
//            _previous = previous;
//            DurationSeconds = duration;
//            Type = type;
//            _operator = op;
//            Order = order;
//            StepDefinition = stepDef; // <--- Guardamos la definición
//            Product = product;
//            MassTarget = masstarget;
//        }

//        private DateTime? _fixedPlannedInit;

//        public void SetFixedStart(DateTime date)
//        {
//            _fixedPlannedInit = date;
//        }

//        public DateTime PlannedInit
//        {
//            get
//            {
//                // 1. PRIORIDAD TOTAL: Si ya me fijaron una fecha, uso esa.
//                // Esto ocurre cuando soy el primero de la fila y mi antecesor ya se fue.
//                if (_fixedPlannedInit.HasValue)
//                {
//                    return _fixedPlannedInit.Value;
//                }

//                // 2. Lógica Dinámica (Solo mientras tengo un antecesor vivo en la cola)
//                DateTime baseDate = _previous?.PlannedEnd ?? _parent.PlannedInit;

//                // (Tu lógica de operario sigue aquí si quieres, o la mueves al SetFixedStart)
//                if (Type == MixerActivityType.OperatorSetup && _operator != null)
//                {
//                    return baseDate > _operator.AvailableAt ? baseDate : _operator.AvailableAt;
//                }
//                return baseDate;
//            }
//        }

//        public DateTime PlannedEnd => PlannedInit.AddSeconds(RealDurationSeconds);

//        public override string ToString() => Type switch
//        {
//            MixerActivityType.OperatorSetup => "Operator Setup",
//            MixerActivityType.Washout => "Washout",
//            MixerActivityType.Transfer => "Transfer to Wip",
//            MixerActivityType.RecipeStepAdd => $"Adding {StepDefinition?.IngredientName}",
//            MixerActivityType.RecipeStepTime => $"{StepDefinition?.OperationType}",
//            _ => "Unknown"
//        };
//        public double MassAtStart { get; set; } // Se captura cuando el paso inicia realmente

//        public string GetStepStatus(double currentMixerMass)
//        {
//            if (Type == MixerActivityType.RecipeStepAdd && StepDefinition != null)
//            {
//                double addedSoFar = currentMixerMass - MassAtStart;
//                double target = MassTarget.GetValue(MassUnits.KiloGram);

//                // Retorna: "20.40 / 50.00 kg (Sensolia)"
//                return $"{Math.Max(0, addedSoFar):N2} / {target:N2} kg";
//            }

//            // Si no es adición, mostrar el total del tanque como antes
//            return $"{currentMixerMass:N2} kg";
//        }
//    }
//    public class MixerScheduleEstimated
//    {

//        public DateTime PlannedInit { get; private set; }
//        public List<MixerActivityLink> AllActivities { get; } = new();

//        public MixerScheduleEstimated(DateTime plannedInit)
//        {

//            PlannedInit = plannedInit;
//        }

//        public void BuildSchedule(IScheduleLink? hookToPrevious, NewOperator? operatorUnit,
//                                   double washoutSecs, List<RecipeStep> recipeSteps, double transferSecs, ProductDefinition product)
//        {
//            AllActivities.Clear();
//            IScheduleLink? lastLink = hookToPrevious;

//            // 0. Operario
//            var opLink = new MixerActivityLink(this, lastLink, 0, MixerActivityType.OperatorSetup, product, new Amount(0, MassUnits.KiloGram), 0, operatorUnit);
//            AllActivities.Add(opLink);
//            lastLink = opLink;

//            // 1. Lavado y Pasos (Igual que antes...)
//            if (washoutSecs > 0)
//            {
//                var wash = new MixerActivityLink(this, lastLink, washoutSecs, MixerActivityType.Washout, product, new Amount(0, MassUnits.KiloGram));
//                AllActivities.Add(wash);
//                lastLink = wash;
//            }

//            foreach (var step in recipeSteps.OrderBy(x => x.Order))
//            {
//                MixerActivityType newstep = step.OperationType == QWENShared.Enums.BackBoneStepType.Add ? MixerActivityType.RecipeStepAdd : MixerActivityType.RecipeStepTime;
//                var sLink = new MixerActivityLink(this, lastLink, step.Duration.GetValue(TimeUnits.Second), newstep, product, step.MassTarget, step.Order, null, step);
//                AllActivities.Add(sLink);
//                lastLink = sLink;
//            }
//            var sumtime = AllActivities.Sum(x => x.DurationSeconds) / 60;

//            if (transferSecs > 0)
//            {
//                AllActivities.Add(new MixerActivityLink(this, lastLink, transferSecs, MixerActivityType.Transfer, product, new Amount(0, MassUnits.KiloGram)));
//            }
//            sumtime = AllActivities.Sum(x => x.DurationSeconds) / 60;
//        }

//        public DateTime PlannedEnd => AllActivities.LastOrDefault()?.PlannedEnd ?? PlannedInit;
//    }
//    public class MixerOrderManager
//    {
//        private readonly NewMixer2 _mixer;
//        public Queue<MixerActivityLink> ActivePipeline { get; } = new();
//        public List<MixerActivityLink> ExecutionHistory { get; } = new();

//        public MixerOrderManager(NewMixer2 mixer) => _mixer = mixer;

//        public void AddOrder(Guid batchId, NewOperator? op, double washSecs, List<RecipeStep> recipe, double transSecs, ProductDefinition product)
//        {
//            IScheduleLink? lastLink = ActivePipeline.ToList().LastOrDefault() ?? ExecutionHistory.LastOrDefault();

//            // Pasamos el _mixer al constructor
//            var tempSchedule = new MixerScheduleEstimated(_mixer.CurrentDate);
//            tempSchedule.BuildSchedule(lastLink, op, washSecs, recipe, transSecs, product);

//            foreach (var act in tempSchedule.AllActivities)
//            {
//                act.BatchId = batchId;
//                ActivePipeline.Enqueue(act);
//            }
//        }

//        public void CompleteCurrentActivity()
//        {
//            if (ActivePipeline.TryDequeue(out var completed))
//            {
//                ExecutionHistory.Add(completed);

//                // LA VERDAD ABSOLUTA: El momento exacto del relevo es AHORA.
//                DateTime now = _mixer.CurrentDate;

//                if (ActivePipeline.TryPeek(out var nextActivity))
//                {
//                    // Le decimos al siguiente: "El pasado ya pasó. Tu realidad empieza AHORA".
//                    nextActivity.SetFixedStart(now);
//                }
//            }
//        }

//        public DateTime MixerProjectedFreeTime => ActivePipeline.LastOrDefault()?.PlannedEnd ?? _mixer.CurrentDate;

//        // FARO 2: ¿Cuándo termina el lote que se está procesando AHORA? (Para el operario FullBatch)
//        public DateTime CurrentBatchPlannedEnd
//        {
//            get
//            {
//                var current = CurrentActivity;
//                if (current == null) return _mixer.CurrentDate;

//                // Buscamos el último eslabón que pertenece al MISMO lote que el actual
//                // Normalmente es el paso de "Transferencia" de este BatchId
//                var lastOfBatch = ActivePipeline.ToList().LastOrDefault(x => x.BatchId == current.BatchId);
//                return lastOfBatch?.PlannedEnd ?? current.PlannedEnd;
//            }
//        }
//        public MixerActivityLink? CurrentActivity => ActivePipeline.TryPeek(out var a) ? a : null;
//        // En MixerOrderManager.cs
//        public ProductDefinition? GetLastScheduledMaterial()
//        {
//            // 1. Si hay cola, miramos el ÚLTIMO papelito y leemos su producto
//            var lastLink = ActivePipeline.LastOrDefault();
//            if (lastLink != null)
//            {
//                return lastLink.Product;
//            }

//            // 2. Si no hay cola, miramos qué tiene el Mixer adentro AHORA
//            if (_mixer.CurrentMaterial != null)
//            {
//                return _mixer.CurrentMaterial;
//            }

//            // 3. Si está vacío y sin cola, miramos qué fue lo último que hizo (para saber si hay que lavar)
//            return _mixer.LastMaterialProcessed;
//        }
//        // En MixerOrderManager.cs
//        public Guid? CurrentBatchId => CurrentActivity?.BatchId;

//        // Retorna todas las actividades que pertenecen al lote que se está ejecutando ahora
//        public List<MixerActivityLink> CurrentBatchActivities =>
//            ActivePipeline.Where(x => x.BatchId == CurrentBatchId).ToList();

//        // Retorna el producto que se está fabricando actualmente
//        public ProductDefinition? CurrentProduct => CurrentActivity?.Product;
//        // --- TIEMPO TRANSCURRIDO DEL BATCH ---
//        // Suma de la duración real de los pasos terminados de este lote + el tiempo del paso actual
//        private double CurrentBatchElapsedSeconds
//        {
//            get
//            {
//                var batchId = CurrentBatchId;
//                if (batchId == null) return 0;

//                // 1. Tiempo de pasos ya terminados del mismo lote
//                var finishedTime = ExecutionHistory
//                    .Where(x => x.BatchId == batchId)
//                    .Sum(x => x.RealDurationSeconds);

//                // 2. Tiempo que lleva el paso actual (Desde que se fijó su inicio hasta ahora)
//                // Usamos una propiedad en MixerActivityLink que guarde el DateTime real de inicio
//                var currentTime = (CurrentActivity != null)
//                    ? (_mixer.CurrentDate - CurrentActivity.PlannedInit).TotalSeconds
//                    : 0;

//                return finishedTime + Math.Max(0, currentTime);
//            }
//        }
//        public Amount CurrentBatchTime => new Amount(CurrentBatchElapsedSeconds, TimeUnits.Second);
//        // --- TIEMPO PERDIDO (STARVATION) ---
//        // Es la acumulación de la "Grasa" (AccumulatedStarvation) de todos los links del lote actual
//        private double CurrentBatchLostTimeSeconds
//        {
//            get
//            {
//                var batchId = CurrentBatchId;
//                if (batchId == null) return 0;

//                var finishedLost = ExecutionHistory.Where(x => x.BatchId == batchId).Sum(x => x.AccumulatedStarvation);
//                var currentLost = CurrentActivity?.AccumulatedStarvation ?? 0;

//                return finishedLost + currentLost;
//            }
//        }
//        public Amount CurrentBatchStarved => new Amount(CurrentBatchLostTimeSeconds, TimeUnits.Second);

//    }

//}
