namespace Simulator.Shared.NuevaSimlationconQwen.Reports
{
    public class WipTankForLineReport
    {
        public string Name { get; set; } = string.Empty;
        public Amount TotalStarvedTime => new Amount(Batches.Sum(b => b.TotalStarvedTime.GetValue(TimeUnits.Minute)), TimeUnits.Minute);
        public Amount TotalRealBatchTime => new Amount(Batches.Sum(b => b.TotalBatchTime.GetValue(TimeUnits.Minute)), TimeUnits.Minute);
        public double OverallStarvationPercentage =>
            TotalRealBatchTime.GetValue(TimeUnits.Minute) > 0
                ? Math.Round((TotalStarvedTime.GetValue(TimeUnits.Minute) / TotalRealBatchTime.GetValue(TimeUnits.Minute)) * 100, 2)
                : 0;

        public Amount TotalStarvedTimeByFeeder => new Amount(Batches.Sum(b => b.StarvedTimeByFeeder.GetValue(TimeUnits.Minute)), TimeUnits.Minute);
        public Amount TotalStarvedTimeByOperator => new Amount(Batches.Sum(b => b.StarvedTimeByOperator.GetValue(TimeUnits.Minute)), TimeUnits.Minute);
        public Amount TotalStarvedTimeByWashoutPump => new Amount(Batches.Sum(b => b.StarvedTimeByWashoutPump.GetValue(TimeUnits.Minute)), TimeUnits.Minute);
        public Amount TotalStarvedTimeTransferingToWip => new Amount(Batches.Sum(b => b.StarvedTimeTransferingToWip.GetValue(TimeUnits.Minute)), TimeUnits.Minute);


        public List<BatchReport> Batches { get; set; } = new List<BatchReport>();
    }
}
