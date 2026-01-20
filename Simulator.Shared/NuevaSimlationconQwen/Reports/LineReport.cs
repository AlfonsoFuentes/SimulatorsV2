using Simulator.Shared.NuevaSimlationconQwen.Equipments.Lines;

namespace Simulator.Shared.NuevaSimlationconQwen.Reports
{
    public class LineReport
    {
        private readonly Dictionary<LineTimeType, Amount> _timeAccumulators = new();
        private readonly ProcessLine _line;
        public string LineName { get; set; } = string.Empty;
        public LineReport(ProcessLine line)
        {

            _line = line;
            LineName = line.Name;
            foreach (LineTimeType type in Enum.GetValues<LineTimeType>())
            {
                _timeAccumulators[type] = new Amount(0, TimeUnits.Second);
            }
        }
        public void RecordTime(LineTimeType type)
        {
            _timeAccumulators[type] += _line.OneSecond;
        }
        public Amount Producing => _timeAccumulators[LineTimeType.Producing];
        public Amount WashingTime => _timeAccumulators[LineTimeType.Washing];
        public Amount ChangeOverTime => _timeAccumulators[LineTimeType.ChangeOver];

        public Amount LowLevelWipTime => _timeAccumulators[LineTimeType.LowLevelWIP];
        public Amount NotScheduledTime => _timeAccumulators[LineTimeType.NotScheduled];
        public Amount PlannedDowntime => _timeAccumulators[LineTimeType.PlannedDowntime];
        public Amount TotalTime
        {
            get
            {
                Amount total = new Amount(0, TimeUnits.Second);
                foreach (var time in _timeAccumulators.Values)
                {
                    total += time;
                }
                return total;
            }
        }
    }
}
