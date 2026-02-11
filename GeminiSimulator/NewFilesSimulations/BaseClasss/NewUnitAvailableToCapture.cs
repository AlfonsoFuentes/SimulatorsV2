using UnitSystem;

using System;

namespace GeminiSimulator.NewFilesSimulations.BaseClasss
{
    // --- INTERFAZ ---
    public interface ICaptureState : ICalculationState
    {
        // (Hereda Calculate, IsProductive, StateLabel, HexColor)
    }

    // --- 1. ESTADO DISPONIBLE (VERDE) ---
    public class NewUnitAvailableToCapture : ICaptureState
    {
        private readonly NewPlantUnit _unit;

        // MEJORA VISUAL: Si hay gente en cola pero el equipo está "Available", 
        // muestra "(Queue: X)" para alertar que pronto cambiará.
        public string StateLabel => _unit.PendingRequestsCount > 0
            ? $"Available (Queue: {_unit.PendingRequestsCount})"
            : "Available";

        public string HexColor => "#00C853"; // Verde
        public NewUnitAvailableToCapture(NewPlantUnit unit) => _unit = unit;
        public bool IsProductive => true; // Está listo para producir

        public void Calculate()
        {
            // Pasivo. La lógica de asignación ocurre en NewPlantUnit.ProcessNextInQueue
        }

        public void CheckTransitions()
        {
            // Pasivo.
        }
    }

    // --- 2. ESTADO CAPTURADO (AZUL) ---
    public class NewUnitCapturedBy : ICaptureState
    {
        private readonly NewPlantUnit _unit;

        // MEJORA VISUAL: Muestra quién lo tiene Y cuántos esperan detrás.
        public string StateLabel
        {
            get
            {
                string ownerName = _unit.CurrentOwner?.Name ?? "Unknown";
                string queueInfo = _unit.PendingRequestsCount > 0
                    ? $" (+{_unit.PendingRequestsCount} waiting)"
                    : "";
                return $"Used by: {ownerName}{queueInfo}";
            }
        }

        public string HexColor => "#2196F3"; // Azul
        public NewUnitCapturedBy(NewPlantUnit unit) => _unit = unit;
        public bool IsProductive => false; // Está ocupado (no disponible para otros)

        public void Calculate()
        {
            // El trabajo real lo suele calcular el Dueño (Mixer), 
            // no el recurso capturado (Bomba).
        }

        public void CheckTransitions()
        {
            // La transición de salida la dicta el Dueño con ReleaseAccess().
        }
    }

    // --- 3. TAREA TEMPORIZADA (NARANJA - Setup/Limpieza) ---
    // Este estado es especial: El recurso trabaja SOLO (ej. un Operario preparándose),
    // pero está "atado" a un Mixer que lo solicitó.
    public class NewUnitTimedTaskState : ICaptureState
    {
        private readonly NewPlantUnit _operator; // El recurso (Operario)
        private readonly NewPlantUnit _owner;    // El jefe (Mixer)
        private double _timeRemaining;

        public NewUnitTimedTaskState(NewPlantUnit op, NewPlantUnit owner, Amount duration)
        {
            _operator = op;
            _owner = owner;
            _timeRemaining = duration.GetValue(TimeUnits.Second);

            // 1. INICIALIZAR LA PREDICCIÓN
            // "Estaré libre exactamente cuando termine este setup"
            // Esto permite que el siguiente Mixer en la fila sepa cuándo acabará el Setup.
            UpdateForecast();
        }

        public bool IsProductive => true; // Está haciendo algo útil (Setup)
        public string StateLabel => $"Setup: {_timeRemaining:F0}s";
        public string HexColor => "#FF9800"; // Naranja

        public void Calculate()
        {
            // Descuenta tiempo
            _timeRemaining -= 1; // 1 tick = 1 segundo

            // 2. ACTUALIZAR LA PREDICCIÓN (Opcional pero recomendado)
            // Si la simulación sufre retrasos, mantenemos el AvailableAt actualizado 
            // para que GetForecastedAvailability sea preciso segundo a segundo.
            if (_timeRemaining % 10 == 0) // Actualizar cada 10s para no saturar
            {
                UpdateForecast();
            }
        }

        private void UpdateForecast()
        {
            _operator.AvailableAt = _operator.CurrentDate.AddSeconds(_timeRemaining);
        }

        public void CheckTransitions()
        {
            if (_timeRemaining <= 0)
            {
                // 3. AUTO-LIBERACIÓN
                // El operario terminó su setup y se libera del Mixer.
                // Gracias a tu arreglo en NewPlantUnit, esto llamará a:
                // CancelReservation(_owner) -> ProcessNextInQueue()
                _operator.ReleaseAccess(_owner);

                // Nota: Al liberarse, NewPlantUnit cambiará automáticamente 
                // el CaptureState a 'NewUnitAvailableToCapture' o 'CapturedBy' (si hay otro).
            }
        }
    }
}


