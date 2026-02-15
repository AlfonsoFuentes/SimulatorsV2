//namespace GeminiSimulator.Main
//{
//    public record DemandLog(
//         string TankName,
//         string ProductName,
//         string Linename,
//         double TimeToEmpty,
//         int BatchTime,
//         string TotalStorage,   // <--- EL DATO NUEVO: ¿Por qué no se programó?
//         string AssignedMixer,   // <--- EL DATO NUEVO: Nombre exacto del mixer si se asignó
//         string StatusColor,     // Para la UI (Success, Warning, Error, Info)
//         bool IsInHouse,
//         string BestMixerName,
//         double BestMixerBatchTime
//     );

//    // 2. REGISTRO DE OFERTA (El "Dashboard" del Mixer)
//    public record SupplyLog(
//        string MixerName,
//        string Operation,       // Ej: "Mixing", "Discharging"
//        string CurrentProduct,
//        string TargetTank,      // <--- EL DATO NUEVO: ¿Para quién cocina?
//        double EtaMinutes,      // Tiempo para liberarse
//        List<string> QueueList  // <--- EL DATO NUEVO: Nombres de los tanques en cola
//    );
//    public record Need(
//            ProcessTank Tank,
//            ProductDefinition Product,
//            double SlackTimeMinutes,
//            bool IsInHouse,
//            bool IsBatchNeeded
//        );
//    public class ProductionScheduler
//    {
//        public List<DemandLog> DemandSnapshot { get; private set; } = new();
//        public List<SupplyLog> SupplySnapshot { get; private set; } = new();
//        private readonly SimulationContext _context;
//        private readonly OptimizationEngine _ai;
//        public OptimizationEngine AiEngine => _ai;
//        public List<Need> LastCalculationSnapshot { get; private set; } = new();
//        public ProductionScheduler(SimulationContext context)
//        {
//            _context = context;
//            _ai = new OptimizationEngine(); // Inicia el cerebro
//        }

//        // Dentro de ProductionScheduler.cs
//        public void Execute(DateTime currentTime)
//        {
//            // =================================================================
//            // PARTE A: LOGICA DE OFERTA (Mixers) - CÁLCULO INTELIGENTE (AI)
//            // =================================================================
//            var supplyList = new List<SupplyLog>();

//            // Filtro de seguridad (Null check)
//            var mixers = _context.Mixers.Values
//                .Where(m => m.Materials != null && m.Materials.Any(mat => mat.IsFinishedProduct) && m.InboundState != null)
//                .OrderBy(m => m.Name)
//                .ToList();

//            foreach (var mixer in mixers)
//            {
//                double timeToFree = 0;
//                string targetTank = "--";
//                var queueNames = mixer.WipsQueue.Select(t => t.Name).ToList();

//                if (mixer.InboundState is not MixerIdle)
//                {
//                    // ---------------------------------------------------------
//                    // 🧠 AI INTEGRATION: PREDICCIÓN DE TIEMPO
//                    // ---------------------------------------------------------
//                    // En lugar de "3600", le preguntamos al AI cuánto tarda ESTE producto en ESTE mixer.
//                    // El AI aprende de los errores pasados.

//                    double totalEst = 3600; // Valor por defecto de seguridad

//                    if (mixer.CurrentMaterial != null)
//                    {
//                        // Asumo que tu método se llama PredictBatchTime o similar. 
//                        // Si se llama diferente, ajusta esta línea:
//                        totalEst = _ai.PredictBatchDuration(mixer, mixer.CurrentMaterial);
//                    }

//                    // Calculamos cuánto falta
//                    double remaining = totalEst - mixer.NetBatchTimeInSeconds;

//                    // Si el AI dice que ya debió terminar pero sigue rodando, mostramos 0 (Inminente)
//                    // Esto le enseña al usuario que el proceso se está demorando más de lo previsto.
//                    timeToFree = remaining > 0 ? remaining / 60.0 : 0;

//                    targetTank = mixer.CurrentWipTank?.Name ?? "";
//                }

