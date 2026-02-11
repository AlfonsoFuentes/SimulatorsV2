using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.BaseClasss
{
    // --- GLOBAL STATES ---

    //public class NewGlobalAvailableState : IGlobalStated
    //{
    //    private readonly NewPlantUnit _unit;
    //    public NewGlobalAvailableState(NewPlantUnit unit) => _unit = unit;

    //    public string StateLabel => "Available";
    //    public string HexColor => "#00C853";
    //    public bool IsOperational => true;

    //    public void CheckTransitions()
    //    {
    //        // 1. DETECCIÓN DE PARADA PROGRAMADA
    //        if (_unit.IsOnPlannedBreak(_unit.CurrentDate))
    //        {
    //            // A. Cambiamos nuestro estado a Downtime
    //            _unit.TransitionGlobalState(new NewGlobalPlannedDowntimeState(_unit));

    //            // B. AVISAR AL DUEÑO (Si existe, ej: Skid)
    //            if (_unit.CurrentOwner != null)
    //            {
    //                // Al dueño sí le avisamos siempre (sea yo bomba o tanque)
    //                _unit.CurrentOwner.TransitionGlobalState(new NewGlobalStarvedByOwner(_unit.CurrentOwner));
    //            }

    //            // C. AVISAR A LAS BOMBAS DE SALIDA (Solo si soy Tanque) <--- TU CORRECCIÓN
    //            if (_unit is Tanks.NewProcessTank)
    //            {
    //                foreach (var output in _unit.Outputs.OfType<Operators.NewPump>())
    //                {
    //                    output.TransitionGlobalState(
    //                        new NewGlobalExternalStarvedByOwner(output, "Inlet Downtime")
    //                    );
    //                }
    //            }

    //            return;
    //        }

    //        // 2. DETECCIÓN DE STARVATION POR DUEÑO
    //        if (_unit.CurrentOwner != null)
    //        {
    //            if (!_unit.CurrentOwner.GlobalState.IsOperational)
    //            {
    //                _unit.TransitionGlobalState(new NewGlobalStarvedByOwner(_unit));
    //            }
    //        }
    //    }
    //}
    //public class NewGlobalPlannedDowntimeState : IGlobalStated
    //{
    //    private readonly NewPlantUnit _unit;
    //    public NewGlobalPlannedDowntimeState(NewPlantUnit unit) => _unit = unit;

    //    public string StateLabel => "Planned Downtime";
    //    public string HexColor => "#F44336";
    //    public bool IsOperational => false;

    //    public void CheckTransitions()
    //    {
    //        // 1. ¿YA TERMINÓ EL DESCANSO?
    //        if (!_unit.IsOnPlannedBreak(_unit.CurrentDate))
    //        {
    //            // A. El Tanque mismo vuelve a estar Disponible (Globalmente)
    //            _unit.TransitionGlobalState(new NewGlobalAvailableState(_unit));

    //            // B. Despertar al Dueño (Skid/Mixer) si existe y no está en descanso
    //            if (_unit.CurrentOwner != null && !_unit.CurrentOwner.IsOnPlannedBreak(_unit.CurrentDate))
    //            {
    //                _unit.CurrentOwner.TransitionGlobalState(new NewGlobalAvailableState(_unit.CurrentOwner));
    //            }

    //            // C. GESTIÓN DE LAS BOMBAS (Aquí está la magia para tu caso)
    //            if (_unit is Tanks.NewProcessTank tank)
    //            {
    //                // VERIFICACIÓN DE SALUD: ¿Sigo vacío?
    //                bool isStillLowLevel = tank.CurrentLevel.GetValue(MassUnits.KiloGram) < tank.LoLevelAlarm.GetValue(MassUnits.KiloGram);

    //                foreach (var output in _unit.Outputs.OfType<Operators.NewPump>())
    //                {
    //                    if (isStillLowLevel)
    //                    {
    //                        // CASO 1: Terminó el descanso, pero sigo SIN NIVEL.
    //                        // NO libero a la bomba. Le cambio la etiqueta para que sepa por qué sigue parada.
    //                        // Pasamos de "Starved by Downtime" a "Starved by Low Level".
    //                        output.TransitionGlobalState(
    //                            new NewGlobalExternalStarvedByOwner(output, "Low Level Alarm") // Etiqueta correcta del estado de nivel bajo
    //                        );
    //                    }
    //                    else
    //                    {
    //                        // CASO 2: Terminó el descanso y tengo nivel.
    //                        // Libero a la bomba para que trabaje.
    //                        output.TransitionGlobalState(new NewGlobalAvailableState(output));
    //                    }
    //                }
    //            }
    //        }
    //    }
    //}
    //public class NewGlobalStarvedByOwner : IGlobalStated // Nombre corregido
    //{
    //    private readonly NewPlantUnit _unit;


    //    public NewGlobalStarvedByOwner(NewPlantUnit unit)
    //    {
    //        _unit = unit;

    //    }

    //    public string StateLabel => $"{_unit.Name} Starved by: {_unit.CurrentOwner?.Name ?? string.Empty}";
    //    public string HexColor => "#FF9800";
    //    public bool IsOperational => false;

    //    public bool IsProductive => false;



    //    public void CheckTransitions()
    //    {
    //        if (_unit.IsOnPlannedBreak(_unit.CurrentDate))
    //        {
    //            _unit.TransitionGlobalState(new NewGlobalPlannedDowntimeState(_unit));
    //            return;
    //        }
    //        bool ownerIsReady = _unit.CurrentOwner == null || _unit.CurrentOwner.GlobalState.IsOperational;
    //        if (ownerIsReady)
    //        {
    //            _unit.TransitionGlobalState(new NewGlobalAvailableState(_unit));
    //        }
    //    }
    //}
    //public class NewGlobalExternalStarvedByOwner : IGlobalStated
    //{
    //    private readonly NewPlantUnit _unit;
    //    private readonly string _externalStarvedLabel;

    //    public NewGlobalExternalStarvedByOwner(NewPlantUnit unit, string externalStarvedLabel)
    //    {
    //        _unit = unit;
    //        _externalStarvedLabel = externalStarvedLabel;
    //    }

    //    public string StateLabel => $"{_unit.Name} Starved by: {_externalStarvedLabel}";
    //    public string HexColor => "#FF9800";
    //    public bool IsOperational => false;
    //    public bool IsProductive => false;

    //    public void CheckTransitions()
    //    {

    //    }
    //}
    //public class NewGlobalStarvedByResourceState : IGlobalStated
    //{
    //    private readonly NewPlantUnit _owner; // El Mixer
    //    public NewPlantUnit CulpritResource { get; } // La Bomba
    //    public string StateLabel => $"Starved by Resource: {CulpritResource.Name}";
    //    public string HexColor => "#FF9800"; // Naranja

    //    public bool IsOperational => false;   // Apaga el cerebro del equipo
    //    public NewGlobalStarvedByResourceState(NewPlantUnit owner, NewPlantUnit culprit)
    //    {
    //        _owner = owner;
    //        CulpritResource = culprit;
    //    }

    //    // ... Properties ...

    //    public void CheckTransitions()
    //    {
    //        // CORRECCIÓN CRÍTICA:
    //        // Si el recurso culpable (La Bomba) ya está operativo de nuevo...
    //        if (CulpritResource.GlobalState.IsOperational)
    //        {
    //            // ...¡Yo también revivo!
    //            _owner.TransitionGlobalState(new NewGlobalAvailableState(_owner));
    //        }
    //    }
    //}
    //public class NewGlobalStarvedByCapturedResourceState : IGlobalStated
    //{
    //    private readonly NewPlantUnit _owner; // El Mixer
    //    public NewPlantUnit CulpritResource { get; } // La Bomba
    //    public string StateLabel => $"Starved by Resource: {CulpritResource.Name}";
    //    public string HexColor => "#FF9800"; // Naranja

    //    public bool IsOperational => false;   // Apaga el cerebro del equipo
    //    public NewGlobalStarvedByCapturedResourceState(NewPlantUnit owner, NewPlantUnit culprit)
    //    {
    //        _owner = owner;
    //        CulpritResource = culprit;
    //    }

    //    // ... Properties ...

    //    public void CheckTransitions()
    //    {
    //        if (CulpritResource.CaptureState is NewUnitCapturedBy && CulpritResource.CurrentOwner == _owner)
    //        {
    //            _owner.TransitionGlobalState(new NewGlobalAvailableState(_owner));
    //            return;
    //        }
    //        if (CulpritResource.CaptureState is NewUnitAvailableToCapture)
    //        {
    //            // ...¡Yo también revivo!
    //            _owner.TransitionGlobalState(new NewGlobalAvailableState(_owner));
    //        }
    //    }
    //}

}


