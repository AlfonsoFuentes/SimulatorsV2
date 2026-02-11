using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.Tanks;
using System;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    public class ManufactureScheduler
    {
        private readonly List<NewRecipedInletTank> _allTanks;
        private readonly List<NewManufacture> _allManufactures;
        private const double EpsilonTime = 0.001;

        public ManufactureScheduler(List<NewRecipedInletTank> tanks, List<NewManufacture> equipment)
        {
            _allTanks = tanks;
            _allManufactures = equipment;
        }
        public void Calculate()
        {
            // 1. Priorizamos los tanques más urgentes
            var orderedTanks = _allTanks
                .OrderBy(x => x.PendingTimeEmptyMassInProcess.GetValue(TimeUnits.Minute))
                .ToList();

            foreach (var tank in orderedTanks)
            {
                var currentMaterial = tank.CurrentMaterial;
                if (currentMaterial == null) continue;

                // 0. Filtro rápido
                if (tank.MassRemainingToOrder.GetValue(MassUnits.KiloGram) <= 0) continue;
                double timeToEmptyMin = tank.PendingTimeEmptyMassInProcess.GetValue(TimeUnits.Minute);
                if (timeToEmptyMin <= 0) continue;
                // Equipos capaces
                var capableEquipment = _allManufactures
                    .Where(x => x.SupportedProducts.Any(p => p.Id == currentMaterial.Id))
                    .ToList();

                if (!capableEquipment.Any()) continue;

                // --- LÓGICA SKID ---
                var skid = capableEquipment.OfType<NewSkid>().FirstOrDefault();
                if (skid != null)
                {
                    if (tank.CurrentLevel < tank.LoLevelControl && skid.OutletState is NewManufactureAvailableState)
                    {
                        skid.ReceiveStartCommand(tank);
                        // NOTA: Si el Skid es instantáneo o continuo, quizás no necesites AddMassInProcess aquí, 
                        // pero si tiene delay, deberías considerarlo. Por ahora lo dejamos como estaba.
                    }
                    continue;
                }

                // --- LÓGICA MIXER ---
                var validMixers = capableEquipment.OfType<NewMixer>();
              
                var bestCandidates = validMixers
                    .Select(m =>
                    {
                        string MixerName= m.Name;
                        double cap = m.ProductCapabilities[currentMaterial].GetValue(MassUnits.KiloGram);
                        double time = m.MixerWillbeFreeAt.GetValue(TimeUnits.Minute);
                        double score = cap / Math.Max(time, EpsilonTime);
                        return new { Mixer = m, Score = score, Capacity = cap };
                    })
                    .OrderByDescending(x => x.Score).ToList();

                var bestCandidateInfo = bestCandidates.FirstOrDefault();

                if (bestCandidateInfo != null)
                {
                    var mixer = bestCandidateInfo.Mixer;
                    double batchSize = mixer.ProductCapabilities[currentMaterial].GetValue(MassUnits.KiloGram);

                    // --- 1. VALIDACIÓN DE ESPACIO (Virtual) ---
                    // Usamos MassInProcess (Físico + Lo que ya viene en camino)
                    double currentLevelTotal = tank.MassInProcess.GetValue(MassUnits.KiloGram);
                    double capacity = tank.Capacity.GetValue(MassUnits.KiloGram);

                    // Espacio libre HOY considerando lo que ya pedí
                    double currentSpaceVirtual = capacity - currentLevelTotal;
                    tank.BestCandidate = null!;
                    // Si el resultado es negativo, significa que ya pedí más de lo que me cabe.
                    // Frenamos para no rebasar.
                    if (currentSpaceVirtual < 0) continue;

                    // --- 2. CÁLCULO DE TIEMPOS (Lead Time) ---

                    // A. Espera
                    double timeToFree = mixer.MixerWillbeFreeAt.GetValue(TimeUnits.Second);

                    // B. Lavado
                    double timeWash = 0;
                    var lastMaterial = mixer.BatchManager.LastProduct;
                    if (mixer.NeedsWashing(lastMaterial, currentMaterial))
                    {
                        timeWash = mixer.WashoutRules
                           .GetMixerWashout(lastMaterial!.Category, currentMaterial.Category)
                           .GetValue(TimeUnits.Second);
                    }

                    // C. Proceso (Seguro)
                    double timeProcess = 3600;
                    if (mixer.TheoricalBatchTime.TryGetValue(currentMaterial, out var tProcess))
                    {
                        timeProcess = tProcess.GetValue(TimeUnits.Second);
                    }

                    // D. Transferencia
                    double timeTransfer = 0;
                    if (mixer._DischargeRate > 0)
                    {
                        timeTransfer = batchSize / mixer._DischargeRate;
                    }

                    double totalLeadTimeSeconds = timeToFree + timeWash + timeProcess + timeTransfer;

                    // --- 3. PROYECCIÓN DE CONSUMO ---

                    double consumptionPerSec = tank.AverageOutletFlow.GetValue(MassFlowUnits.Kg_sg);

                    // ¿Cuánto espacio EXTRA se va a liberar mientras espero el batch?
                    double projectedConsumption = consumptionPerSec * totalLeadTimeSeconds;

                    // Espacio Total al momento de llegada = Espacio Virtual Hoy + Espacio Liberado por Consumo
                    double projectedSpaceAtArrival = currentSpaceVirtual + projectedConsumption;

                    // REGLA 1: ¿Cabe el batch?
                    // Si el espacio que tendré cuando llegue es menor al batch (con 5% de margen), espero.
                    if (projectedSpaceAtArrival < (batchSize * 0.95))
                    {
                        continue;
                    }

                    // --- 4. DECISIÓN JIT (SLACK TIME) ---

                    // Tiempo de vida con lo que tengo + lo que viene (MassInProcess)
                    

                    // Tiempo que tarda en llegar el rescate
                    double estimatedBatchTimeMin = totalLeadTimeSeconds / 60.0;

                    // Holgura (Slack)
                    double slack = timeToEmptyMin - estimatedBatchTimeMin;

                    // REGLA 2: ¿La ambulancia tarda más que mi tiempo de vida?
                    bool IsBatchNeeded = slack <= 0; // O un pequeño umbral positivo (ej. <= 5 min)
                    tank.BestCandidate = mixer;
                    if (IsBatchNeeded)
                    {
                        // 1. Ejecutamos la orden física
                        mixer.ReceiveStartCommand(tank);

                        // 2. ACTUALIZAMOS LA CONTABILIDAD (CRÍTICO)
                        // Como el Mixer no avisa, el Scheduler lo hace aquí.
                        // Esto incrementa MassInProcess inmediatamente para el siguiente loop.
                        tank.AddMassInProcess(mixer,batchSize);
                    }
                }
            }
        }
        
        
    }
}