//                supplyList.Add(new SupplyLog(
//                    mixer.Name,
//                    mixer.InboundState?.StateName ?? "Off",
//                    mixer.CurrentMaterial?.Name ?? "--",
//                    targetTank,
//                    timeToFree,
//                    queueNames
//                ));
//            }
//            SupplySnapshot = supplyList.OrderBy(x => x.MixerName).ThenBy(x => x.EtaMinutes).ToList();

//            // =================================================================
//            // PARTE B: LOGICA DE DEMANDA (El Cerebro del Scheduler)
//            // =================================================================

//            var allNeeds = GatherAllNeeds();
//            var sortedNeeds = allNeeds.Where(x => x.IsBatchNeeded).OrderBy(n => n.SlackTimeMinutes).ToList();

//            var logicTrace = new Dictionary<string, (string Reason, BatchMixer? Mixer, string Color)>();

//            foreach (var need in sortedNeeds)
//            {
//                // 1. CÁLCULOS MATEMÁTICOS

//                if (need.Tank.Name.Contains("9"))
//                {
//                }
//                var bestMixer = FindBestMixerFor(need.Product, need.IsInHouse);
//                // --- CHECK 1: PIPELINE FULL? ---
//                if (bestMixer == null)
//                {
//                    logicTrace[need.Tank.Name] = ("No Compatible Mixer", null!, "Error");
//                    continue;
//                }
//                // --- CHECK 4: ASIGNACIÓN EXITOSA ---
//                if (need.IsInHouse)
//                    bestMixer.ReceivePriorityRequirementBatch(need.Tank);
//                else
//                    bestMixer.ReceiveRequirementBatch(need.Tank);

//                logicTrace[need.Tank.Name] = ("Assigned & Scheduled", bestMixer, "Info");
//            }

//            // =================================================================
//            // PARTE C: CONSTRUIR LA LISTA FINAL
//            // =================================================================
//            var demandList = new List<DemandLog>();

//            var wiptans = _context.Tanks.Values
//                    .OfType<BatchWipTank>()
//                    .Where(t => t.CurrentLine != null).Select(x => x as ProcessTank).ToList();
//            var InHouseTanks = _context.Tanks.Values
//                    .OfType<InHouseTank>().Select(x => x as ProcessTank).ToList();

//            var allTanks = wiptans.Concat(InHouseTanks).ToList();

//            foreach (var tank in allTanks)
//            {
//                string reason = "Stable / No Demand";
//                BatchMixer? mixer = mixers.FirstOrDefault(x => x.CurrentWipTank == tank);
//                string color = "Default";
//                double timeToEmpty = tank.PendingTimeToCurrentLevel.GetValue(TimeUnits.Minute);

//                bool isVip = tank is InHouseTank;

//                if (logicTrace.ContainsKey(tank.Name))
//                {
//                    var trace = logicTrace[tank.Name];
//                    reason = trace.Reason;

//                    color = trace.Color;
//                }
//                string linename = tank is WipTank wipTank && wipTank.CurrentLine != null ? wipTank.CurrentLine.Name : "--";
//                var bestMixer = FindBestMixerFor(tank.CurrentMaterial!, false);
//                double MixerAvailableTime = bestMixer == null ? 0 :
//                    CalculateEstimatedTimeAvailability(bestMixer!, tank.CurrentMaterial!, isVip, bestMixer?.GetCapacity(tank.CurrentMaterial!).GetValue(MassUnits.KiloGram) ?? 0);
//                demandList.Add(new DemandLog(
//                    tank.Name,
//                    tank.CurrentMaterial?.Name ?? "Empty",
//                      linename,
//                    timeToEmpty,
//                    mixer?.NetBatchTimeInSeconds ?? 0,
//                    $"{tank.TotalMassInProcess:F0}, Kg",
//                    mixer?.Name ?? "--",
//                    color,
//                    isVip,
//                    bestMixer?.Name ?? "", MixerAvailableTime

//                ));
//            }

//            // =================================================================
//            // PARTE D: ORDENAMIENTO POR PRIORIDAD (Weighted Score)
//            // =================================================================
//            DemandSnapshot = demandList
//                .OrderBy(x =>
//                {
//                    if (x.TimeToEmpty < 60) return 0; // Prioridad Máxima

