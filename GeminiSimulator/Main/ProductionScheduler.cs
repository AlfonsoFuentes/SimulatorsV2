using GeminiSimulator.Materials;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.PlantUnits.Tanks;
using System.Net.NetworkInformation;
using UnitSystem;

namespace GeminiSimulator.Main
{
    public record DemandLog(
         string TankName,
         string ProductName,
         double TimeToEmpty,
         string DecisionLogic,   // <--- EL DATO NUEVO: ¿Por qué no se programó?
         string AssignedMixer,   // <--- EL DATO NUEVO: Nombre exacto del mixer si se asignó
         string StatusColor,     // Para la UI (Success, Warning, Error, Info)
         bool IsInHouse
     );

    // 2. REGISTRO DE OFERTA (El "Dashboard" del Mixer)
    public record SupplyLog(
        string MixerName,
        string Operation,       // Ej: "Mixing", "Discharging"
        string CurrentProduct,
        string TargetTank,      // <--- EL DATO NUEVO: ¿Para quién cocina?
        double EtaMinutes,      // Tiempo para liberarse
        List<string> QueueList  // <--- EL DATO NUEVO: Nombres de los tanques en cola
    );
    public record Need(
            ProcessTank Tank,
            ProductDefinition Product,
            double SlackTimeMinutes,
            bool IsInHouse,
            bool IsUrgent
        );
    public class ProductionScheduler
    {
        public List<DemandLog> DemandSnapshot { get; private set; } = new();
        public List<SupplyLog> SupplySnapshot { get; private set; } = new();
        private readonly SimulationContext _context;
        private readonly OptimizationEngine _ai;
        public OptimizationEngine AiEngine => _ai;
        public List<Need> LastCalculationSnapshot { get; private set; } = new();
        public ProductionScheduler(SimulationContext context)
        {
            _context = context;
            _ai = new OptimizationEngine(); // Inicia el cerebro
        }

        // Dentro de ProductionScheduler.cs
        public void Execute(DateTime currentTime)
        {
            // =================================================================
            // PARTE A: LOGICA DE OFERTA (Mixers) - CÁLCULO INTELIGENTE (AI)
            // =================================================================
            var supplyList = new List<SupplyLog>();

            // Filtro de seguridad (Null check)
            var mixers = _context.Mixers.Values
                .Where(m => m.Materials != null && m.Materials.Any(mat => mat.IsFinishedProduct))
                .OrderBy(m => m.Name)
                .ToList();

            foreach (var mixer in mixers)
            {
                double timeToFree = 0;
                string targetTank = "--";
                var queueNames = mixer.WipsQueue.Select(t => t.Name).ToList();

                if (mixer.InboundState is not MixerIdle)
                {
                    // ---------------------------------------------------------
                    // 🧠 AI INTEGRATION: PREDICCIÓN DE TIEMPO
                    // ---------------------------------------------------------
                    // En lugar de "3600", le preguntamos al AI cuánto tarda ESTE producto en ESTE mixer.
                    // El AI aprende de los errores pasados.

                    double totalEst = 3600; // Valor por defecto de seguridad

                    if (mixer.CurrentMaterial != null)
                    {
                        // Asumo que tu método se llama PredictBatchTime o similar. 
                        // Si se llama diferente, ajusta esta línea:
                        totalEst = _ai.PredictBatchDuration(mixer, mixer.CurrentMaterial);
                    }

                    // Calculamos cuánto falta
                    double remaining = totalEst - mixer.NetBatchTimeInSeconds;

                    // Si el AI dice que ya debió terminar pero sigue rodando, mostramos 0 (Inminente)
                    // Esto le enseña al usuario que el proceso se está demorando más de lo previsto.
                    timeToFree = remaining > 0 ? remaining / 60.0 : 0;

                    targetTank = mixer.CurrentMaterial?.Name ?? "Processing...";
                }

                supplyList.Add(new SupplyLog(
                    mixer.Name,
                    mixer.InboundState?.StateName ?? "Off",
                    mixer.CurrentMaterial?.Name ?? "--",
                    targetTank,
                    timeToFree,
                    queueNames
                ));
            }
            SupplySnapshot = supplyList.OrderBy(x => x.EtaMinutes).ToList();

            // =================================================================
            // PARTE B: LOGICA DE DEMANDA (El Cerebro del Scheduler)
            // =================================================================

            var allNeeds = GatherAllNeeds();
            var sortedNeeds = allNeeds.OrderBy(n => n.SlackTimeMinutes).ToList();

            var logicTrace = new Dictionary<string, (string Reason, string Mixer, string Color)>();

            foreach (var need in sortedNeeds)
            {
                // 1. CÁLCULOS MATEMÁTICOS
                double virtualLevel = need.Tank.CurrentLevel + need.Tank.MassScheduledToReceive;
                double capacity = need.Tank.WorkingCapacity.GetValue(MassUnits.KiloGram);
                double utilization = capacity > 0 ? (virtualLevel / capacity) * 100 : 0;

                // --- CHECK 1: PIPELINE FULL? ---
                if (utilization > 95)
                {
                    logicTrace[need.Tank.Name] = ($"Pipeline Full (Virt: {utilization:F0}%)", "--", "Success");
                    continue;
                }

                // --- CHECK 2: ENTRADA FISICA OCUPADA? ---
                if (need.Tank.InboundState is TankReceiving)
                {
                    logicTrace[need.Tank.Name] = ("Physical Inlet Busy", "--", "Warning");
                    continue;
                }

                // --- CHECK 3: BUSCANDO NOVIO (MIXER) ---
                // IMPORTANTE: Tu método FindBestMixerFor DEBE usar _ai internamente
                // para elegir el mixer más rápido o eficiente según el aprendizaje.
                var bestMixer = FindBestMixerFor(need);

                if (bestMixer == null)
                {
                    logicTrace[need.Tank.Name] = ("No Compatible Mixer", "--", "Error");
                    continue;
                }

                // --- CHECK 4: ASIGNACIÓN EXITOSA ---
                if (need.IsInHouse) bestMixer.ReceivePriorityRequirementBatch(need.Tank);
                else bestMixer.ReceiveRequirementBatch(need.Tank);

                logicTrace[need.Tank.Name] = ("Assigned & Scheduled", bestMixer.Name, "Info");
            }

            // =================================================================
            // PARTE C: CONSTRUIR LA LISTA FINAL
            // =================================================================
            var demandList = new List<DemandLog>();

            var allTanks = _context.Tanks.Values
                    .OfType<ProcessTank>()
                    .Where(t => t is WipTank || t is InHouseTank)
                    .ToList();

            foreach (var tank in allTanks)
            {
                string reason = "Stable / No Demand";
                string mixer = "--";
                string color = "Default";
                double timeToEmpty = tank.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute);

                bool isVip = tank is InHouseTank;

                if (logicTrace.ContainsKey(tank.Name))
                {
                    var trace = logicTrace[tank.Name];
                    reason = trace.Reason;
                    mixer = trace.Mixer;
                    color = trace.Color;
                }
                else if (tank.MassScheduledToReceive > 0)
                {
                    reason = "Incoming Load (Prev Cycle)";
                    color = "Success";
                }
                else if (tank.CurrentLevel < 100)
                {
                    reason = "Idle / Not Configured";
                }

                demandList.Add(new DemandLog(
                    tank.Name,
                    tank.CurrentMaterial?.Name ?? "Empty",
                    timeToEmpty,
                    reason,
                    mixer,
                    color,
                    isVip
                ));
            }

