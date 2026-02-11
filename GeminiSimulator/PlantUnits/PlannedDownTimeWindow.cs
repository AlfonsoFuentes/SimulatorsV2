namespace GeminiSimulator.PlantUnits
{

    public class PlannedDownTimeWindow
    {
        public TimeOnly Start { get; }
        public TimeOnly End { get; }

        public PlannedDownTimeWindow(TimeSpan start, TimeSpan end)
        {
            Start = TimeOnly.FromTimeSpan(start);
            End = TimeOnly.FromTimeSpan(end);
        }

        public bool IsInside(TimeOnly time)
        {
            if (Start <= End) return time >= Start && time <= End;
            else return time >= Start || time <= End;
        }
    }
}