//                    return x.StatusColor switch
//                    {
//                        "Error" => 0,
//                        "Warning" => 1,
//                        "Info" => 2,
//                        "Success" => 3,  // Pipeline Full baja aquí
//                        _ => 4
//                    };
//                })
//                .ThenBy(x => x.TimeToEmpty)
//                .ToList();
//        }



//        // --- FASE 1: RECOLECCIÓN ---
//        private List<Need> GatherAllNeeds()
//        {
//            var needs = new List<Need>();

//            var lines = _context.Lines.Values.Where(x => x.CurrentOrder != null && x.CurrentWipTank != null).ToList();
//            // A. Analizar WIPs de Líneas (Consumo normal)
//            List<ProcessTank> AllTanks = new();
//            foreach (var line in lines)
//            {
//                AllTanks.Add(line.CurrentWipTank!);
//            }
//            foreach (var tank in _context.TanksInHouse)
//            {
//                AllTanks.Add(tank);
//            }
//            AllTanks = AllTanks.OrderBy(x => x.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute)).ToList();
//            foreach (var tank in AllTanks)
//            {
//                var need = AnalyzeTank(tank, tank.CurrentMaterial!, false);
//                if (need != null) needs.Add(need);
//            }


//            return needs;
//        }

//        // --- FASE 2: ANÁLISIS DE URGENCIA (MATEMÁTICA PURA) ---
//        // Dentro de ProductionScheduler.cs

//        private Need? AnalyzeTank(ProcessTank tank, ProductDefinition product, bool isInHouse)
//        {
//            // -------------------------------------------------------------
//            // CORRECCIÓN: ESTIMAR EL TAMAÑO REAL DEL LOTE (EL "BALDE")
//            // -------------------------------------------------------------

//            if (tank.Name.Contains("9"))
//            {

//            }
//            double timeToEmptyMin = tank.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute);
//            if (timeToEmptyMin == 0) { return null; }//No hay datos que analizar aun no ha corrido la linea asignara basura
//            var bestMixer = FindBestMixerFor(product, isInHouse);
//            // 1. Buscamos qué mixers pueden hacer este producto para saber el tamaño típico del lote.

//            if (bestMixer == null) return null;


//            // Si no hay mixers, asumimos algo seguro (ej. 2000kg) o la capacidad del tanque si es pequeño.
//            // Esto evita división por cero.
//            Amount Mixercapacity = bestMixer.GetCapacity(product);

//            double estimatedBatchSize = Mixercapacity.GetValue(MassUnits.KiloGram);
//            // -------------------------------------------------------------
//            // CASO 1: VALIDACIÓN DE ESPACIO FUTURO (Predictivo)
//            // -------------------------------------------------------------

//            double currentLevel = tank.TotalMassInProcess;
//            double currentSpace = tank.WorkingCapacity.GetValue(MassUnits.KiloGram) - currentLevel;

//            if (currentSpace < 0)
//                return null;
//            // 2. ¿Cuánto tardará el mixer en llegar?
//            double batchtimme = _ai.PredictBatchDuration(bestMixer, product);
//            double dischargeTime = bestMixer.DischargeRate == 0 ? 10000000 : estimatedBatchSize / bestMixer.DischargeRate;
//            double estimatedBatchTimeSec = batchtimme + dischargeTime;
//            if (tank.Name.Contains("Base"))
//            {
//            }


//            // 3. ¿Cuánto va a consumir la línea mientras cocinamos? (Proyección)
//            double consumptionPerSec = tank.AverageOutleFlow.GetValue(MassFlowUnits.Kg_sg);
//            double projectedConsumption = consumptionPerSec * estimatedBatchTimeSec;

//            // 4. ESPACIO PROYECTADO (El hueco que habrá en el futuro)
//            double projectedSpace = currentSpace + projectedConsumption;

//            // REGLA CORREGIDA:
//            // Comparamos el hueco futuro contra el TAMAÑO DEL LOTE (Mixer), no contra el tanque entero.
//            // Si el hueco futuro es menor que el lote del mixer, esperamos.
//            if (projectedSpace < (estimatedBatchSize * 0.95))
//            {
//                return null; // Aún no cabe un lote completo. Esperar.
//            }





