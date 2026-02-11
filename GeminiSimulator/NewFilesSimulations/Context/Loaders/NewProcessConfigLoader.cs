using GeminiSimulator.DesignPatterns;
using GeminiSimulator.Helpers;
using GeminiSimulator.NewFilesSimulations.Context;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using GeminiSimulator.Plans;
using GeminiSimulator.PlantUnits.Lines;
using GeminiSimulator.SKUs;
using QWENShared.DTOS.LinePlanneds;
using QWENShared.DTOS.SimulationPlanneds;
using UnitSystem;

namespace GeminiSimulator.WashoutMatrixs
{
    public class NewProcessConfigLoader : INewPlanLoader
    {
        private readonly NewSimulationContext _context;

        // NIVEL 5: Prioridad Alta en la Fase 2
        public int ExecutionOrder => 5;

        public NewProcessConfigLoader(NewSimulationContext context)
        {
            _context = context;
        }

        public void Load(SimulationPlannedDTO planData)
        {
            Console.WriteLine($"--- [Plan Loader] Procesando Plan: {planData.Name} ---");

            // =========================================================
            // PASO 1: CONFIGURACIÓN DEL ESCENARIO (GLOBAL)
            // =========================================================
            LoadScenario(planData);

            // =========================================================
            // PASO 2: CARGA DE PLANES POR LÍNEA (DETALLE)
            // =========================================================
            LoadLinePlans(planData.PlannedLines);

            // (Opcional) Aquí podrías llamar a LoadMixerPlans(planData.PlannedMixers) si lo requieres
        }

        private void LoadScenario(SimulationPlannedDTO dto)
        {
            Console.WriteLine(" -> Configurando Tiempos y Reglas...");

            DateTime start = dto.InitDate ?? DateTime.Now;
            TimeSpan duration = TimeSpan.FromHours(dto.Hours);

            // Validar Amount para evitar nulos
            Amount maxRestriction = dto.MaxRestrictionTime ?? new Amount(0, TimeUnits.Minute);
            if (dto.MaxRestrictionTime == null && dto.MaxRestrictionTimeValue > 0)
            {
                maxRestriction = new Amount(dto.MaxRestrictionTimeValue, TimeUnits.Minute);
            }

            var scenario = new NewSimulationScenario(
                dto.Name,
                start,
                duration,
                dto.OperatorHasNotRestrictionToInitBatch,
                maxRestriction
            );

            _context.Scenario = scenario;

            Console.WriteLine($"    |__ Inicio: {start:g} | Duración: {dto.Hours}h | Turno: {scenario.InitialShift}");
        }

        // Método LoadLinePlans dentro de ProcessConfigLoader.cs

        private void LoadLinePlans(List<LinePlannedDTO> linePlans)
        {
            if (linePlans == null) return;

            foreach (var lineDto in linePlans)
            {
                var unit = _context.GetUnit(lineDto.LineId);

                if (unit is NewLine line)
                {
                    var domainOrders = new List<ProductionOrder>();

                    if (lineDto.PlannedSKUDTOs != null)
                    {
                        // Asumimos que el orden en la lista es el orden de producción


                        foreach (var dto in lineDto.PlannedSKUDTOs)
                        {
                            // 1. OBTENER DATOS DE REFERENCIA FALTANTES
                            // El DTO trae Velocidad y Pesos, pero no veo 'Category' ni 'Volume' en tu clase PlannedSKUDTO.
                            // Los buscamos en el contexto usando el SKUId.

                            double speedPerMinute = dto.LineSpeed.GetValue(LineVelocityUnits.EA_min);
                            double plannedAu = 0;

                            if (speedPerMinute > 0)
                            {
                                // Numerador: Cajas x Unidades/Caja (Producción total planeada)
                                double totalPlannedUnits = dto.Case_Shift * dto.EA_Case;

                                // Denominador: Velocidad x 8 horas x 60 min (Capacidad teórica)
                                double theoreticalCapacity = speedPerMinute * 8 * 60;

                                plannedAu = Math.Round((totalPlannedUnits / theoreticalCapacity) * 100, 2);
                            }

                            SkuDefinition? sku = null;
                            if (_context.Skus.TryGetValue(dto.SKUId, out var staticSku))
                            {

                                sku = staticSku;

                            }
                            if (sku != null)
                            {
                                var order = new ProductionOrder(sku,
                              dto.Order, // O dto.Order si existe en la clase base Dto
                               plannedAu,
                              dto.PlannedCases,
                               dto.LineSpeed,                                // Amount -> Amount
                             dto.TimeToChangeSKU
                           );

                                domainOrders.Add(order);
                            }
                            // 2. CONSTRUIR ORDEN USANDO EL DTO (Respetando tus datos)

                        }
                    }

                    // 3. Preferencias de Mixers
                    var prefs = lineDto.PreferedMixerDTOs?.Select(x => x.MixerId).ToList() ?? new List<Guid>();

                    // 4. Asignar
                    line.AssignProductionPlan(domainOrders, prefs, lineDto.ShiftType);
                }
            }
        }
    }
}
