using GeminiSimulator.DesignPatterns;
using System.Xml;
using UnitSystem;

//namespace GeminiSimulator.PlantUnits.Lines.States
//{
//    public abstract class LineOutletState : IUnitState
//    {
//        protected PackagingLine _line = null!;
//        public abstract string StateName { get; }
//        public virtual string SubStateName => string.Empty;
//        public LineOutletState(PackagingLine line)
//        {
//            _line = line;
//        }



//        public virtual void Calculate() { /* Por defecto no hace nada */ }

//        public abstract void CheckTransitions();
//    }
//    public class LineNotScheduled : LineOutletState
//    {
//        public LineNotScheduled(PackagingLine line) : base(line)
//        {
//        }

//        // Nombre para la UI en inglés
//        public override string StateName => "Not Schedule";

//        public override void Calculate()
//        {
//            _line.AccumulateTime(LineStateCategory.OrganizationalLoss);
//        }

//        public override void CheckTransitions()
//        {

//        }
//    }
//    public class ProducingLineState : LineOutletState
//    {
//        double _timeProducing = 0;
//        double _counter = 0;
//        public ProducingLineState(PackagingLine line) : base(line)
//        {

//            _timeProducing = line.ProducingReimaingTime;

//        }
//        string _StateName = "Producing";
//        // Nombre para la UI en inglés
//        public override string StateName => _StateName;

//        public override void Calculate()
//        {
//            if (_line.InboundState is LineInletAvailable)
//            {

//                _line.AccumulateTime(LineStateCategory.Operating);
//                _counter++;
//                _StateName = "Producing";
//                _line.Produce();
//            }
//            else
//            {
//                _StateName = "Producing Starved by Inlet";
//                _line.NotProduce();
//            }
//        }

//        public override void CheckTransitions()
//        {
//            if (_line.CurrentMassPending <= 0 && _line.InboundState is StarvedByInlet)
//            {
//                _line.CurrentWipTank?.CountersToZero();
//                if (_line.NextOrder == null)
//                {
//                    _line.DetachWip();
//                    _line.TransitionOutbound(new LineNotScheduled(_line));

//                    return;
//                }
//                var nextwip = _line.GetWipToProduce(_line.NextOrder);
//                if (nextwip != null && _line.CurrentWipTank == nextwip)
//                {
//                    _line.TransitionOutbound(new ChangeOverState(_line));
//                }
//                else
//                {
//                    _line.DetachWip();
//                    _line.ProductChange(_line.CurrentDate);
//                }

//                return;
//            }
//            if (_counter >= _timeProducing)
//            {
//                _line.TransitionOutbound(new StarvedByAuState(_line));
//            }

//        }
//    }
//    public class StarvedByAuState : LineOutletState
//    {
//        double _timeStarved = 0;
//        double _counter = 0;
//        public StarvedByAuState(PackagingLine line) : base(line)
//        {

//            _timeStarved = line.ProducingByAuReimaingTime;

//        }

//        // Nombre para la UI en inglés
//        public override string StateName => "Starved by AU";

//        public override void Calculate()
//        {
//            if (_line.InboundState is LineInletAvailable)
//            {
//                _line.AccumulateTime(LineStateCategory.Operating);
//                _counter++;
//                _line.NotProduce();
//            }
//        }

//        public override void CheckTransitions()
//        {
//            if (_counter >= _timeStarved)
//            {
//                _line.CalculateAuTime();
//                _line.TransitionOutbound(new ProducingLineState(_line));
//            }
//        }
//    }
//    public class ChangeOverState : LineOutletState
//    {

//        double _counter = 0;
//        double _maxChangeoverTime = 0;
//        public ChangeOverState(PackagingLine line) : base(line)
//        {

//            var formatTime = line.CurrentOrder?.TimeToChangeSKU.GetValue(TimeUnits.Second) ?? 0;
//            double washoutTime = 0;
//            if (_line.CurrentOrder != null && _line.NextOrder != null)
//            {

//                // Extraemos las categorías para la consulta
//                var fromCat = _line.CurrentOrder.Category;
//                var toCat = _line.NextOrder.Category;

//                // Buscamos en la matriz que está en el SimulationContext
//                washoutTime = _line.Context?.WashoutRules.GetLineWashout(fromCat, toCat).GetValue(TimeUnits.Second) ?? 0;

//            }

//            // 3. El tiempo real de parada es el mayor de los dos
//            _maxChangeoverTime = Math.Max(formatTime, washoutTime);

//        }
//        string _StateName = "Change Over";
//        // Nombre para la UI en inglés
//        public override string StateName => _StateName;

//        Amount pendingtime = new Amount(0, TimeUnits.Minute);
//        public override void Calculate()
//        {
//            _line.AccumulateTime(LineStateCategory.TechnicalLoss);
//            _counter++;
//            var pendingtimesec = _maxChangeoverTime - _counter;
//            pendingtime.SetValue(pendingtimesec, TimeUnits.Second);
//            _StateName = $"{"Change Over"} {pendingtime.ToString()}";
//        }

//        public override void CheckTransitions()
//        {
//            if (_counter >= _maxChangeoverTime)
//            {

//                _line.DetachWip();
//                // Reiniciamos para la nueva producción
//                _line.ProductChange(_line.CurrentDate);


//            }

//        }
//    }
//}
