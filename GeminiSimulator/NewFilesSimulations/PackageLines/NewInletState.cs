using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.NewFilesSimulations.Tanks;
using GeminiSimulator.PlantUnits.Lines;

namespace GeminiSimulator.NewFilesSimulations.PackageLines
{

    public class NewNotScheduleInlet : INewInletState
    {
        NewLine _line;
        public NewNotScheduleInlet(NewLine line)
        {
            _line = line;
        }

        // Leyenda para la UI en inglés
        public string StateLabel => "Not Scheduled";

        public bool IsProductive => true;//Gemini me ayuda con este concepto

        public string HexColor => "Gemini me ayuda con el color";

        public void Calculate()
        {
            
        }

        public void CheckTransitions()
        {
            // 1. OBTENER EL TURNO ACTUAL SEGÚN LA HORA


        }
    }
    public class NewOutOfShiftState : INewInletState
    {
        NewLine _line;
        public NewOutOfShiftState(NewLine line) { _line = line; }

        // Leyenda para la UI en inglés
        public string StateLabel => "Out of Shift";
        public bool IsProductive => true;//Gemini me ayuda con este concepto

        public string HexColor => "Gemini me ayuda con el color";
        public void Calculate()
        {
            _line.AddStandbySecond();
        }

        public void CheckTransitions()
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

                _line.TransitionInletState(new NewLineInletAvailable(_line));

            }

        }
    }
    //Esto ya estaria programado en la clase base como estoy copiando lo mantengo asi para luego refactorizar

    public class NewReadyToProduce : INewInletState
    {
        NewLine _line;
        public NewReadyToProduce(NewLine line)
        {

            _line = line;

        }
        public bool IsProductive => true;//Gemini me ayuda con este concepto

        public string HexColor => "Gemini me ayuda con el color";
        // Nombre para la UI en inglés
        public string StateLabel => "Ready to Produce";

        public void Calculate()
        {
   
        }

        public void CheckTransitions()
        {
            if (_line.CurrentWipTank != null && _line.CurrentWipTank.OutletState is NewTankNormalOutletState)
            {
                _line.TransitionInletState(new NewLineInletAvailable(_line));
            }
        }
    }
    public class NewStarvedByInlet : INewInletState
    {
        NewLine _line;
        public NewStarvedByInlet(NewLine line)
        {
            _line = line;


        }
        public bool IsProductive => true;//Gemini me ayuda con este concepto

        public string HexColor => "Gemini me ayuda con el color";
        // Nombre para la UI en inglés
        public string StateLabel => "Starved by Inlet";

        public void Calculate()
        {
           
        }

        public void CheckTransitions()
        {
            if (_line.CurrentWipTank != null && _line.CurrentWipTank.OutletState is  NewTankNormalOutletState)
            {
                _line.TransitionInletState(new NewLineInletAvailable(_line));
            }
        }
    }
    public class NewLineInletAvailable : INewInletState
    {
        NewLine _line;
        public NewLineInletAvailable(NewLine line)
        {

            _line = line;

        }
        public bool IsProductive => true;//Gemini me ayuda con este concepto

        public string HexColor => "Gemini me ayuda con el color";
        // Nombre para la UI en inglés
        public string StateLabel => "Inlet Available";

        public void Calculate()
        {

        }

        public void CheckTransitions()
        {
            if (_line.CurrentWipTank != null && _line.CurrentWipTank.OutletState is not NewTankNormalOutletState)
            {
                _line.TransitionInletState(new NewStarvedByInlet(_line));
            }
        }
    }
}
