using GeminiSimulator.Helpers;
using GeminiSimulator.Main;
using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.Plans;
using GeminiSimulator.PlantUnits;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.PlantUnits.ManufacturingEquipments;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.PlantUnits.Tanks;
using GeminiSimulator.WashoutMatrixs;
using QWENShared.Enums;

namespace GeminiSimulator.NewFilesSimulations.Context
{
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
            _scheduler = new ManufactureScheduler(context.RecipeTanks, context.Manufactures); // <--- NUEVO

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

        public NewSimulationSnapshot CurrentSnapshot { get; private set; } = null!;
        public async Task RunAsync(CancellationTokenSource ct)
        {
            _cts = ct;

            // 1. Preparación
            BuildExecutionHierarchy();
            CurrentTime = _scenario.StartTime;
            foreach (var unit in _executionOrder) unit.CheckInitialStatus(CurrentTime);

            Status = EngineStatus.Running;

            try
            {
                while (CurrentTime <= _scenario.EndTime && Status != EngineStatus.Stopped)
                {
                    if (ct.IsCancellationRequested) break;

                    // --- PAUSA ---
                    if (Status == EngineStatus.Paused)
                    {
                        await Task.Delay(1, ct.Token);
                        continue;
                    }

                    // --- 2. CÁLCULO (A máxima velocidad posible) ---

                    // OPTIMIZACIÓN: ¿Realmente necesitas recalcular el Scheduler cada segundo?
                    // Si el scheduler es pesado, puedes ejecutarlo solo cada minuto simulado:
                    // if (CurrentTime.Second == 0) _scheduler.Execute(CurrentTime);
                    _scheduler.Calculate();

                    foreach (var unit in _executionOrder)
                    {
                        unit.Calculate(CurrentTime);
                    }

                    // --- 3. ACTUALIZACIÓN DE ESTADO (MEMORY ONLY) ---
                    // Aquí está el truco: NO llamamos a progress.Report().
                    // Solo actualizamos la propiedad pública. Es una operación de memoria instantánea (nanosegundos).
                    // La UI leerá esta propiedad cada 50ms desde su propio Timer.

                    var snapshot = CreateSnapshot(CurrentTime);
                    snapshot.Scheduler = _scheduler;

                    // Actualizamos la variable pública que lee la UI
                    CurrentSnapshot = snapshot;

                    // (Opcional) Si quieres reportar progreso solo para una barra de carga lenta,
                    // hazlo con Throttle (ej: cada 100 ticks simulados).
                    // if (CurrentTime.Second % 10 == 0 && progress != null) progress.Report(snapshot);


                    // --- 4. CONTROL DE VELOCIDAD ---
                    _context.TotalSimulationSpan += TimeSpan.FromSeconds(1);
                    CurrentTime = CurrentTime.AddSeconds(1);

                    // Solo frenamos si el usuario quiere ir más lento que la capacidad máxima del CPU
                    if (SpeedFactor > 0 && SpeedFactor < 100) // Si es 100 (Max), no frenamos
                    {
                        // Lógica simple de delay
                        // A velocidades altas, Task.Delay es impreciso.
                        // Si SpeedFactor es alto, mejor no hacer Delay en cada vuelta, sino cada X vueltas.

                        int delayMs = (int)(1000 / SpeedFactor);
                        if (delayMs > 15) await Task.Delay(delayMs, ct.Token);
                        // Si el delay es muy pequeño (<15ms), Windows no lo respeta bien. 
                        // Mejor dejar correr libre o usar un SpinWait si quieres precisión extrema,
                        // pero para Blazor WebAssembly, dejar correr libre es mejor.
                    }
                    await UpdateModel();
                }

                Status = EngineStatus.Stopped;
            }
            catch (Exception ex)
            {
                string exm = ex.Message;
            }
            // Ejecutamos en un hilo aparte para no bloquear nada

        }

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
        // REPORTE DE MIXERS: Suma historia + paso activo en tiempo real
        public List<AssetStarvedReport> MixerEfficiencyRanking =>
            Mixers.Select(m =>
            {
                double totalStarved = m.BatchManager.ExecutionHistory.Sum(s => (double)s.AccumulatedStarvation);
                double totalOp = m.BatchManager.ExecutionHistory.Sum(s => (double)s.DurationSeconds);

                // El "Truco" para el tiempo real: sumar lo que está pasando ahora
                if (m.BatchManager.CurrentStep != null)
                {
                    totalStarved += m.BatchManager.CurrentStep.AccumulatedStarvation;
                    totalOp += m.BatchManager.CurrentStep.DurationSeconds;
                }

                return new AssetStarvedReport
                {
                    Name = m.Name,
                    StarvedMinutes = totalStarved / 60.0,
                    OperationalMinutes = totalOp / 60.0
                };
            }).OrderByDescending(r => r.StarvedMinutes).ToList();

