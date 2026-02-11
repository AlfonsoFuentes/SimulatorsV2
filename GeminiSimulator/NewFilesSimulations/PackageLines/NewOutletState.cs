using GeminiSimulator.NewFilesSimulations.BaseClasss;
using UnitSystem;

namespace GeminiSimulator.NewFilesSimulations.PackageLines
{

    public class NewLineNotScheduled : INewOutletState
    {
        private NewLine _line;
        public string SubStateName => string.Empty;
        public NewLineNotScheduled(NewLine line)
        {
            _line = line;
        }

        // Nombre para la UI en inglés
        public string StateLabel => "Not Schedule";

        public bool IsProductive => true;

        public string HexColor => "Gemini me ayudara con el color";

        public void Calculate()
        {

        }

        public void CheckTransitions()
        {

        }
    }
    public class NewProducingLineState : INewOutletState
    {
        public string SubStateName => string.Empty;
        double _timeProducing = 0;
        double _counter = 0;
        private NewLine _line;
        public bool IsProductive => true;
        public string HexColor => "Gemini me ayuda con el color";
        public NewProducingLineState(NewLine line)
        {
            _line = line;
            _timeProducing = line.ProducingReimaingTime;

        }
        string _StateName = "Producing";
        // Nombre para la UI en inglés
        public string StateLabel => _StateName;

        public void Calculate()
        {
            if (_line.InletState is NewLineInletAvailable)
            {

              
                _counter++;
                _StateName = "Producing";
                _line.Produce();
            }
            else
            {
                _StateName = "Producing Starved by Inlet";
                _line.NotProduce();
            }
        }

        public void CheckTransitions()
        {
            if (_line.CurrentMassPending <= 0 && _line.InletState is NewStarvedByInlet)
            {
                _line.CurrentWipTank?.SetCoutersToZero();
                if (_line.NextOrder == null)
                {
                    _line.DetachWip();
                    _line.TransitionOutletState(new NewLineNotScheduled(_line));

                    return;
                }
                var nextwip = _line.GetWipToProduce(_line.NextOrder);
                if (nextwip.Wip != null && _line.CurrentWipTank == nextwip.Wip)
                {
                    _line.TransitionOutletState(new NewChangeOverState(_line));
                }
                else
                {
                    _line.DetachWip();
                    _line.ProductChange(_line.CurrentDate);
                }

                return;
            }
            if (_counter >= _timeProducing)
            {
                _line.TransitionOutletState(new NewStarvedByAuState(_line));
            }

        }
    }
    public class NewStarvedByAuState : INewOutletState
    {
        public string SubStateName => string.Empty;
        double _timeStarved = 0;
        double _counter = 0;
        public bool IsProductive => true;
        public string HexColor => "Gemini me ayuda con el color";
        private NewLine _line;
        public NewStarvedByAuState(NewLine line) 
        {
            _line = line;
            _timeStarved = line.ProducingByAuReimaingTime;

        }

        // Nombre para la UI en inglés
        public  string StateLabel => "Starved by AU";

        public  void Calculate()
        {
            if (_line.InletState is NewLineInletAvailable)
            {
                
                _counter++;
                _line.NotProduce();
            }
        }

        public  void CheckTransitions()
        {
            if (_counter >= _timeStarved)
            {
                _line.CalculateAuTime();
                _line.TransitionOutletState(new NewProducingLineState(_line));
            }
        }
    }
    public class NewChangeOverState : INewOutletState
    {
        public bool IsProductive => true;
        public string HexColor => "Gemini me ayuda con el color";
        double _counter = 0;
        double _maxChangeoverTime = 0;
        private NewLine _line;
        public NewChangeOverState(NewLine line) 
        {
            _line = line;
            var formatTime = line.CurrentOrder?.TimeToChangeSKU.GetValue(TimeUnits.Second) ?? 0;
            double washoutTime = 0;
            if (_line.CurrentOrder != null && _line.NextOrder != null)
            {

                // Extraemos las categorías para la consulta
                var fromCat = _line.CurrentOrder.Category;
                var toCat = _line.NextOrder.Category;

                // Buscamos en la matriz que está en el SimulationContext
                washoutTime = _line.WashoutRules.GetLineWashout(fromCat, toCat).GetValue(TimeUnits.Second) ;

            }

            // 3. El tiempo real de parada es el mayor de los dos
            _maxChangeoverTime = Math.Max(formatTime, washoutTime);

        }
        string _StateName = "Change Over";
        // Nombre para la UI en inglés
        public  string StateLabel => _StateName;

        public string SubStateName => string.Empty;

        Amount pendingtime = new Amount(0, TimeUnits.Minute);
        public  void Calculate()
        {

            _counter++;
            var pendingtimesec = _maxChangeoverTime - _counter;
            pendingtime.SetValue(pendingtimesec, TimeUnits.Second);
            _StateName = $"{"Change Over"} {pendingtime.ToString()}";
        }

        public  void CheckTransitions()
        {
            if (_counter >= _maxChangeoverTime)
            {

                _line.DetachWip();
                // Reiniciamos para la nueva producción
                _line.ProductChange(_line.CurrentDate);


            }

        }
    }
}
