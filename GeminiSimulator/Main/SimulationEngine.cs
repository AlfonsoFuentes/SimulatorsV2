//namespace GeminiSimulator.Main
//{
//    public class SimulationEngine
//    {
//        private ProductionScheduler _scheduler;
//        public ProductionScheduler Scheduler => _scheduler;
//        private readonly SimulationContext _context;
//        private readonly SimulationScenario _scenario;
//        public EngineStatus Status { get; private set; } = EngineStatus.Stopped;
//        private List<PlantUnit> _executionOrder = new();

//        public List<PlantUnit> RawMaterialVessels { get; set; } = new();
//        public List<BatchMixer> Mixers => _executionOrder.OfType<BatchMixer>().Where(x => x.Materials.Any(x => x.IsFinishedProduct)).ToList();
//        public List<PlantUnit> WipTanks { get; set; } = new();
//        public List<PackagingLine> Lines => _executionOrder.OfType<PackagingLine>().ToList();
//        public DateTime CurrentTime { get; private set; }
//        public SimulationEngine(SimulationContext context, SimulationScenario scenario)
//        {
//            _context = context;
//            _scenario = scenario;
//            _scheduler = new ProductionScheduler(context); // <--- NUEVO

//        }


//        private void BuildExecutionHierarchy()
//        {
//            // Limpiamos y preparamos para el ordenamiento
//            _executionOrder.Clear();
//            var visited = new HashSet<Guid>();
//            var queue = new Queue<PlantUnit>();

//            // 1. Empezamos por las "Sinks" (las 13 líneas de envasado)
//            var lines = _context.AllUnits.Values
//                .Where(u => u.Type == ProcessEquipmentType.Line);

//            foreach (var line in lines) queue.Enqueue(line);

//            var mixers = _context.AllUnits.Values.OfType<BatchMixer>().ToList();

//            foreach (var mixer in mixers) mixer.SetOptimizationEngine(_scheduler.AiEngine);

//            // 2. Navegamos hacia atrás por las conexiones de entrada (Inputs)
//            while (queue.Count > 0)
//            {
//                var current = queue.Dequeue();

//                if (visited.Contains(current.Id)) continue;

//                visited.Add(current.Id);
//                _executionOrder.Add(current);

//                // Buscamos aguas arriba (Upstream)
//                foreach (var inputUnit in current.Inputs)
//                {
//                    queue.Enqueue(inputUnit);
//                }
//            }
//            CreateReporList();
//        }

//        public double SpeedFactor { get; set; } = 100.0;

//        private CancellationTokenSource? _cts;

//        // --- MANDOS DE CONTROL ---

//        public void Pause() => Status = EngineStatus.Paused;

//        public void Resume() => Status = EngineStatus.Running;

//        public void Stop()
//        {
//            Status = EngineStatus.Stopped;
//            _cts?.Cancel();
//        }
//        public Func<Task> UpdateModel { get; set; } = null!;

//        public SimulationSnapshot CurrentSnapshot { get; private set; } = null!;
//        public async Task RunAsync(CancellationTokenSource ct)
//        {
//            _cts = ct;

//            // 1. Preparación
//            BuildExecutionHierarchy();
//            CurrentTime = _scenario.StartTime;
//            foreach (var unit in _executionOrder) unit.PrepareUnit(CurrentTime);

//            Status = EngineStatus.Running;

//            // Ejecutamos en un hilo aparte para no bloquear nada
//            await Task.Run(async () =>
//            {
//                while (CurrentTime <= _scenario.EndTime && Status != EngineStatus.Stopped)
//                {
//                    if (ct.IsCancellationRequested) break;

//                    // --- PAUSA ---
//                    if (Status == EngineStatus.Paused)
//                    {
//                        await Task.Delay(100, ct.Token);
//                        continue;
//                    }

//                    // --- 2. CÁLCULO (A máxima velocidad posible) ---

