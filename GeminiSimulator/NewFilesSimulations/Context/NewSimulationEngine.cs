using GeminiSimulator.Main;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.Enums;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    public enum EngineStatus
    {
        None,
        Stopped,
        Running,
        Paused
    }
    public class NewSimulationEngine
    {


        private ManufactureScheduler _scheduler;
        public ManufactureScheduler Scheduler => _scheduler;
        private readonly NewSimulationContext _context;
        private readonly NewSimulationScenario _scenario;
        public EngineStatus Status { get; private set; } = EngineStatus.Stopped;
        private List<NewPlantUnit> _executionOrder = new();


        public DateTime CurrentTime { get; private set; }
        public NewSimulationEngine(NewSimulationContext context, NewSimulationScenario scenario)
        {
            _context = context;
            _scenario = scenario;
            _scheduler = new ManufactureScheduler(context.RecipeTanks, context.Manufactures, _context.BatchTransferCalculationModel); // <--- NUEVO

        }
        public void SetCalculationModel(TransferBatchToMixerCalculation BatchTransferCalculationModel)
        {
            _scheduler.SetCalculationMode(BatchTransferCalculationModel);
        }

        private void BuildExecutionHierarchy()
        {
            // Limpiamos y preparamos para el ordenamiento
            _executionOrder.Clear();
            var visited = new HashSet<Guid>();
            var queue = new Queue<NewPlantUnit>();

            // 1. Empezamos por las "Sinks" (las 13 líneas de envasado)
            var lines = _context.Lines;

            foreach (var line in lines) queue.Enqueue(line);

            //var mixers = _context.AllUnits.Values.OfType<NewPlantUnit>().ToList();

            //foreach (var mixer in mixers) mixer.SetOptimizationEngine(_scheduler.AiEngine);

            // 2. Navegamos hacia atrás por las conexiones de entrada (Inputs)
            while (queue.Count > 0)
            {
                var current = queue.Dequeue();

                if (visited.Contains(current.Id)) continue;

                visited.Add(current.Id);
                _executionOrder.Add(current);

                // Buscamos aguas arriba (Upstream)
                foreach (var inputUnit in current.Inputs)
                {
                    queue.Enqueue(inputUnit);
                }
            }
            CreateReporList();

        }

        public double SpeedFactor { get; set; } = 100.0;

        private CancellationTokenSource? _cts;

        // --- MANDOS DE CONTROL ---

        public void Pause() => Status = EngineStatus.Paused;

        public void Resume() => Status = EngineStatus.Running;

        public void Stop()
        {
            Status = EngineStatus.Stopped;
            _cts?.Cancel();
        }
        public Func<Task> UpdateModel { get; set; } = null!;

        public DateTime StartTime { get; set; }
        public NewSimulationSnapshot CurrentSnapshot { get; private set; } = null!;
        // Dentro de NewSimulationEngine.cs
        public async Task RunAsync(CancellationTokenSource ct)
        {
            _cts = ct;

            // 1. Preparación
            BuildExecutionHierarchy();

            // Validación de seguridad
            if (_executionOrder.Count == 0)
            {
                Status = EngineStatus.Stopped;
                return;
            }

            // Lista dinámica para sacar líneas terminadas
            var activeUnits = new List<NewPlantUnit>(_executionOrder);

            StartTime = _scenario.StartTime;
            CurrentTime = _scenario.StartTime;

            foreach (var unit in _executionOrder)
            {
                unit.CheckInitialStatus(CurrentTime);
            }

            Status = EngineStatus.Running;

            // ========================================================================
            // BUCLE PRINCIPAL (TICK BY TICK)
            // ========================================================================

#if DEBUG
            try
            {
#endif
                // Condición: Mientras haya líneas activas Y no se haya detenido manual
                while (activeUnits.OfType<NewLine>().Any() && Status != EngineStatus.Stopped)
                {
                    if (ct.IsCancellationRequested) break;

                    if (Status == EngineStatus.Paused)
                    {
                        await Task.Delay(100, ct.Token);
                        continue;
                    }

                    // 1. CÁLCULO (SCHEDULER)
                    _scheduler.Calculate();

                    // 2. CÁLCULO (EQUIPOS)
                    // Usamos copia .ToList() para poder borrar de la lista original
                    var unitsToProcess = activeUnits.ToList();

                    foreach (var unit in unitsToProcess)
                    {
                        unit.Calculate(CurrentTime);

                        // Lógica de salida: Si la línea terminó, adiós.
                        if (unit is NewLine line && line.IsWorkComplete)
                        {
                            activeUnits.Remove(line);
                        }
                    }

                    // 3. FOTO DEL ESTADO (SNAPSHOT)
                    var snapshot = CreateSnapshot(CurrentTime);
                    snapshot.Scheduler = _scheduler;
                    CurrentSnapshot = snapshot;

                    // 4. AVANCE DEL RELOJ
                    _context.TotalSimulationSpan += TimeSpan.FromSeconds(1);
                    CurrentTime = CurrentTime.AddSeconds(1);

                    // 5. CONTROL DE VELOCIDAD
                    if (SpeedFactor > 0 && SpeedFactor < 100)
                    {
                        int delayMs = (int)(1000 / SpeedFactor);
                        if (delayMs > 15) await Task.Delay(delayMs, ct.Token);
                    }

                    // 6. ACTUALIZACIÓN DE UI (TICK BY TICK)
                    // ============================================================
                    if (UpdateModel != null) await UpdateModel(); // <--- ¡AQUÍ ESTÁ! 
                                                                  // ============================================================
                                                                  // Se ejecuta en CADA vuelta del while, permitiendo ver la animación.
                }

#if DEBUG
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"🔴 ERROR CRÍTICO: {ex.Message}");
                System.Diagnostics.Debugger.Break(); // Parada forzosa en Debug
                Status = EngineStatus.Stopped;
                throw;
            }
#endif

            // CIERRE FINAL
            Status = EngineStatus.Stopped;

            // Última actualización para mostrar el estado "Detenido" en pantalla
            if (UpdateModel != null) await UpdateModel();
        }
        public async Task RunAsync2(CancellationTokenSource ct)
        {
            _cts = ct;

            // 1. Preparación
            BuildExecutionHierarchy();

            // Creamos una lista DINÁMICA de trabajo. 
            // Al principio contiene todo (Mixers, Tanques, Líneas).
            var activeUnits = new List<NewPlantUnit>(_executionOrder);

            StartTime = _scenario.StartTime;
            CurrentTime = _scenario.StartTime;

            // Inicializamos todos
            foreach (var unit in _executionOrder) unit.CheckInitialStatus(CurrentTime);

            Status = EngineStatus.Running;

            try
            {
                // CONDICIÓN DEL BUCLE:
                // Corremos mientras haya al menos UNA línea en la lista de activos.
                // (Quitamos el límite de _scenario.EndTime para que termine todo el trabajo)
                while (activeUnits.OfType<NewLine>().Any() && Status != EngineStatus.Stopped)
                {
                    if (ct.IsCancellationRequested) break;

                    if (Status == EngineStatus.Paused)
                    {
                        await Task.Delay(100, ct.Token); // Delay mayor en pausa para no quemar CPU
                        continue;
                    }

                    // --- CÁLCULO ---
                    _scheduler.Calculate();

                    // Iteramos sobre una COPIA (.ToList) para poder borrar elementos de la lista original mientras iteramos
                    foreach (var unit in activeUnits.ToList())
                    {
                        unit.Calculate(CurrentTime);

                        // --- LA LÓGICA DE EXCLUSIÓN ---
                        // Si es una Línea y dice que ya acabó...
                        if (unit is NewLine line && line.IsWorkComplete)
                        {
                            // 1. La sacamos del cálculo futuro
                            activeUnits.Remove(line);

                            // 2. (Opcional) Forzamos un estado visual de "Completado" o "Apagado" si quieres
                            // line.TransitionOutletState(new NewLineNotScheduled(line));
                        }
                    }

                    // --- ACTUALIZACIÓN DE ESTADO ---
                    var snapshot = CreateSnapshot(CurrentTime);
                    snapshot.Scheduler = _scheduler;
                    CurrentSnapshot = snapshot;

                    // --- CONTROL DE TIEMPO ---
                    _context.TotalSimulationSpan += TimeSpan.FromSeconds(1);
                    CurrentTime = CurrentTime.AddSeconds(1);

                    // --- CONTROL DE VELOCIDAD ---
                    if (SpeedFactor > 0 && SpeedFactor < 100)
                    {
                        int delayMs = (int)(1000 / SpeedFactor);
                        if (delayMs > 10) await Task.Delay(delayMs, ct.Token);
                    }

                    // Throttle visual para no ahogar la UI (Actualizar cada 500ms simulados o lo que prefieras)
                    // O déjalo libre si confías en Blazor.
                    await UpdateModel();
                }

                // FIN DEL JUEGO
                Status = EngineStatus.Stopped;

                // ¡IMPORTANTE! Última actualización a la UI para que sepa que paramos
                await UpdateModel();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Simulation Error: {ex.Message}");
            }
        }
        //public async Task RunAsync2(CancellationTokenSource ct)
        //{
        //    _cts = ct;

        //    // 1. Preparación
        //    BuildExecutionHierarchy();
        //    StartTime = _scenario.StartTime;
        //    CurrentTime = _scenario.StartTime;
        //    foreach (var unit in _executionOrder) unit.CheckInitialStatus(CurrentTime);

        //    Status = EngineStatus.Running;

        //    try
        //    {
        //        while (CurrentTime <= _scenario.EndTime && Status != EngineStatus.Stopped)
        //        {
        //            if (ct.IsCancellationRequested) break;

        //            // --- PAUSA ---
        //            if (Status == EngineStatus.Paused)
        //            {
        //                await Task.Delay(1, ct.Token);
        //                continue;
        //            }

        //            // --- 2. CÁLCULO (A máxima velocidad posible) ---

        //            // OPTIMIZACIÓN: ¿Realmente necesitas recalcular el Scheduler cada segundo?
        //            // Si el scheduler es pesado, puedes ejecutarlo solo cada minuto simulado:
        //            // if (CurrentTime.Second == 0) _scheduler.Execute(CurrentTime);
        //            _scheduler.Calculate();

        //            foreach (var unit in _executionOrder)
        //            {
        //                unit.Calculate(CurrentTime);
        //            }

        //            // --- 3. ACTUALIZACIÓN DE ESTADO (MEMORY ONLY) ---
        //            // Aquí está el truco: NO llamamos a progress.Report().
        //            // Solo actualizamos la propiedad pública. Es una operación de memoria instantánea (nanosegundos).
        //            // La UI leerá esta propiedad cada 50ms desde su propio Timer.

        //            var snapshot = CreateSnapshot(CurrentTime);
        //            snapshot.Scheduler = _scheduler;

        //            // Actualizamos la variable pública que lee la UI
        //            CurrentSnapshot = snapshot;

        //            // (Opcional) Si quieres reportar progreso solo para una barra de carga lenta,
        //            // hazlo con Throttle (ej: cada 100 ticks simulados).
        //            // if (CurrentTime.Second % 10 == 0 && progress != null) progress.Report(snapshot);


        //            // --- 4. CONTROL DE VELOCIDAD ---
        //            _context.TotalSimulationSpan += TimeSpan.FromSeconds(1);
        //            CurrentTime = CurrentTime.AddSeconds(1);

        //            // Solo frenamos si el usuario quiere ir más lento que la capacidad máxima del CPU
        //            if (SpeedFactor > 0 && SpeedFactor < 100) // Si es 100 (Max), no frenamos
        //            {
        //                // Lógica simple de delay
        //                // A velocidades altas, Task.Delay es impreciso.
        //                // Si SpeedFactor es alto, mejor no hacer Delay en cada vuelta, sino cada X vueltas.

        //                int delayMs = (int)(1000 / SpeedFactor);
        //                if (delayMs > 15) await Task.Delay(delayMs, ct.Token);
        //                // Si el delay es muy pequeño (<15ms), Windows no lo respeta bien. 
        //                // Mejor dejar correr libre o usar un SpinWait si quieres precisión extrema,
        //                // pero para Blazor WebAssembly, dejar correr libre es mejor.
        //            }
        //            await UpdateModel();
        //        }

        //        Status = EngineStatus.Stopped;
        //    }
        //    catch (Exception ex)
        //    {
        //        string exm = ex.Message;
        //        Console.WriteLine(exm);
        //    }
        //    // Ejecutamos en un hilo aparte para no bloquear nada

        //}

        private NewSimulationSnapshot CreateSnapshot(DateTime time)
        {
            var snapshot = new NewSimulationSnapshot { CurrentSimTime = time };

            //foreach (var unit in _executionOrder)
            //{
            //    // Aquí ocurre la magia: El Engine no sabe qué hay dentro, 
            //    // solo pide el reporte y el equipo entrega sus datos y estilos.
            //    snapshot.EquipmentData.Add(unit.Id, unit.Report);
            //}

            return snapshot;
        }


        // Actualizamos GetUnitsByGroup para pasar la UNIDAD completa, no solo el tipo

        void CreateReporList()
        {
            RawMaterialTanks = _executionOrder.OfType<NewRawMaterialInhouseTank>().ToList();
            var rawmtaerial = _executionOrder.OfType<NewRawMaterialTank>().ToList();
            RawMaterialTanks = RawMaterialTanks.Concat(rawmtaerial).OrderBy(x => x.Name);

            WipLinePairList = GetWipLinePairs();
        }
        public IEnumerable<NewProcessTank> RawMaterialTanks = new List<NewProcessTank>();



        public List<NewSkid> Skids => _executionOrder.OfType<NewSkid>().ToList();
        public List<NewMixer> Mixers => _executionOrder.OfType<NewMixer>()
            .Where(x => x.SupportedProducts.Any(x => x.Type == MaterialType.ProductBackBone)).OrderBy(x => x.Name).ToList();
        public List<NewWipTank> WipTanks => _executionOrder.OfType<NewWipTank>().ToList();
        public List<NewLine> Lines => _executionOrder.OfType<NewLine>().ToList();


        // Crea los pares para la columna dual (WIP frente a su Línea)
        public IEnumerable<WipLinePair> WipLinePairList = new List<WipLinePair>();
        private IEnumerable<WipLinePair> GetWipLinePairs()
        {
            // Aquí podrías tener una lógica de configuración o buscar por nombre/conexión
            // Por ahora, asumimos que existe una relación 1:1 en el orden
            var wips = _executionOrder.OfType<NewWipTank>().ToList();
            var lines = _executionOrder.OfType<NewLine>().OrderBy(x => x.Name).ToList();
            List<WipLinePair> result = new();

            foreach (var line in lines)
            {
                var wiptankss = line.Inputs.SelectMany(x => x.Inputs.OfType<NewWipTank>()).ToList();

                foreach (var wiptank in wiptankss)
                {
                    result.Add(new WipLinePair(wiptank.Id, line.Id));
                }
            }
            return result;

        }
        public enum EquipmentGroup
        {
            RawMaterial,
            Manufacturing, // Includes Mixers and Continuous Systems
            WIP,
            Packaging
        }
        public record WipLinePair(Guid WipId, Guid LineId);



    }


}

