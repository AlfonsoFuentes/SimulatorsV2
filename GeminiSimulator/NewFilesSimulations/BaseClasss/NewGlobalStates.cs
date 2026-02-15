using GeminiSimulator.NewFilesSimulations.Tanks;

namespace GeminiSimulator.NewFilesSimulations.BaseClasss
{
    public interface IGlobalStated : IState
    {
        bool IsOperational { get; }
    }

    // --- 1. OPERACIONAL (VERDE) ---
    public class GlobalState_Operational : IGlobalStated
    {
        private readonly NewPlantUnit _unit;
        public GlobalState_Operational(NewPlantUnit unit) => _unit = unit;

        public string StateLabel => "Operational";
        public string HexColor => "#00C853"; // Verde
        public bool IsOperational => true;

        public void CheckTransitions()
        { // --- REGLA A (Corregida) ---
            if (_unit.IsOnPlannedBreak(_unit.CurrentDate))
            {
                _unit.TransitionGlobalState(new GlobalState_Maintenance(_unit));
                if (_unit is NewProcessTank tank)
                {
                    foreach (var slave in _unit.Outputs.OfType<NewPlantUnit>())
                    {
                        if (slave.Name.Contains("G"))
                        {

                        }
                        // Solo bloqueamos si el esclavo está intentando trabajar
                        if (slave.GlobalState.IsOperational)
                        {
                            slave.TransitionGlobalState(new GlobalState_SlaveBlocked(slave, _unit, $"{_unit.Name} Planned downtime"));
                        }
                    }
                }
                else if (_unit.CurrentOwner != null)
                {
                    if (_unit.CurrentOwner.GlobalState.IsOperational)
                    {
                        _unit.CurrentOwner.TransitionGlobalState(new GlobalState_SlaveBlocked(_unit.CurrentOwner, _unit, $"{_unit.Name} Planned downtime"));
                    }
                }
                
                return;
            }

       
        }
    }

    // --- 2. MANTENIMIENTO (ROJO) ---
    public class GlobalState_Maintenance : IGlobalStated
    {
        private readonly NewPlantUnit _unit;
        public GlobalState_Maintenance(NewPlantUnit unit)
        {
            _unit = unit;
        }

        public string StateLabel => "Planned Downtime";
        public string HexColor => "#F44336"; // Rojo
        public bool IsOperational => false;

        public void CheckTransitions()
        {
            // ¿Ya terminó el recreo?
            if (!_unit.IsOnPlannedBreak(_unit.CurrentDate))
            {
                // 1. Me despierto YO MISMO
                _unit.TransitionGlobalState(new GlobalState_Operational(_unit));

                // 2. CORRECCIÓN: NO despertamos al Dueño (Mixer) manualmente.
                // El Mixer tiene su propio cerebro (MasterWaiting) que se dará cuenta 
                // de que ya estamos verdes y se despertará solo. ¡Es más seguro!

                // 3. Despierto a mis Esclavos (Si soy un Tanque y tengo bombas abajo)
                foreach (var slave in _unit.Outputs.OfType<NewPlantUnit>())
                {
                    if (slave.Name.Contains("G"))
                    {

                    }
                    slave.TryReleaseFromBlock(_unit);
                    if (slave.CurrentOwner != null && !slave.CurrentOwner.GlobalState.IsOperational)
                    {
                        slave.CurrentOwner.TryReleaseFromBlock(slave);
                    }

                }
            }

        }
    }

    // --- 3. BLOQUEADO POR OTRO (NARANJA OSCURO) ---
    public class GlobalState_SlaveBlocked : IGlobalStated
    {
        private readonly NewPlantUnit _me;
        private readonly NewPlantUnit _blocker; // <--- Referencia al "Culpable"
        private readonly string _reason;

        public GlobalState_SlaveBlocked(NewPlantUnit me, NewPlantUnit blocker, string reason)
        {
            _me = me;
            _blocker = blocker;
            _reason = reason;
        }

        public string StateLabel => $"Blocked by: {_blocker.Name} ({_reason})";
        public string HexColor => "#EF6C00";
        public bool IsOperational => false;

        // Propiedad para que otros puedan consultar quién tiene la llave
        public NewPlantUnit Blocker => _blocker;

        public void CheckTransitions()
        {
            // Solo vigila su propia salud (Mantenimiento)
            //if (_me.IsOnPlannedBreak(_me.CurrentDate))
            //{
            //    _me.TransitionGlobalState(new GlobalState_Maintenance(_me));
            //}
        }
    }
    //public class GlobalState_SlaveBlocked : IGlobalStated
    //{
    //    private readonly NewPlantUnit _me;
    //    private readonly string _reason;
    //    private readonly NewPlantUnit _blocker; // <--- Referencia al "Culpable"
    //    public GlobalState_SlaveBlocked(NewPlantUnit me, string reason = "")
    //    {
    //        if (me.Name.Contains("G"))
    //        {

    //        }
    //        _me = me;
    //        // Si no dan razón, asumimos que es culpa del dueño actual
    //        _reason = string.IsNullOrEmpty(reason)
    //                  ? (_me.CurrentOwner?.Name ?? "Upstream Block")
    //                  : reason;
    //    }

    //    public string StateLabel => $"Blocked by: {_reason}";
    //    public string HexColor => "#EF6C00"; // Naranja Oscuro
    //    public bool IsOperational => false;

    //    public void CheckTransitions()
    //    {
    //        if (_me.CurrentOwner != null && _me.CurrentOwner.GlobalState.IsOperational)
    //        {
    //            if (_me.IsOnPlannedBreak(_me.CurrentDate))
    //            {
    //                _me.TransitionGlobalState(new GlobalState_Maintenance(_me));
    //            }
    //            else
    //            {
    //                _me.TransitionGlobalState(new GlobalState_Operational(_me));
    //            }
    //        }
    //    }
    //}

    // --- 4. ESPERANDO RECURSO (NARANJA CLARO) ---
    // Este es el estado vital para el Mixer cuando hace Reserve()
    public class GlobalState_MasterWaiting : IGlobalStated
    {
        private readonly NewPlantUnit _master;      // Yo (Mixer)
        public NewPlantUnit TargetResource { get; } // Lo que quiero (Bomba)

        public GlobalState_MasterWaiting(NewPlantUnit master, NewPlantUnit target)
        {
            _master = master;
            TargetResource = target;
        }

        public string StateLabel => $"Waiting for: {TargetResource.Name}";
        public string HexColor => "#FF9800"; // Naranja
        public bool IsOperational => false;

        public void CheckTransitions()
        {
            // A. VERIFICACIÓN DE PROPIEDAD (Push notification backup) 
            // Si la bomba ya dice que soy su dueño (gracias a AssignResource), ¡Arranco!
            //if (TargetResource.CurrentOwner == _master)
            //{
            //    _master.TransitionGlobalState(new GlobalState_Operational(_master));
            //    return;
            //}

            //// B. VERIFICACIÓN DE DISPONIBILIDAD (Polling backup)
            //// Si no soy dueño, pero veo que la bomba se puso verde (Operational) y está libre,
            //// me despierto para intentar hacer RequestAccess de nuevo en el siguiente ciclo.
            //if (TargetResource.GlobalState.IsOperational &&
            //    TargetResource.CaptureState is NewUnitAvailableToCapture &&
            //    TargetResource.CurrentOwner == null)
            //{
            //    _master.TransitionGlobalState(new GlobalState_Operational(_master));
            //}
        }
    }
}