//                    // OPTIMIZACIÓN: ¿Realmente necesitas recalcular el Scheduler cada segundo?
//                    // Si el scheduler es pesado, puedes ejecutarlo solo cada minuto simulado:
//                    // if (CurrentTime.Second == 0) _scheduler.Execute(CurrentTime);
//                    _scheduler.Execute(CurrentTime);

//                    foreach (var unit in _executionOrder)
//                    {
//                        unit.Calculate(CurrentTime);
//                    }

//                    // --- 3. ACTUALIZACIÓN DE ESTADO (MEMORY ONLY) ---
//                    // Aquí está el truco: NO llamamos a progress.Report().
//                    // Solo actualizamos la propiedad pública. Es una operación de memoria instantánea (nanosegundos).
//                    // La UI leerá esta propiedad cada 50ms desde su propio Timer.

//                    var snapshot = CreateSnapshot(CurrentTime);
//                    snapshot.Scheduler = _scheduler;

//                    // Actualizamos la variable pública que lee la UI
//                    CurrentSnapshot = snapshot;

//                    // (Opcional) Si quieres reportar progreso solo para una barra de carga lenta,
//                    // hazlo con Throttle (ej: cada 100 ticks simulados).
//                    // if (CurrentTime.Second % 10 == 0 && progress != null) progress.Report(snapshot);


//                    // --- 4. CONTROL DE VELOCIDAD ---
//                    _context.TotalSimulationSpan += TimeSpan.FromSeconds(1);
//                    CurrentTime = CurrentTime.AddSeconds(1);

//                    // Solo frenamos si el usuario quiere ir más lento que la capacidad máxima del CPU
//                    if (SpeedFactor > 0 && SpeedFactor < 100) // Si es 100 (Max), no frenamos
//                    {
//                        // Lógica simple de delay
//                        // A velocidades altas, Task.Delay es impreciso.
//                        // Si SpeedFactor es alto, mejor no hacer Delay en cada vuelta, sino cada X vueltas.

//                        int delayMs = (int)(1000 / SpeedFactor);
//                        if (delayMs > 15) await Task.Delay(delayMs, ct.Token);
//                        // Si el delay es muy pequeño (<15ms), Windows no lo respeta bien. 
//                        // Mejor dejar correr libre o usar un SpinWait si quieres precisión extrema,
//                        // pero para Blazor WebAssembly, dejar correr libre es mejor.
//                    }
//                    await UpdateModel();
//                }

//                Status = EngineStatus.Stopped;
//            }, ct.Token);
//        }
//        public async Task RunAsync2(IProgress<SimulationSnapshot> progress, CancellationTokenSource ct)
//        {
//            _cts = ct;
//            // 1. Fase de Preparación
//            BuildExecutionHierarchy();
//            CurrentTime = _scenario.StartTime;
//            foreach (var unit in _executionOrder) unit.PrepareUnit(CurrentTime);

//            Status = EngineStatus.Running;

//            await Task.Run(async () =>
//            {
//                while (CurrentTime <= _scenario.EndTime && Status != EngineStatus.Stopped)
//                {
//                    if (ct.IsCancellationRequested) break;

//                    // --- MANEJO DE PAUSA ---
//                    if (Status == EngineStatus.Paused)
//                    {
//                        await Task.Delay(100, ct.Token);
//                        continue;
//                    }
//                    _scheduler.Execute(CurrentTime);
//                    // --- 2. CÁLCULO (Segundo a Segundo) ---
//                    foreach (var unit in _executionOrder)
//                    {
//                        unit.Calculate(CurrentTime);
//                    }

//                    // --- 3. REPORTE (Cada segundo de simulación se envía a la UI) ---
//                    var snapshot = CreateSnapshot(CurrentTime);
//                    snapshot.Scheduler = _scheduler;
//                    progress.Report(snapshot);


