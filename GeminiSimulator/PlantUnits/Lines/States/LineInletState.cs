using GeminiSimulator.DesignPatterns;

namespace GeminiSimulator.PlantUnits.Lines.States
{
    public abstract class LineInletState : IUnitState
    {
        protected PackagingLine _line = null!;
        public abstract string StateName { get; }
        public virtual string SubStateName => string.Empty;
        public LineInletState(PackagingLine line)
        {
            _line = line;
        }



        public virtual void Calculate() { /* Por defecto no hace nada */ }

        public abstract void CheckTransitions();
    }
    public class NotScheduleInlet : LineInletState
    {
        public NotScheduleInlet(PackagingLine line) : base(line) { }

        // Leyenda para la UI en inglés
        public override string StateName => "Not Scheduled";

        public override void Calculate()
        {
           
        }

        public override void CheckTransitions()
        {
            // 1. OBTENER EL TURNO ACTUAL SEGÚN LA HORA
        

        }
    }
    public class OutOfShiftState : LineInletState
    {
        public OutOfShiftState(PackagingLine line) : base(line) { }

        // Leyenda para la UI en inglés
        public override string StateName => "Out of Shift";

        public override void Calculate()
        {
            _line.AccumulateTime(LineStateCategory.OrganizationalLoss);
        }

        public override void CheckTransitions()
        {
            // 1. OBTENER EL TURNO ACTUAL SEGÚN LA HORA
            int currentShiftIndex = _line.GetCurrentShiftIndex(_line.CurrentDate);

            // 2. VALIDAR SI LA LÍNEA TIENE PERMISO PARA ESTE TURNO
            bool isScheduledNow = _line.IsLineScheduledForShift(currentShiftIndex, _line.ShiftType);

            if (isScheduledNow)
            {
                // Si el turno ya es válido, la línea "despierta".
                // El siguiente paso lógico es evaluar si hay una parada programada 
                // (ej. reunión de inicio de turno) o si vamos directo a preparar la máquina.

                if (_line.IsOnPlannedBreak(_line.CurrentDate))
                {
                    _line.TransitionInBound(new PlannedDowntimeState(_line));
                }

            }

        }
    }
    public class PlannedDowntimeState : LineInletState
    {
        public PlannedDowntimeState(PackagingLine line) : base(line) { }

        // Etiqueta para la UI en inglés
        public override string StateName => "Planned Downtime";

        public override void Calculate()
        {
            _line.AccumulateTime(LineStateCategory.PlannedLoss);
        }

        public override void CheckTransitions()
        {
            // 1. VALIDAR SI CONTINÚA LA PARADA PROGRAMADA
            // Si el reloj sigue dentro de una de las ventanas de '_scheduledBreaks'
            if (_line.IsOnPlannedBreak(_line.CurrentDate))
            {
                // Nos mantenemos en este estado, no hay a dónde ir.
                return;
            }

            // 2. SI LA PARADA TERMINÓ, EVALUAMOS LA SIGUIENTE PRIORIDAD: EL TURNO
            int currentShiftIndex = _line.GetCurrentShiftIndex(_line.CurrentDate);
            bool isScheduledNow = _line.IsLineScheduledForShift(currentShiftIndex, _line.ShiftType);

            if (isScheduledNow)
            {
                _line.TransitionInBound(new LineInletAvailable(_line));
            }
            else
            {
                // Si el break terminó justo cuando también terminaba el turno,
                // pasamos al estado de espera de personal.
                _line.TransitionInBound(new OutOfShiftState(_line));
            }
        }
    }
    public class ReadyToProduce : LineInletState
    {
        public ReadyToProduce(PackagingLine line) : base(line)
        {

           

        }

        // Nombre para la UI en inglés
        public override string StateName => "Ready to Produce";

        public override void Calculate()
        {

        }

        public override void CheckTransitions()
        {

        }
    }
    public class StarvedByInlet : LineInletState
    {
        public StarvedByInlet(PackagingLine line) : base(line)
        {



        }

        // Nombre para la UI en inglés
        public override string StateName => "Starved by Inlet";

        public override void Calculate()
        {
            _line.AccumulateTime(LineStateCategory.SupplyLoss);
        }

        public override void CheckTransitions()
        {

        }
    }
    public class LineInletAvailable : LineInletState
    {
        public LineInletAvailable(PackagingLine line) : base(line)
        {



        }

        // Nombre para la UI en inglés
        public override string StateName => "Inlet Available";

        public override void Calculate()
        {

        }

        public override void CheckTransitions()
        {
               
        }
    }
}