//            double estimatedBatchTimeMin = estimatedBatchTimeSec / 60.0;
//            double slack = timeToEmptyMin - estimatedBatchTimeMin;




//            bool IsBatchNeeded = slack <= 0;
//            if (!IsBatchNeeded) return null;

//            return new Need(tank, product, slack, isInHouse, IsBatchNeeded);
//        }


//        private BatchMixer? FindBestMixerFor(ProductDefinition _product, bool _isInHouse)
//        {
//            if (_product == null) return null;
//            // 1. Filtrar: Solo mixers que físicamente puedan hacer el producto
//            var candidates = _context.Mixers.Values
//                .Where(m => m.CanProcess(_product))
//                .ToList();

//            if (!candidates.Any()) return null;

//            List<(BatchMixer Mixer, double BestProductivity)> scoredCandidates = new();

//            foreach (var candidate in candidates)
//            {
//                var productivity = CalculateEstimatedAvailability(candidate, _product, _isInHouse);
//                scoredCandidates.Add((candidate, productivity));
//            }



//            var BestCandidate = scoredCandidates.MaxBy(x => x.BestProductivity);

//            var selectedmixer = BestCandidate.Mixer;
//            // 3. Ganador: El que tenga menor tiempo de espera
//            return selectedmixer;
//        }
//        private double CalculateEstimatedTimeAvailability(BatchMixer mixer, ProductDefinition productToMake, bool isPriority, double BatchSizeKg)
//        {
//            double timelineSeconds = 1;
//            ProductDefinition? currentContextProduct = mixer.CurrentMaterial ?? mixer.LastMaterialProcessed;

//            // --- A. ESTADO ACTUAL (Lo que ya está haciendo) ---
//            if (mixer.InboundState is not MixerIdle)
//            {
//                timelineSeconds += GetMixerMixerPendingBatchTime(mixer);
//                // Si está lavando

//            }

//            // --- B. LA COLA DE ESPERA (Future Simulation) ---
//            // Si soy VIP (InHouse), me salto esta parte (no sumo el tiempo de la cola)
//            if (!isPriority)
//            {
//                foreach (var queuedTank in mixer.WipsQueue)
//                {
//                    var nextProduct = queuedTank.CurrentMaterial;
//                    if (nextProduct == null) continue;

//                    // 1. Lavado (Desde el anterior -> al de la cola)
//                    if (currentContextProduct != null)
//                    {
//                        var wash = _context.WashoutRules.GetMixerWashout(currentContextProduct.Category, nextProduct.Category);
//                        timelineSeconds += wash.GetValue(TimeUnits.Second);
//                    }

//                    // 2. Batch (Receta)
//                    timelineSeconds += _ai.GetMaxProductTime(nextProduct);
//                    var capacity = mixer.GetCapacity(nextProduct).GetValue(MassUnits.KiloGram);
//                    // 3. Descarga (Transferencia)
//                    double dischargeTime = capacity / (mixer.DischargeRate > 0 ? mixer.DischargeRate : 1);
//                    timelineSeconds += dischargeTime;

//                    // Actualizamos el contexto: el mixer queda sucio con ESTE producto
//                    currentContextProduct = nextProduct;
//                }
//            }

//            // --- C. COSTO DE ENTRADA PARA MÍ ---
//            // Cuánto tardará en lavarse para recibir MI producto
//            if (currentContextProduct != null)
//            {
//                var washForMe = _context.WashoutRules.GetMixerWashout(currentContextProduct.Category, productToMake.Category);
//                timelineSeconds += washForMe.GetValue(TimeUnits.Second);
//            }