        // MATRIZ DE FRICCIÓN: Arregla el error de m.Name y suma el paso actual
        public List<FrictionReport> FrictionMatrix =>
            Mixers.SelectMany(m =>
            {
                var allSteps = m.BatchManager.ExecutionHistory.ToList();
                if (m.BatchManager.CurrentStep != null) allSteps.Add(m.BatchManager.CurrentStep);

                // Aquí arreglamos el error de la imagen image_55816d.png
                return allSteps.Select(s => new { MixerName = m.Name, Step = s });
            })
            .Where(x => x.Step.AccumulatedStarvation > 0 && x.Step.ResourceName != "N/A")
            .GroupBy(x => new { x.MixerName, x.Step.ResourceName })
            .Select(g => new FrictionReport
            {
                MixerName = g.Key.MixerName,
                ResourceName = g.Key.ResourceName,
                LostMinutes = g.Sum(x => (double)x.Step.AccumulatedStarvation) / 60.0,
                StopCount = g.Count()
            })
            .OrderByDescending(r => r.LostMinutes)
            .ToList();

        public List<AssetStarvedReport> PumpBottleneckRanking =>
     Mixers.SelectMany(m =>
     {
         // Unificamos el historial y el paso actual de cada Mixer
         var allSteps = m.BatchManager.ExecutionHistory.ToList();
         if (m.BatchManager.CurrentStep != null)
         {
             allSteps.Add(m.BatchManager.CurrentStep);
         }
         return allSteps;
     })
     .Where(s => s.ResourceName != "N/A") // Filtramos recursos válidos
     .GroupBy(s => s.ResourceName)
     .Select(g => new AssetStarvedReport
     {
         Name = g.Key,
         // Usamos (double) para evitar que los segundos se pierdan en el redondeo
         StarvedMinutes = g.Sum(s => (double)s.AccumulatedStarvation) / 60.0,
         OperationalMinutes = g.Sum(s => (double)s.DurationSeconds) / 60.0
     })
     .OrderByDescending(r => r.StarvedMinutes)
     .ToList();

        public List<StopReasonReport> DowntimeReasons =>
           Mixers.SelectMany(m =>
           {
               // Creamos una lista temporal que une el pasado y el presente de este mixer
               var allSteps = m.BatchManager.ExecutionHistory.ToList();
               if (m.BatchManager.CurrentStep != null)
               {
                   allSteps.Add(m.BatchManager.CurrentStep);
               }
               return allSteps;
           })
           .GroupBy(s => s.GetType().Name) // Agrupamos por tipo de paso (StepMass, StepWashing, etc.)
           .Select(g => new StopReasonReport
           {
               // "StepMass" se convierte en "Mass", "StepWashing" en "Washing"
               Reason = g.Key.Replace("Step", ""),
               // Aplicamos el cast a (double) para evitar pérdida de precisión
               TotalMinutes = g.Sum(s => (double)s.AccumulatedStarvation) / 60.0
           })
           .OrderByDescending(r => r.TotalMinutes)
           .ToList();
    }

    }

