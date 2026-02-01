using Simulator.Shared.NuevaSimlationconQwen.Equipments.Mixers;

namespace Simulator.Shared.NuevaSimlationconQwen.Reports
{
    public class BatchReport
    {
        private readonly Dictionary<MixerTimeType, Amount> _timeAccumulators = new();
        private readonly ProcessMixer _mixer;
        public BatchReport(ProcessMixer mixer)
        {
            BatchId = Guid.NewGuid();
            _mixer = mixer;
            MixerName = mixer.Name;
            foreach (MixerTimeType type in Enum.GetValues<MixerTimeType>())
            {
                _timeAccumulators[type] = new Amount(0, TimeUnits.Second);
            }
        }
        public Guid BatchId { get; set; }
        public string MixerName { get; set; } = string.Empty;
        public DateTime DateReceivedToInitBatch { get; set; }
        public DateTime? DateRealInitiatedBatch { get; private set; }

        string GetStateLabel()
        {
            if (_mixer.CurrentTransferRequest != null)
            {
                return _mixer.OutletState?.StateLabel ?? string.Empty;
            }
            else if (_mixer.CurrentManufactureOrder != null)
            {
                return _mixer.InletState?.StateLabel ?? string.Empty;
            }
            return string.Empty;
        }
        public string MixerState => GetStateLabel();
        public string RealInitiatedBatchString => DateRealInitiatedBatch?.ToString("G") ?? "Not init yet";
        public void InitRealBatchDate(DateTime date)
        {
            if (DateRealInitiatedBatch.HasValue)
            {
                return;
            }
            DateRealInitiatedBatch = date;
        }
        public void RecordTime(MixerTimeType type)
        {
            _timeAccumulators[type] += _mixer.OneSecond;
        }
        public Amount NetBatchTime => _timeAccumulators[MixerTimeType.Batching];
        public Amount WashingTime => _timeAccumulators[MixerTimeType.Washing];
        public Amount TransferingToWipTime => _timeAccumulators[MixerTimeType.TransferToWIP];


        public Amount StarvedTimeByFeeder => _timeAccumulators[MixerTimeType.StarvedByFeeder];
        public Amount StarvedTimeByOperator => _timeAccumulators[MixerTimeType.StarvedByOperator];
        public Amount StarvedTimeByWashoutPump => _timeAccumulators[MixerTimeType.StarvedByWashoutPump];
   
  
        public Amount TotalBatchTime => new Amount(_timeAccumulators.Sum(kv => kv.Value.GetValue(TimeUnits.Minute)), TimeUnits.Minute);
        public Amount StarvedTimeTransferingToWip => _timeAccumulators[MixerTimeType.StarvedByTransferToWIP];
        public Amount StarvedTimeConnectingToWip => _timeAccumulators[MixerTimeType.ConnectingToWIP];
        public Amount TheroicalTransferTimeToWip { get; set; } = new Amount(0, TimeUnits.Minute);
        public Amount BatchSize { get; set; } = new Amount(0, MassUnits.KiloGram);
        public Amount BatchStarvedTime => StarvedTimeByFeeder +    StarvedTimeByOperator +    StarvedTimeByWashoutPump;
         public Amount BatchCycleTime {  get; set; } =new Amount(0, TimeUnits.Minute);

        public Amount TotalStarvedTime=> BatchStarvedTime + StarvedTimeTransferingToWip + StarvedTimeConnectingToWip;

    }
}
