using GeminiSimulator.Materials;
using GeminiSimulator.NewFilesSimulations.Operators;
using UnitSystem;

//namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
//{


//    public class MixerScheduleEstimated
//    {
//        // CAMBIO: Añadimos el constructor que invoca el OrderManager
//        public MixerScheduleEstimated(DateTime plannedInit)
//        {
//            PlannedInit = plannedInit;
//        }

//        public DateTime PlannedInit { get; private set; }

//        public List<MixerActivityLink> AllActivities { get; private set; } = new();
//        public MixerActivityLink? OperatorLink { get; private set; }
//        public void BuildSchedule(IScheduleLink? hookToPrevious, NewOperator? operatorUnit, double washoutSecs, List<RecipeStep> recipeSteps, double transferSecs)
//        {
//            AllActivities.Clear();
//            IScheduleLink? lastLink = hookToPrevious;

//            // 0. Operario (EL VÍNCULO DINÁMICO)
//            // Determinamos el tiempo de setup según la configuración (opSecs)
//            double setupDuration = (operatorUnit != null) ? _mixer.OperatorTimeDisabled.GetValue(TimeUnits.Second) : 0;

//            // Pasamos 'operatorUnit' para que el eslabón pueda consultar su 'AvailableAt' en tiempo real
//            var opLink = new MixerActivityLink(this, lastLink, setupDuration, MixerActivityType.OperatorSetup, op:operatorUnit);
//            AllActivities.Add(opLink);
//            lastLink = opLink;

//            // 1. Lavado
//            if (washoutSecs > 0)
//            {
//                var wash = new MixerActivityLink(this, lastLink, washoutSecs, MixerActivityType.Washout);
//                AllActivities.Add(wash);
//                lastLink = wash;
//            }

//            // 2. Pasos de Receta
//            foreach (var step in recipeSteps.OrderBy(x => x.Order))
//            {
//                var stepLink = new MixerActivityLink(this, lastLink, step.Duration.GetValue(TimeUnits.Second), MixerActivityType.RecipeStep, null, step.Order);
//                AllActivities.Add(stepLink);
//                lastLink = stepLink;
//            }

//            // 3. Transferencia
//            if (transferSecs > 0)
//            {
//                var trans = new MixerActivityLink(this, lastLink, transferSecs, MixerActivityType.Transfer);
//                AllActivities.Add(trans);
//            }
//        }


//        public DateTime PlannedEnd => AllActivities.LastOrDefault()?.PlannedEnd ?? PlannedInit;

//        public void UpdateInitDate(DateTime newInit)
//        {
//            PlannedInit = newInit;
//        }
//    }
//    public interface IScheduleLink
//    {
//        DateTime PlannedInit { get; }
//        DateTime PlannedEnd { get; }
//        double DurationSeconds { get; }
//        MixerActivityType Type { get; }

//    }
//    public class MixerActivityLink : IScheduleLink
//    {
//        private readonly MixerScheduleEstimated _parent;
//        private readonly IScheduleLink? _previous;
//        private readonly NewOperator? _operator; // El vínculo dinámico al recurso humano

//        // Implementación de la Interfaz IScheduleLink
//        public string Name => ToString();
//        public double DurationSeconds { get; set; }

//        public int Order { get; private set; }
//        public MixerActivityType Type { get; }
//        public Guid BatchId { get; set; }

//        // Propiedades para el seguimiento de la realidad
//        public double RealDurationSeconds { get; set; }
//        public double AccumulatedStarvation { get; set; }

//        public MixerActivityLink(
//            MixerScheduleEstimated parent,
//            IScheduleLink? previous,
//            double duration,
//            MixerActivityType type,
//            int order = 0,
//            NewOperator? op = null)
//        {
//            _parent = parent;
//            _previous = previous;
//            DurationSeconds = duration;
//            Type = type;
//            _operator = op; // Guardamos la referencia al objeto vivo del operario
//            Order = order;
//        }

//        /// <summary>
//        /// EL ACORDEÓN: Calcula el inicio de la actividad en tiempo real.
//        /// Si el operario se retrasa en otro mixer, esta fecha se desplaza automáticamente.
//        /// </summary>
//        public DateTime PlannedInit
//        {
//            get
//            {
//                // 1. Fecha base: cuando termina el eslabón anterior en este Mixer 
//                // o el inicio programado del lote si es el primero.
//                DateTime baseDate = _previous?.PlannedEnd ?? _parent.PlannedInit;

//                // 2. Lógica de Concurrencia para el Operario:
//                // Si esta actividad requiere al operario (Setup), el inicio real es el 
//                // momento en que AMBOS (Mixer y Operario) están libres.
//                if (Type == MixerActivityType.OperatorSetup && _operator != null)
//                {
//                    // Comparamos la disponibilidad del equipo vs la del operario
//                    return baseDate > _operator.AvailableAt ? baseDate : _operator.AvailableAt;
//                }

//                return baseDate;
//            }
//        }

//        /// <summary>
//        /// El fin de la actividad considera el inicio dinámico + duración teórica + retrasos reales.
//        /// </summary>
//        public DateTime PlannedEnd => PlannedInit.AddSeconds(DurationSeconds + AccumulatedStarvation);

//        public override string ToString()
//        {
//            return Type switch
//            {
//                MixerActivityType.OperatorSetup => "Operator Setup",
//                MixerActivityType.Washout => "Equipment Washout",
//                MixerActivityType.Transfer => "Product Transfer",
//                MixerActivityType.RecipeStep => $"Step {Order}: Processing",
//                _ => "Unknown Activity"
//            };
//        }
//    }


//    public class MixerOrderManager
//    {
//        private readonly NewMixer _mixer;
//        public Queue<MixerActivityLink> ActivePipeline { get; } = new();
//        public List<MixerActivityLink> ExecutionHistory { get; } = new();

//        public MixerOrderManager(NewMixer mixer) => _mixer = mixer;

//        public void AddOrder(Guid batchId, NewOperator? op, double washSecs, List<RecipeStep> recipe, double transSecs)
//        {
//            IScheduleLink? lastLink = ActivePipeline.ToList().LastOrDefault()
//                                      ?? ExecutionHistory.LastOrDefault();

//            var tempSchedule = new MixerScheduleEstimated(_mixer.CurrentDate);

//            // Le pasamos el objeto 'op' al constructor del cronograma
//            tempSchedule.BuildSchedule(lastLink, op, washSecs, recipe, transSecs);

//            foreach (var activity in tempSchedule.AllActivities)
//            {
//                activity.BatchId = batchId;
//                ActivePipeline.Enqueue(activity);
//            }
//        }

//        public void ReportStarvation(double seconds)
//        {
//            if (ActivePipeline.TryPeek(out var current))
//            {
//                current.AccumulatedStarvation += seconds;
//            }
//        }

//        public void CompleteCurrentActivity()
//        {
//            if (ActivePipeline.TryDequeue(out var completed))
//            {
//                // Aquí capturamos la duración real antes de guardarla
//                completed.RealDurationSeconds = (DateTime.Now - completed.PlannedInit).TotalSeconds;
//                ExecutionHistory.Add(completed);
//            }
//        }

//        public MixerActivityLink? CurrentActivity => ActivePipeline.TryPeek(out var a) ? a : null;
//    }
//    public enum MixerActivityType
//    {
//        OperatorSetup,
//        Washout,
//        RecipeStep,
//        Transfer
//    }
//}