//                    // --- 4. CONTROL DE VELOCIDAD (Retraso de Visualización) ---
//                    if (SpeedFactor > 0)
//                    {
//                        // Calculamos el delay: 1000ms / 2.0 = 500ms de espera.
//                        int delayMs = (int)(1000 / SpeedFactor);

//                        // Si el delay es muy corto, usamos Yield para mantener la fluidez de la UI
//                        if (delayMs < 15) await Task.Yield();
//                        else await Task.Delay(delayMs, ct.Token);
//                    }
//                    // Si SpeedFactor es 0, no hay delay: corre a la velocidad del CPU.
//                    _context.TotalSimulationSpan += TimeSpan.FromSeconds(1);
//                    CurrentTime = CurrentTime.AddSeconds(1);
//                }

//                Status = EngineStatus.Stopped;
//            }, ct.Token);


//        }

//        private SimulationSnapshot CreateSnapshot(DateTime time)
//        {
//            var snapshot = new SimulationSnapshot { CurrentSimTime = time };

//            foreach (var unit in _executionOrder)
//            {
//                // Aquí ocurre la magia: El Engine no sabe qué hay dentro, 
//                // solo pide el reporte y el equipo entrega sus datos y estilos.
//                snapshot.EquipmentData.Add(unit.Id, unit.GetReportData());
//            }

//            return snapshot;
//        }


//        // Actualizamos GetUnitsByGroup para pasar la UNIDAD completa, no solo el tipo

//        void CreateReporList()
//        {
//            RawMaterialTanks = _executionOrder.OfType<InHouseTank>().ToList();
//            var rawmtaerial = _executionOrder.OfType<RawMaterialTank>().ToList();
//            RawMaterialTanks = RawMaterialTanks.Concat(rawmtaerial).OrderBy(x => x.Name);
//            Manufacture = _executionOrder.OfType<EquipmentManufacture>().OrderBy(x => x.Name);
//            WipLinePairList = GetWipLinePairs();
//        }
//        public IEnumerable<PlantUnit> RawMaterialTanks = new List<PlantUnit>();

//        public IEnumerable<PlantUnit> Manufacture = new List<PlantUnit>();

//        // Obtiene los datos específicos de una unidad desde el snapshot global


//        // Crea los pares para la columna dual (WIP frente a su Línea)
//        public IEnumerable<WipLinePair> WipLinePairList = new List<WipLinePair>();
//        private IEnumerable<WipLinePair> GetWipLinePairs()
//        {
//            // Aquí podrías tener una lógica de configuración o buscar por nombre/conexión
//            // Por ahora, asumimos que existe una relación 1:1 en el orden
//            var wips = _executionOrder.OfType<WipTank>().ToList();
//            var lines = _executionOrder.OfType<PackagingLine>().ToList();
//            List<WipLinePair> result = new();

//            foreach (var line in lines)
//            {
//                var wiptankss = line.Inputs.SelectMany(x => x.Inputs.OfType<WipTank>()).ToList();

//                foreach (var wiptank in wiptankss)
//                {
//                    result.Add(new WipLinePair(wiptank.Id, line.Id));
//                }
//            }
//            return result;
//            //for (int i = 0; i < Math.Min(wips.Count, lines.Count); i++)
//            //{
//            //    yield return new WipLinePair(wips[i].Id, lines[i].Id);
//            //}
//        }
//        public enum EquipmentGroup
//        {
//            RawMaterial,
//            Manufacturing, // Includes Mixers and Continuous Systems
//            WIP,
//            Packaging
//        }
//        public record WipLinePair(Guid WipId, Guid LineId);
//    }
//    public class SimulationSnapshot
//    {
//        public DateTime CurrentSimTime { get; set; }

//        // El ID del equipo es la llave, y su diccionario de campos con estilo es el valor
//        public Dictionary<Guid, Dictionary<string, ReportField>> EquipmentData { get; set; } = new();
//        public ProductionScheduler Scheduler { get; set; } = null!;
//    }




//}
