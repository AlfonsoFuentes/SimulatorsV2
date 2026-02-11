using QWENShared.Enums;
using UnitSystem;

namespace GeminiSimulator.WashoutMatrixs
{
    public class NewSimulationScenario
    {
        public string Name { get; set; } = string.Empty;
        public DateTime StartTime { get; private set; }
        public TimeSpan Duration { get; private set; }
        public DateTime EndTime => StartTime.Add(Duration);

        // --- REGLAS GLOBALES DE PROCESO ---
        public bool OperatorHasNoRestrictionToInitBatch { get; private set; }
        public Amount MaxRestrictionTime { get; private set; }

        // --- CONSTRUCTOR ---
        public NewSimulationScenario(string name,
            DateTime startTime,
            TimeSpan duration,
            bool operatorNoRestriction,
            Amount maxRestrictionTime)
        {
            Name = name;
            StartTime = startTime;
            Duration = duration;
            OperatorHasNoRestrictionToInitBatch = operatorNoRestriction;
            MaxRestrictionTime = maxRestrictionTime;
        }

        // --- LÓGICA DE NEGOCIO: TURNOS (Extraída del DTO) ---

        /// <summary>
        /// Calcula el turno activo basado en una hora específica.
        /// Útil para saber en qué turno estamos a mitad de la simulación.
        /// </summary>
        public CurrentShift GetShiftAt(DateTime time)
        {
            return time.Hour switch
            {
                >= 6 and < 14 => CurrentShift.Shift_1,
                >= 14 and < 22 => CurrentShift.Shift_2,
                _ => CurrentShift.Shift_3 // 22:00 a 06:00
            };
        }

        /// <summary>
        /// El turno inicial de la simulación.
        /// </summary>
        public CurrentShift InitialShift => GetShiftAt(StartTime);

        /// <summary>
        /// Valida si un plan configurado para un turno específico (plannedShift) 
        /// es válido para ejecutarse en el turno actual (currentShift).
        /// </summary>
        public bool IsPlanCompatibleWithShift(ShiftType plannedShiftType, DateTime currentTime)
        {
            var current = GetShiftAt(currentTime);

            return current switch
            {
                CurrentShift.Shift_1 => plannedShiftType switch
                {
                    ShiftType.Shift_1_2_3 => true,
                    ShiftType.Shift_1_2 => true,
                    ShiftType.Shift_1_3 => true,
                    ShiftType.Shift_1 => true,
                    _ => false,
                },
                CurrentShift.Shift_2 => plannedShiftType switch
                {
                    ShiftType.Shift_1_2_3 => true,
                    ShiftType.Shift_1_2 => true,
                    ShiftType.Shift_2_3 => true,
                    ShiftType.Shift_2 => true,
                    _ => false,
                },
                CurrentShift.Shift_3 => plannedShiftType switch
                {
                    ShiftType.Shift_1_2_3 => true,
                    ShiftType.Shift_2_3 => true,
                    ShiftType.Shift_1_3 => true,
                    ShiftType.Shift_3 => true,
                    _ => false
                },
                _ => false
            };
        }
    }
}