            // =================================================================
            // PARTE D: ORDENAMIENTO POR PRIORIDAD (Weighted Score)
            // =================================================================
            DemandSnapshot = demandList
                .OrderBy(x =>
                {
                    if (x.TimeToEmpty < 60) return 0; // Prioridad Máxima

                    return x.StatusColor switch
                    {
                        "Error" => 0,
                        "Warning" => 1,
                        "Info" => 2,
                        "Success" => 3,  // Pipeline Full baja aquí
                        _ => 4
                    };
                })
                .ThenBy(x => x.TimeToEmpty)
                .ToList();
        }
        public void Execute23(DateTime currentTime)
        {
            // =================================================================
            // PARTE A: LOGICA DE OFERTA (Mixers) - SOLO PRODUCTO TERMINADO
            // =================================================================
            var supplyList = new List<SupplyLog>();

            // CORRECCIÓN 1: Null Check y Lambda clara (m => m...) para evitar errores
            var mixers = _context.Mixers.Values
                .Where(m => m.Materials != null && m.Materials.Any(mat => mat.IsFinishedProduct))
                .OrderBy(m => m.Name)
                .ToList();

            foreach (var mixer in mixers)
            {
                double timeToFree = 0;
                string targetTank = "--";

                // Copia de seguridad de la lista para evitar conflictos de hilos
                var queueNames = mixer.WipsQueue.Select(t => t.Name).ToList();

                if (mixer.InboundState is not MixerIdle)
                {
                    double totalEst = 3600; // Idealmente usar _ai.GetEstimatedTime...
                    double remaining = totalEst - mixer.NetBatchTimeInSeconds;
                    timeToFree = remaining > 0 ? remaining / 60.0 : 0;

                    targetTank = mixer.CurrentMaterial?.Name ?? "Processing...";
                }

                supplyList.Add(new SupplyLog(
                    mixer.Name,
                    mixer.InboundState?.StateName ?? "Off",
                    mixer.CurrentMaterial?.Name ?? "--",
                    targetTank,
                    timeToFree,
                    queueNames
                ));
            }
            SupplySnapshot = supplyList.OrderBy(x => x.EtaMinutes).ToList();

            // =================================================================
            // PARTE B: LOGICA DE DEMANDA (El Cerebro del Scheduler)
            // =================================================================

            var allNeeds = GatherAllNeeds();
            var sortedNeeds = allNeeds.OrderBy(n => n.SlackTimeMinutes).ToList();

            // Diccionario para trazar la decisión tomada para cada tanque
            var logicTrace = new Dictionary<string, (string Reason, string Mixer, string Color)>();

            foreach (var need in sortedNeeds)
            {
                // 1. CÁLCULOS MATEMÁTICOS
                double virtualLevel = need.Tank.CurrentLevel + need.Tank.MassScheduledToReceive;
                double capacity = need.Tank.WorkingCapacity.GetValue(MassUnits.KiloGram);

                // CORRECCIÓN 2: Evitar división por cero
                double utilization = capacity > 0 ? (virtualLevel / capacity) * 100 : 0;

                // --- CHECK 1: PIPELINE FULL? ---
                if (utilization > 95)
                {
                    logicTrace[need.Tank.Name] = ($"Pipeline Full (Virt: {utilization:F0}%)", "--", "Success");
                    continue;
                }

                // --- CHECK 2: ENTRADA FISICA OCUPADA? ---
                if (need.Tank.InboundState is TankReceiving)
                {
                    logicTrace[need.Tank.Name] = ("Physical Inlet Busy", "--", "Warning");
                    continue;
                }

                // --- CHECK 3: BUSCANDO NOVIO (MIXER) ---
                var bestMixer = FindBestMixerFor(need);

                if (bestMixer == null)
                {
                    logicTrace[need.Tank.Name] = ("No Compatible Mixer", "--", "Error");
                    continue;
                }

                // --- CHECK 4: ASIGNACIÓN EXITOSA ---
                if (need.IsInHouse) bestMixer.ReceivePriorityRequirementBatch(need.Tank);
                else bestMixer.ReceiveRequirementBatch(need.Tank);

                logicTrace[need.Tank.Name] = ("Assigned & Scheduled", bestMixer.Name, "Info");
            }

            // =================================================================
            // PARTE C: CONSTRUIR LA LISTA FINAL (WIPs e InHouse)
            // =================================================================
            var demandList = new List<DemandLog>();

            var allTanks = _context.Tanks.Values
                    .OfType<ProcessTank>()
                    .Where(t => t is WipTank || t is InHouseTank)
                    .ToList();

            foreach (var tank in allTanks)
            {
                string reason = "Stable / No Demand";
                string mixer = "--";
                string color = "Default";
                double timeToEmpty = tank.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute);

                // Detectar si es VIP (InHouse) para pasarlo al log
                bool isVip = tank is InHouseTank;

                if (logicTrace.ContainsKey(tank.Name))
                {
                    var trace = logicTrace[tank.Name];
                    reason = trace.Reason;
                    mixer = trace.Mixer;
                    color = trace.Color;
                }
                else if (tank.MassScheduledToReceive > 0)
                {
                    reason = "Incoming Load (Prev Cycle)";
                    color = "Success";
                }
                else if (tank.CurrentLevel < 100)
                {
                    reason = "Idle / Not Configured";
                }

                demandList.Add(new DemandLog(
                    tank.Name,
                    tank.CurrentMaterial?.Name ?? "Empty",
                    timeToEmpty,
                    reason,
                    mixer,
                    color,
                    isVip
                ));
            }