//            // --- D. FACTOR OPERARIO (BLOQUEO CRUZADO) ---
//            // Si el mixer necesita operario para arrancar Y el operario está ocupado en OTRO lado
//            if (mixer.EngagementType == OperatorEngagementType.StartOnDefinedTime|| mixer.EngagementType == OperatorEngagementType.FullBatch)
//            {
//                if (mixer.BatchOperator != null &&
//                    mixer.BatchOperator.CurrentOwner != null &&
//                    mixer.BatchOperator.CurrentOwner != mixer)
//                {
//                    // Preguntamos al otro equipo cuánto le falta
//                    if (mixer.BatchOperator.CurrentOwner is BatchMixer otherMixer)
//                    {
//                        if (mixer.EngagementType == OperatorEngagementType.StartOnDefinedTime)
//                        {
//                            // Sumamos el tiempo que le falta al otro para liberar a "Juan"
//                            timelineSeconds += otherMixer.PendingOperatorRealse;
//                        }
//                        else
//                        {
//                            timelineSeconds += GetMixerMixerPendingBatchTime(otherMixer);
//                        }
//                    }
//                }
//            }
            

//            return (timelineSeconds / 60.0);
//        }
//        // --- EL CÁLCULO COMPLEJO DE DISPONIBILIDAD (TU LÓGICA DE ORO) ---
//        private double CalculateEstimatedAvailability(BatchMixer mixer, ProductDefinition productToMake, bool isPriority)
//        {

//            double BatchSizeKg = mixer.GetCapacity(productToMake).GetValue(MassUnits.KiloGram);
//            double timelineSeconds = CalculateEstimatedTimeAvailability(mixer, productToMake, isPriority, BatchSizeKg);

//            var result = BatchSizeKg / (timelineSeconds / 60.0);
//            return result; // Devolvemos Minutos
//        }
//        public double GetMixerMixerPendingBatchTime(BatchMixer mixer)
//        {
//            double timelineSeconds = 0;
//            double BatchSizeKg = mixer.GetCapacity(mixer.CurrentMaterial).GetValue(MassUnits.KiloGram);

//            if(mixer.InboundState is MixerStarvedByAtInitOperator)
//            {
//                if (mixer.LastMaterialProcessed != null && mixer.CurrentMaterial != null)
//                {
//                    var w = _context.WashoutRules.GetMixerWashout(mixer.LastMaterialProcessed.Category, mixer.CurrentMaterial.Category);
//                    timelineSeconds += w.GetValue(TimeUnits.Second);
//                }
    
//                // Si está esperando operario al inicio, asumimos que falta TODO el batch
//                if (mixer.CurrentMaterial != null)
//                {
//                    double total = _ai.GetMaxProductTime(mixer.CurrentMaterial);
//                    timelineSeconds += total;
//                    // Y falta descargar
//                    if (mixer.DischargeRate > 0)
//                        timelineSeconds += (BatchSizeKg / mixer.DischargeRate);
//                }
//            }
//            else if (mixer.InboundState is MixerManagingWashing || mixer.InboundState is MixerManagingWashingStarved)
//            {
//                // Asumimos peor caso: Falta el lavado completo (o podrías poner la mitad)
//                if (mixer.LastMaterialProcessed != null && mixer.CurrentMaterial != null)
//                {
//                    var w = _context.WashoutRules.GetMixerWashout(mixer.LastMaterialProcessed.Category, mixer.CurrentMaterial.Category);
//                    timelineSeconds += w.GetValue(TimeUnits.Second);
//                }
//            }
//            // Si está cocinando (Batching)
//            else if (mixer.InboundState is MixerFillingWithPump || mixer.InboundState is MixerProcessingTime || mixer.InboundState is MixerFillingManual)
//            {
//                double total = _ai.GetMaxProductTime(mixer.CurrentMaterial!);
//                double elapsed = mixer.NetBatchTimeInSeconds; // Usamos tu contador
//                double remaining = total - elapsed;
//                timelineSeconds += (remaining > 0 ? remaining : 0);

//                // Y falta descargar
//                if (mixer.DischargeRate > 0)
//                    timelineSeconds += (BatchSizeKg / mixer.DischargeRate);
//            }
//            // Si está descargando
//            else if (mixer.InboundState is MixerDischarging)
//            {
//                if (mixer.DischargeRate > 0)
//                    timelineSeconds += (mixer.CurrentMass / mixer.DischargeRate);
//            }

//            return timelineSeconds;
//        }
//    }
//}
