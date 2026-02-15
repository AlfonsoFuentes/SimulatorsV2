using GeminiSimulator.NewFilesSimulations.BaseClasss;
using GeminiSimulator.PlantUnits.Lines;
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
                _line.AddProducingSecond(_line.CurrentFlowRateKg);
            }
            else
            {
            
                _StateName = "Producing Starved by Inlet";
                _line.NotProduce();
                _line.AddInletStarvationSecond(); // Reporte Pérdida (Naranja)
            }
        }

        public void CheckTransitions()
        {
            if (_line.CurrentMassPending <= 0.1 && _line.InletState is NewStarvedByInlet)
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
                _line.AddInternalLossSecond(); // Reporte Pérdida (Rojo) <--- ¡CORREGIDO!
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
        private readonly NewLine _line;
        public string StateLabel => "Analyzing Changeover Path...";
        public string SubStateName => string.Empty;
        public string HexColor => "#9E9E9E"; // Gris neutro para la decisión
        public bool IsProductive => false;

        public NewChangeOverState(NewLine line) => _line = line;

        public void Calculate() { /* Estado de transición instantánea */ }
        public void CheckTransitions()
        {
            var fromMat = _line.CurrentOrder?.Material;
            var nextOrder = _line.NextOrder;

            // 1. Identificamos el siguiente tanque WIP
            var (nextWip, nextPump) = _line.GetWipToProduce(nextOrder!);

            // 2. ¿Cambiamos de tanque?
            bool isSameWip = nextWip == _line.CurrentWipTank;

            double formatTime = _line.CurrentOrder?.TimeToChangeSKU.GetValue(TimeUnits.Second) ?? 0;
            double washTime = 0;
            bool needsWash = _line.NeedsWashing(fromMat, nextOrder?.Material);

            // 3. Lógica de Lavado
            if (needsWash)
            {
                washTime = _line.WashoutRules.GetLineWashout(fromMat!.Category, nextOrder!.Material!.Category).GetValue(TimeUnits.Second);
            }

            // --- EL GRAN AJUSTE LÓGICO ---

            if (!isSameWip)
            {
                // Si cambiamos de tanque, primero soltamos el actual
                _line.DetachWip();

                // El lavado aquí sería solo para la "línea/bomba", 
                // ya que el nuevo tanque se supone que ya viene con el material correcto.
            }

            // 4. DERIVACIÓN FINAL
            if (needsWash && formatTime > 0)
            {
                // Intentar capturar la bomba de lavado si es necesario
                if (!_line.WashingPump!.RequestAccess(_line))
                {
                    _line.WashingPump.Reserve(_line);
                    _line.TransitionGlobalState(new GlobalState_MasterWaiting(_line, _line.WashingPump));
                }
                _line.TransitionOutletState(new NewConcurrentChangeoverState(_line, formatTime, washTime));
            }
            else if (needsWash)
            {
                if (!_line.WashingPump!.RequestAccess(_line))
                {
                    _line.WashingPump.Reserve(_line);
                    _line.TransitionGlobalState(new GlobalState_MasterWaiting(_line, _line.WashingPump));
                }
                _line.TransitionOutletState(new NewWashingOnlyState(_line, washTime));
            }
            else if (formatTime > 0)
            {
                _line.TransitionOutletState(new NewMechanicalSetupState(_line, formatTime));
            }
            else
            {
                // Si no hay nada, simplemente hacemos el cambio de producto 
                // (esto ya gatillará la conexión al nuevo WIP en el ProductChange)
                _line.ProductChange(_line.CurrentDate);
            }
        }
        public void CheckTransitions2()
        {
            var fromMat = _line.CurrentOrder?.Material;
            var toMat = _line.NextOrder?.Material;

            // 1. Obtenemos tiempos base de la orden y matriz
            double formatTime = _line.CurrentOrder?.TimeToChangeSKU.GetValue(TimeUnits.Second) ?? 0;
            double washTime = 0;
            bool needsWash = _line.NeedsWashing(fromMat, toMat);

            if (needsWash)
            {
                washTime = _line.WashoutRules.GetLineWashout(fromMat!.Category, toMat!.Category).GetValue(TimeUnits.Second);
            }

            // 2. LÓGICA DE DERIVACIÓN (Usando tus clases finales)

            if (needsWash && formatTime > 0)
            {
                if (!_line.WashingPump!.RequestAccess(_line))
                {


                    _line.WashingPump.Reserve(_line);
                    _line.TransitionGlobalState(new GlobalState_MasterWaiting(_line, _line.WashingPump));

                }
                // CASO 3: Cambio de partes y Lavado en paralelo
                _line.TransitionOutletState(new NewConcurrentChangeoverState(_line, formatTime, washTime));
            }
            else if (needsWash)
            {
                if (!_line.WashingPump!.RequestAccess(_line))
                {


                    _line.WashingPump.Reserve(_line);
                    _line.TransitionGlobalState(new GlobalState_MasterWaiting(_line, _line.WashingPump));
                   
                }
                // CASO 2: Solo lavado de tubería/máquina
                _line.TransitionOutletState(new NewWashingOnlyState(_line, washTime));
            }
            else if (formatTime > 0)
            {
                // CASO 1: Solo ajuste mecánico de formato
                _line.TransitionOutletState(new NewMechanicalSetupState(_line, formatTime));
            }
            else
            {
                // CASO 0: No hay cambios (SKU idéntico o muy similar)
                _line.DetachWip();
                _line.ProductChange(_line.CurrentDate);
            }
        }
    }
    public class NewMechanicalSetupState : INewOutletState
    {
        private readonly NewLine _line;
        private double _counter = 0;
        private readonly double _targetSeconds;
        public bool IsProductive => false; // El setup no es productivo
        public string HexColor => "#2196F3"; // Azul (Setup/Mecánico)
        public string StateLabel => $"Mechanical Setup: {TimeSpan.FromSeconds(_targetSeconds - _counter):mm\\:ss}";
        public string SubStateName => "Parts Change";

        public NewMechanicalSetupState(NewLine line, double formatTime)
        {
            _line = line;
            _targetSeconds = formatTime;
        }

        public void Calculate()
        {
            _counter++;
            _line.AddChangeOverSecond(); // Registramos tiempo de setup
        }

        public void CheckTransitions()
        {
            if (_counter >= _targetSeconds)
            {
                _line.DetachWip();
                _line.ProductChange(_line.CurrentDate);
            }
        }
    }
    public class NewWashingOnlyState : INewOutletState
    {
        private readonly NewLine _line;
        private double _washingCounter = 0;
        private readonly double _washSeconds;

        public bool IsProductive => false;
        public string HexColor => "#9C27B0";
        public string StateLabel => $"Washing: {TimeSpan.FromSeconds(_washSeconds - _washingCounter):mm\\:ss}";
        public string SubStateName => "Washing";

        public NewWashingOnlyState(NewLine line, double washoutTime)
        {
            _line = line;
            _washSeconds = washoutTime;
        }

        public void Calculate()
        {
            // Si este método se ejecuta, es porque GlobalState es Operational 
            // y ya poseemos la bomba gracias al 'RequestAccess' previo.
            _washingCounter++;
            _line.AddChangeOverSecond();
        }

        public void CheckTransitions()
        {
            if (_washingCounter >= _washSeconds)
            {
                _line.WashingPump!.ReleaseAccess(_line);
                _line.DetachWip();
                _line.ProductChange(_line.CurrentDate);
            }
        }
    }
    public class NewConcurrentChangeoverState : INewOutletState
    {
        private readonly NewLine _line;
        private readonly DateTime _startTime;
        private double _mechCounter = 0;
        private double _washCounter = 0;
        private readonly double _targetMech;
        private readonly double _targetWash;

        // Flags de control
        private bool _washFinished = false;

        public bool IsProductive => false;
        public string HexColor => "#673AB7";
        public string StateLabel => $"Mixed CO (Mech: {Math.Max(0, _targetMech - _mechCounter):F0}s | Wash: {Math.Max(0, _targetWash - _washCounter):F0}s)";
        public string SubStateName => "Concurrent Setup";

        public NewConcurrentChangeoverState(NewLine line, double mechTime, double washTime)
        {
            _line = line;
            _startTime = line.CurrentDate;
            _targetMech = mechTime;
            _targetWash = washTime;
        }

        public void Calculate()
        {
            // 1. ACUMULACIÓN MECÁNICA (Basada en tiempo real)
            _mechCounter = (_line.CurrentDate - _startTime).TotalSeconds;

            // 2. ACUMULACIÓN DE LAVADO (Solo si tenemos la bomba y no hemos terminado)
            if (!_washFinished)
            {
                _washCounter++;
            }

            _line.AddChangeOverSecond();
        }

        public void CheckTransitions()
        {
            // --- GESTIÓN DE LA BOMBA (Handshake) ---
            // Si el contador llegó a la meta pero el flag sigue en false,
            // significa que este es el tick exacto donde terminamos el lavado.
            if (!_washFinished && _washCounter >= _targetWash)
            {
                _washFinished = true;
                _line.WashingPump!.ReleaseAccess(_line); // Liberación limpia en el lugar correcto
            }

            // --- GESTIÓN DEL CAMBIO DE ESTADO ---
            bool mechDone = _mechCounter >= _targetMech;

            if (mechDone && _washFinished)
            {
                _line.DetachWip();
                _line.ProductChange(_line.CurrentDate);
            }
        }
    }
}