            // =================================================================
            // PARTE D: ORDENAMIENTO POR PRIORIDAD (Weighted Score)
            // =================================================================
            // CORRECCIÓN 3: Sistema de prioridades estricto
            // 0: CRÍTICO (Rojo / Vacío)
            // 1: WARNING (Naranja)
            // 2: INFO (Azul - Asignado)
            // 3: SEGURO (Verde - Pipeline Full) -> ¡Aquí bajan los verdes!
            // 4: DEFAULT (Gris)

            DemandSnapshot = demandList
                .OrderBy(x =>
                {
                    // Regla de Oro: Si queda menos de 1 hora, es prioridad máxima (0)
                    if (x.TimeToEmpty < 60) return 0;

                    return x.StatusColor switch
                    {
                        "Error" => 0,    // No hay mixer compatible
                        "Warning" => 1,  // Inlet Busy
                        "Info" => 2,     // Acaba de ser asignado
                        "Success" => 3,  // Pipeline Full / Incoming (BAJA PRIORIDAD VISUAL)
                        _ => 4           // Default / Stable
                    };
                })
                .ThenBy(x => x.TimeToEmpty) // Desempate: El más urgente arriba
                .ToList();
        }
        public void Execute22(DateTime currentTime)
        {
            // =================================================================
            // PARTE A: LOGICA DE OFERTA (Mixers) - ¿Qué están haciendo y para quién?
            // =================================================================
            var supplyList = new List<SupplyLog>();

            var mixers = _context.Mixers.Values.Where(x => x.Materials.Any(x => x.IsFinishedProduct)).ToList();
            foreach (var mixer in mixers)
            {
                double timeToFree = 0;
                string targetTank = "--";
                var queueNames = mixer.WipsQueue.Select(t => t.Name).ToList(); // Sacamos los nombres

                if (mixer.InboundState is not MixerIdle)
                {
                    // Calculamos ETA
                    double totalEst = 3600; // O _ai.GetEstimatedTime...
                    double remaining = totalEst - mixer.NetBatchTimeInSeconds;
                    timeToFree = remaining > 0 ? remaining / 60.0 : 0;

                    // INTENTAMOS AVERIGUAR PARA QUIÉN ESTÁ TRABAJANDO
                    // *NOTA*: Esto depende de tu modelo. Si el mixer no tiene propiedad 'TargetTank', 
                    // asumimos que lo que está en su 'CurrentMaterial' va para alguien, 
                    // pero si no tienes ese enlace explícito, ponemos "Batch in Progress".
                    // Si tu objeto Mixer tiene 'CurrentRequirement', úsalo:
                    // targetTank = mixer.CurrentRequirement?.Tank?.Name ?? "Unknown Dest.";

                    // Si no tienes esa propiedad, revisa si puedes deducirlo. 
                    // Por ahora pondré el producto, pero idealmente enlaza esto en tu Engine.
                    targetTank = mixer.CurrentMaterial?.Name ?? "Processing...";
                }

                supplyList.Add(new SupplyLog(
                    mixer.Name,
                    mixer.InboundState?.StateName ?? "Off",
                    mixer.CurrentMaterial?.Name ?? "--",
                    targetTank,
                    timeToFree,
                    queueNames
                ));
            }
            SupplySnapshot = supplyList.OrderBy(x => x.EtaMinutes).ToList();

            // =================================================================
            // PARTE B: LOGICA DE DEMANDA (Tanques) - El "Por Qué" de las decisiones
            // =================================================================

            var allNeeds = GatherAllNeeds();
            var sortedNeeds = allNeeds.OrderBy(n => n.SlackTimeMinutes).ToList();

            // Diccionario para guardar la "Razón Lógica" de cada tanque
            var logicTrace = new Dictionary<string, (string Reason, string Mixer, string Color)>();

            foreach (var need in sortedNeeds)
            {
                // 1. ESTADO ACTUAL
                double virtualLevel = need.Tank.CurrentLevel + need.Tank.MassScheduledToReceive;
                double capacity = need.Tank.WorkingCapacity.GetValue(MassUnits.KiloGram);
                double virtualSpace = capacity - virtualLevel;
                double utilization = (virtualLevel / capacity) * 100;

                // --- CHECK 1: PIPELINE FULL? ---
                // Si el tanque ya está virtualmente lleno (>95%), el Scheduler dice "STOP"
                if (utilization > 95)
                {
                    logicTrace[need.Tank.Name] = ($"Pipeline Full (Virt: {utilization:F0}%)", "--", "Success");
                    continue;
                }

                // --- CHECK 2: ENTRADA FISICA OCUPADA? ---
                if (need.Tank.InboundState is TankReceiving)
                {
                    logicTrace[need.Tank.Name] = ("Physical Inlet Busy (Receiving)", "--", "Warning");
                    continue;
                }

                // --- CHECK 3: BUSCANDO NOVIO (MIXER) ---
                var bestMixer = FindBestMixerFor(need);

                if (bestMixer == null)
                {
                    logicTrace[need.Tank.Name] = ("No Compatible Mixer Found", "--", "Error");
                    continue;
                }

                // --- CHECK 4: ASIGNACIÓN EXITOSA ---
                // Si llegamos aquí, ¡Asignamos!
                if (need.IsInHouse) bestMixer.ReceivePriorityRequirementBatch(need.Tank);
                else bestMixer.ReceiveRequirementBatch(need.Tank);

                logicTrace[need.Tank.Name] = ("Assigned & Scheduled", bestMixer.Name, "Info");
            }

            // =================================================================
            // PARTE C: CONSTRUIR LA LISTA FINAL (Uniendo todo)
            // =================================================================
            var demandList = new List<DemandLog>();

            var allTanks = _context.Tanks.Values
                    .OfType<ProcessTank>()
                    .Where(t => t is WipTank || t is InHouseTank) // <--- FILTRO POR TIPO
                    .ToList();
            foreach (var tank in allTanks)
            {
                string reason = "Stable / No Demand";
                string mixer = "--";
                string color = "Default";
                double timeToEmpty = tank.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute);

                // Si el tanque pasó por el "túnel de decisiones", recuperamos qué pasó
                if (logicTrace.ContainsKey(tank.Name))
                {
                    var trace = logicTrace[tank.Name];
                    reason = trace.Reason;
                    mixer = trace.Mixer;
                    color = trace.Color;
                }
                else if (tank.MassScheduledToReceive > 0)
                {
                    // Si no entró en la lógica de hoy pero tiene carga, es un remanente
                    reason = "Incoming Load (Previous Cycle)";
                    color = "Success";
                }
                else if (tank.CurrentLevel < 100) // Casi vacío y nadie lo miró
                {
                    // Si nadie lo miró y está vacío, es raro, quizás no tiene producto configurado
                    reason = "Idle / Not Configured";
                }

                demandList.Add(new DemandLog(
                    tank.Name,
                    tank.CurrentMaterial?.Name ?? "Empty",
                    timeToEmpty,
                    reason,        // <--- AQUI MOSTRAMOS LA LÓGICA
                    mixer,
                    color,
                    false // IsInHouse simplificado
                ));
            }

            // Ordenar: Los problemas (Rojo/Error) primero
            DemandSnapshot = demandList
                .OrderBy(x => x.StatusColor == "Default") // Los estables al fondo
                .ThenBy(x => x.StatusColor == "Success")  // Los atendidos después
                .ThenBy(x => x.TimeToEmpty)         // Los urgentes arriba
                .ToList();
        }
        //public void Execute2(DateTime currentTime)
        //{
        //    // ----------------------------------------------------------------------
        //    // PASO 1: RECOPILAR NECESIDADES
        //    // ----------------------------------------------------------------------
        //    var allNeeds = GatherAllNeeds();
        //    var sortedNeeds = allNeeds
        //        .Where(n => n.IsUrgent)
        //        .OrderBy(n => n.SlackTimeMinutes)
        //        .ToList();

        //    // Guardamos la foto cruda de necesidades (opcional, por si la usas en otro lado)
        //    LastCalculationSnapshot = sortedNeeds;

        //    // ----------------------------------------------------------------------
        //    // PASO 2: ASIGNACIÓN DE LOTES (TU LÓGICA DE PIPELINE)
        //    // ----------------------------------------------------------------------
        //    // Diccionario temporal para guardar quién fue el "Mixer Elegido" en este ciclo
        //    // Clave: TankName, Valor: MixerName
        //    var decisionsInThisCycle = new Dictionary<string, string>();

        //    foreach (var need in sortedNeeds)
        //    {
        //        // 1. Calculamos el "Nivel Virtual": Lo que tengo físico + Lo que ya me prometieron
        //        double virtualLevel = need.Tank.CurrentLevel + need.Tank.MassScheduledToReceive;

        //        // 2. Calculamos el "Espacio Virtual"
        //        double virtualSpace = need.Tank.WorkingCapacity.GetValue(MassUnits.KiloGram) - virtualLevel;

        //        // 3. Estimamos el tamaño del siguiente batch
        //        var capableMixers = _context.Mixers.Values.Where(m => m.CanProcess(need.Product)).ToList();
        //        double estimatedNextBatch = capableMixers.Any()
        //            ? capableMixers.Max(m => m.GetCapacity(need.Product).GetValue(MassUnits.KiloGram))
        //            : 2000;

        //        // 4. EL FRENO DE MANO: Si no cabe otro batch completo (con factor de seguridad), pasamos.
        //        if (virtualSpace < (estimatedNextBatch * 0.95))
        //        {
        //            decisionsInThisCycle[need.Tank.Name] = "Pipeline Full";
        //            continue;
        //        }

        //        // 5. VALIDACIÓN DE ENTRADA FÍSICA
        //        if (need.Tank.InboundState is TankReceiving || need.Tank.InboundState is TankInletStarvedHighLevel)
        //        {
        //            decisionsInThisCycle[need.Tank.Name] = "Receiving Now";
        //            continue;
        //        }

        //        // 6. BUSCAR Y ASIGNAR MIXER
        //        var bestMixer = FindBestMixerFor(need);

        //        if (bestMixer != null)
        //        {
        //            if (need.IsInHouse)
        //                bestMixer.ReceivePriorityRequirementBatch(need.Tank);
        //            else
        //                bestMixer.ReceiveRequirementBatch(need.Tank);

        //            // Guardamos la decisión para mostrarla en la UI
        //            decisionsInThisCycle[need.Tank.Name] = bestMixer.Name;
        //        }
        //        else
        //        {
        //            decisionsInThisCycle[need.Tank.Name] = "No Mixer";
        //        }
        //    }

        //    // ----------------------------------------------------------------------
        //    // PASO 3: GENERAR SNAPSHOT DE OFERTA (MIXERS) PARA LA UI
        //    // ----------------------------------------------------------------------
        //    var supplyList = new List<SupplyLog>();
        //    foreach (var mixer in _context.Mixers.Values)
        //    {
        //        double timeToFree = 0;
        //        if (mixer.InboundState is not MixerIdle && mixer.CurrentMaterial != null)
        //        {
        //            double totalEst = 3600; // Intenta usar _ai.GetEstimatedTime(...) si puedes
        //            double remainingSec = totalEst - mixer.NetBatchTimeInSeconds;
        //            timeToFree = remainingSec > 0 ? remainingSec / 60.0 : 0;
        //        }

        //        supplyList.Add(new SupplyLog(
        //            mixer.Name,
        //            mixer.InboundState?.StateName ?? "Unknown",
        //            mixer.CurrentMaterial?.Name ?? "--",
        //            timeToFree,
        //            mixer.WipsQueue.Count
        //        ));
        //    }
        //    SupplySnapshot = supplyList.OrderBy(x => x.TimeToFreeMinutes).ToList();

        //    // ----------------------------------------------------------------------
        //    // PASO 4: GENERAR SNAPSHOT DE DEMANDA (TANQUES) PARA LA UI
        //    // ----------------------------------------------------------------------
        //    // IMPORTANTE: Iteramos sobre TODOS los tanques de proceso, no solo 'sortedNeeds'
        //    var demandList = new List<DemandLog>();
        //    var allProcessTanks = _context.Tanks.Values
        //            .OfType<ProcessTank>()
        //            .Where(t => t is WipTank || t is InHouseTank) // <--- FILTRO POR TIPO
        //            .ToList();
        //    foreach (var tank in allProcessTanks)
        //    {
        //        // Buscamos si este tanque tenía una necesidad activa en este ciclo
        //        var need = sortedNeeds.FirstOrDefault(n => n.Tank == tank);

        //        // Datos básicos
        //        string tankName = tank.Name;
        //        string prodName = tank.CurrentMaterial?.Name ?? (need?.Product.Name ?? "Empty");
        //        double currentLevel = tank.CurrentLevel;
        //        double timeToEmpty = tank.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute);

        //        // Datos de decisión
        //        double slack = need != null ? need.SlackTimeMinutes : 9999;
        //        bool isInHouse = need?.IsInHouse ?? false;

        //        // Estado y Target Mixer
        //        string status = "Stable";
        //        string targetMixer = "--";

        //        // Intentamos recuperar qué decisión se tomó arriba
        //        if (decisionsInThisCycle.ContainsKey(tankName))
        //        {
        //            targetMixer = decisionsInThisCycle[tankName]; // Puede ser "Mixer A", "Pipeline Full", etc.
        //        }
        //        else if (need != null)
        //        {
        //            // Si estaba en needs pero no entró al diccionario, asumimos Waiting
        //            var best = FindBestMixerFor(need);
        //            targetMixer = best?.Name ?? "None";
        //        }

        //        // Definimos el Status visual
        //        if (tank.MassScheduledToReceive > 0)
        //        {
        //            status = "In Pipeline";
        //            if (targetMixer == "--") targetMixer = "Incoming...";
        //        }
        //        else if (slack < 0)
        //        {
        //            status = "Late Start";
        //        }
        //        else if (need != null)
        //        {
        //            status = "Needs Product";
        //        }
        //        else if (currentLevel >= tank.WorkingCapacity.GetValue(MassUnits.KiloGram) * 0.95)
        //        {
        //            status = "Full";
        //        }

        //        demandList.Add(new DemandLog(
        //            tankName,
        //            prodName,
        //            currentLevel,
        //            timeToEmpty,
        //            slack,
        //            targetMixer,
        //            status,
        //            isInHouse
        //        ));
        //    }

        //    // Ordenamos para la vista: Urgentes primero
        //    DemandSnapshot = demandList
        //        .OrderBy(x => x.SlackMinutes)
        //        .ThenBy(x => x.TimeToEmptyMinutes)
        //        .ToList();
        //}
        //public void Execute3(DateTime currentTime)
        //{
        //    var allNeeds = GatherAllNeeds();
        //    var sortedNeeds = allNeeds
        //        .Where(n => n.IsUrgent)
        //        .OrderBy(n => n.SlackTimeMinutes)
        //        .ToList();
        //    LastCalculationSnapshot = sortedNeeds;
        //    var supplyList = new List<SupplyLog>();
        //    foreach (var mixer in _context.Mixers.Values)
        //    {
        //        // Calculamos cuándo se libera este mixer
        //        double timeToFree = 0;
        //        if (mixer.InboundState is not MixerIdle && mixer.CurrentMaterial != null)
        //        {
        //            // Tiempo Total Estimado - Tiempo Transcurrido
        //            // (Asegúrate de tener acceso al estimado, o usa un promedio)
        //            double totalEst = 3600; // O _ai.GetEstimatedTime(...)
        //            double remainingSec = totalEst - mixer.NetBatchTimeInSeconds;
        //            timeToFree = remainingSec > 0 ? remainingSec / 60.0 : 0;
        //        }

        //        supplyList.Add(new SupplyLog(
        //            mixer.Name,
        //            mixer.InboundState?.StateName ?? "Unknown",
        //            mixer.CurrentMaterial?.Name ?? "--",
        //            timeToFree,
        //            mixer.WipsQueue.Count
        //        ));
        //    }
        //    // Ordenamos: Los que se liberan primero van arriba
        //    SupplySnapshot = supplyList.OrderBy(x => x.MixerName).ToList();
        //    var demandList = new List<DemandLog>();
        //    foreach (var need in sortedNeeds)
        //    {
        //        // ----------------------------------------------------------------------
        //        // CORRECCIÓN: LOGICA DE PIPELINE (LLENADO MÚLTIPLE)
        //        // ----------------------------------------------------------------------

        //        // 1. Calculamos el "Nivel Virtual": Lo que tengo físico + Lo que ya me prometieron
        //        double virtualLevel = need.Tank.CurrentLevel + need.Tank.MassScheduledToReceive;

        //        // 2. Calculamos el "Espacio Virtual": El hueco que quedaría si todo lo prometido llegara YA
        //        double virtualSpace = need.Tank.WorkingCapacity.GetValue(MassUnits.KiloGram) - virtualLevel;

        //        // 3. Estimamos el tamaño del siguiente batch (Max Mixer Capacity para seguridad)
        //        var capableMixers = _context.Mixers.Values.Where(m => m.CanProcess(need.Product)).ToList();
        //        double estimatedNextBatch = capableMixers.Any()
        //            ? capableMixers.Max(m => m.GetCapacity(need.Product).GetValue(MassUnits.KiloGram))
        //            : 2000;

        //        // 4. EL NUEVO FRENO:
        //        // Solo dejamos de pedir si NO cabe otro batch completo encima de lo que ya viene.
        //        // Usamos un factor de seguridad (0.95) para no llenar hasta el borde matemático.
        //        if (virtualSpace < (estimatedNextBatch * 0.95))
        //        {
        //            continue; // Ya está lleno el pipeline, no cabe ni uno más.
        //        }

        //        // ----------------------------------------------------------------------
        //        // VALIDACIÓN DE ENTRADA FÍSICA (Para no chocar trenes)
        //        // ----------------------------------------------------------------------
        //        // Aunque queramos pedir más, si el tanque está FÍSICAMENTE recibiendo producto AHORA MISMO,
        //        // esperamos a que termine esa transferencia para no bloquear bombas/válvulas.
        //        // NOTA: Esto depende de si tu tanque tiene 1 o varias entradas. Si tiene 1, descomenta esto:

        //        if (need.Tank.InboundState is TankReceiving || need.Tank.InboundState is TankInletStarvedHighLevel)
        //        {
        //            continue;
        //        }


        //        // --- SI PASAMOS AQUÍ, PODEMOS ENCOLAR OTRO BATCH ---

        //        var bestMixer = FindBestMixerFor(need);

        //        if (bestMixer != null)
        //        {
        //            if (need.IsInHouse)
        //                bestMixer.ReceivePriorityRequirementBatch(need.Tank);
        //            else
        //                bestMixer.ReceiveRequirementBatch(need.Tank);

        //            // IMPORTANTE: Al asignarlo, el BatchMixer internamente DEBE sumar 
        //            // a need.Tank.MassScheduledToReceive inmediatamente, 
        //            // para que en la siguiente vuelta del loop, 'virtualLevel' suba y frene si es necesario.
        //        }
        //        string status = "Waiting";
        //        if (need.Tank.MassScheduledToReceive > 0) status = "In Pipeline";
        //        else if (need.SlackTimeMinutes < 0) status = "Late Start";
        //        demandList.Add(new DemandLog(
        //            need.Tank.Name,
        //            need.Product.Name,
        //            need.Tank.CurrentLevel,
        //            need.Tank.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute),
        //            need.SlackTimeMinutes,
        //            bestMixer?.Name ?? "None", // Quién lo va a salvar
        //            status,
        //            need.IsInHouse
        //        ));
        //        DemandSnapshot = demandList
        //  .OrderBy(x => x.SlackMinutes)
        //  .ThenBy(x => x.TimeToEmptyMinutes)
        //  .ToList();
        //    }
        //}


        // --- CLASE AUXILIAR PARA GUARDAR DATOS DEL ANÁLISIS ---


        // --- FASE 1: RECOLECCIÓN ---
        private List<Need> GatherAllNeeds()
        {
            var needs = new List<Need>();

            // A. Analizar WIPs de Líneas (Consumo normal)
            foreach (var line in _context.Lines.Values)
            {
                if (line.CurrentWipTank == null || line.CurrentOrder == null) continue;

                // Analizamos el tanque con lógica "Normal" (false)
                var need = AnalyzeTank(line.CurrentWipTank, line.CurrentOrder.Material!, false);
                if (need != null) needs.Add(need);
            }

            // B. Analizar Tanques InHouse (Prioridad Crítica)
            foreach (var tank in _context.TanksInHouse)
            {
                if (tank.CurrentMaterial == null) continue;

                // Analizamos el tanque con lógica "InHouse" (true)
                var need = AnalyzeTank(tank, tank.CurrentMaterial, true);
                if (need != null) needs.Add(need);
            }

            return needs;
        }

        // --- FASE 2: ANÁLISIS DE URGENCIA (MATEMÁTICA PURA) ---
        // Dentro de ProductionScheduler.cs

        private Need? AnalyzeTank(ProcessTank tank, ProductDefinition product, bool isInHouse)
        {
            // -------------------------------------------------------------
            // CORRECCIÓN: ESTIMAR EL TAMAÑO REAL DEL LOTE (EL "BALDE")
            // -------------------------------------------------------------

            // 1. Buscamos qué mixers pueden hacer este producto para saber el tamaño típico del lote.
            var capableMixers = _context.Mixers.Values.Where(m => m.CanProcess(product)).ToList();

            // Si no hay mixers, asumimos algo seguro (ej. 2000kg) o la capacidad del tanque si es pequeño.
            // Esto evita división por cero.
            double estimatedBatchSize = capableMixers.Any()
      ? capableMixers.Max(m => m.GetCapacity(product).GetValue(MassUnits.KiloGram)) // <--- CAMBIO AQUI
      : 2000;

            // -------------------------------------------------------------
            // CASO 1: VALIDACIÓN DE ESPACIO FUTURO (Predictivo)
            // -------------------------------------------------------------

            double currentLevel = tank.CurrentLevel;
            double currentSpace = tank.WorkingCapacity.GetValue(MassUnits.KiloGram) - currentLevel;

            // 2. ¿Cuánto tardará el mixer en llegar?
            double estimatedBatchTimeSec = _ai.GetMaxProductTime(product);

            // 3. ¿Cuánto va a consumir la línea mientras cocinamos? (Proyección)
            double consumptionPerSec = tank.AverageOutleFlow.GetValue(MassFlowUnits.Kg_sg);
            double projectedConsumption = consumptionPerSec * estimatedBatchTimeSec;

            // 4. ESPACIO PROYECTADO (El hueco que habrá en el futuro)
            double projectedSpace = currentSpace + projectedConsumption;

            // REGLA CORREGIDA:
            // Comparamos el hueco futuro contra el TAMAÑO DEL LOTE (Mixer), no contra el tanque entero.
            // Si el hueco futuro es menor que el lote del mixer, esperamos.
            if (projectedSpace < (estimatedBatchSize * 0.95))
            {
                return null; // Aún no cabe un lote completo. Esperar.
            }

            // -------------------------------------------------------------
            // CÁLCULO DE URGENCIA (Igual que antes)
            // -------------------------------------------------------------

            double timeToEmptyMin = tank.PendingTimeToEmptyVessel.GetValue(TimeUnits.Minute);
            if (timeToEmptyMin == 0 && currentLevel > 100) timeToEmptyMin = 9999;

            double estimatedBatchTimeMin = estimatedBatchTimeSec / 60.0;
            double slack = timeToEmptyMin - estimatedBatchTimeMin;
            double triggerThreshold = isInHouse ? 120 : 60;

            bool isUrgent = slack <= triggerThreshold;

            // Filtro final anti-rebose
            if (tank.CurrentLevel >= tank.WorkingCapacity.GetValue(MassUnits.KiloGram) * 0.98) isUrgent = false;

            return new Need(tank, product, slack, isInHouse, isUrgent);
        }

        // --- FASE 3: SELECCIÓN DEL MEJOR MIXER (CALCULADORA DE TIEMPO) ---
        private BatchMixer? FindBestMixerFor(Need need)
        {
            // 1. Filtrar: Solo mixers que físicamente puedan hacer el producto
            var candidates = _context.Mixers.Values
                .Where(m => m.CanProcess(need.Product))
                .ToList();

            if (!candidates.Any()) return null;

            // 2. Calcular ETA (Estimated Time of Arrival) para cada uno
            var scoredCandidates = candidates.Select(mixer => new
            {
                Mixer = mixer,
                // Calculamos cuándo estará libre este mixer para MÍ
                MinutesToWait = CalculateEstimatedAvailability(mixer, need.Product, need.IsInHouse)
            });

            // 3. Ganador: El que tenga menor tiempo de espera
            return scoredCandidates
                .OrderBy(x => x.MinutesToWait)
                .First().Mixer;
        }

        // --- EL CÁLCULO COMPLEJO DE DISPONIBILIDAD (TU LÓGICA DE ORO) ---
        private double CalculateEstimatedAvailability(BatchMixer mixer, ProductDefinition productToMake, bool isPriority)
        {
            double timelineSeconds = 0;

            // Rastreamos qué producto deja sucio al mixer en cada paso mental
            ProductDefinition? currentContextProduct = mixer.CurrentMaterial ?? mixer.LastMaterialProcessed;

            // --- A. ESTADO ACTUAL (Lo que ya está haciendo) ---
            if (mixer.InboundState is not MixerIdle)
            {
                // Si está lavando
                if (mixer.InboundState is MixerManagingWashing || mixer.InboundState is MixerManagingWashingStarved)
                {
                    // Asumimos peor caso: Falta el lavado completo (o podrías poner la mitad)
                    if (mixer.LastMaterialProcessed != null && mixer.CurrentMaterial != null)
                    {
                        var w = _context.WashoutRules.GetMixerWashout(mixer.LastMaterialProcessed.Category, mixer.CurrentMaterial.Category);
                        timelineSeconds += w.GetValue(TimeUnits.Second);
                    }
                }
                // Si está cocinando (Batching)
                else if (mixer.InboundState is MixerFillingWithPump || mixer.InboundState is MixerProcessingTime || mixer.InboundState is MixerFillingManual)
                {
                    double total = _ai.GetMaxProductTime(mixer.CurrentMaterial!);
                    double elapsed = mixer.NetBatchTimeInSeconds; // Usamos tu contador
                    double remaining = total - elapsed;
                    timelineSeconds += (remaining > 0 ? remaining : 0);

                    // Y falta descargar
                    if (mixer.DischargeRate > 0)
                        timelineSeconds += (mixer.Capacity / mixer.DischargeRate);
                }
                // Si está descargando
                else if (mixer.InboundState is MixerDischarging)
                {
                    if (mixer.DischargeRate > 0)
                        timelineSeconds += (mixer.CurrentMass / mixer.DischargeRate);
                }
            }

            // --- B. LA COLA DE ESPERA (Future Simulation) ---
            // Si soy VIP (InHouse), me salto esta parte (no sumo el tiempo de la cola)
            if (!isPriority)
            {
                foreach (var queuedTank in mixer.WipsQueue)
                {
                    var nextProduct = queuedTank.CurrentMaterial;
                    if (nextProduct == null) continue;

                    // 1. Lavado (Desde el anterior -> al de la cola)
                    if (currentContextProduct != null)
                    {
                        var wash = _context.WashoutRules.GetMixerWashout(currentContextProduct.Category, nextProduct.Category);
                        timelineSeconds += wash.GetValue(TimeUnits.Second);
                    }

                    // 2. Batch (Receta)
                    timelineSeconds += _ai.GetMaxProductTime(nextProduct);

                    // 3. Descarga (Transferencia)
                    double dischargeTime = queuedTank.MassScheduledToReceive / (mixer.DischargeRate > 0 ? mixer.DischargeRate : 1);
                    timelineSeconds += dischargeTime;

                    // Actualizamos el contexto: el mixer queda sucio con ESTE producto
                    currentContextProduct = nextProduct;
                }
            }

            // --- C. COSTO DE ENTRADA PARA MÍ ---
            // Cuánto tardará en lavarse para recibir MI producto
            if (currentContextProduct != null)
            {
                var washForMe = _context.WashoutRules.GetMixerWashout(currentContextProduct.Category, productToMake.Category);
                timelineSeconds += washForMe.GetValue(TimeUnits.Second);
            }

            // --- D. FACTOR OPERARIO (BLOQUEO CRUZADO) ---
            // Si el mixer necesita operario para arrancar Y el operario está ocupado en OTRO lado
            if (mixer.EngagementType == OperatorEngagementType.StartOnDefinedTime)
            {
                if (mixer.BatchOperator != null &&
                    mixer.BatchOperator.CurrentOwner != null &&
                    mixer.BatchOperator.CurrentOwner != mixer)
                {
                    // Preguntamos al otro equipo cuánto le falta
                    if (mixer.BatchOperator.CurrentOwner is BatchMixer otherMixer)
                    {
                        // Sumamos el tiempo que le falta al otro para liberar a "Juan"
                        timelineSeconds += otherMixer.PendingOperatorRealse;
                    }
                }
            }

            return timelineSeconds / 60.0; // Devolvemos Minutos
        }
    }
}
