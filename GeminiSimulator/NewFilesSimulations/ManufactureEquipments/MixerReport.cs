namespace GeminiSimulator.NewFilesSimulations.ManufactureEquipments
{


    public class StepTimeManager
    {
        private readonly NewStepLink _owner;

        public double TheoreticalDuration { get; set; }
        public double WorkedSeconds { get; private set; }
        public double StarvationSeconds { get; private set; }
        public string LastResourceCulpable=>_owner.ResourceName;

        private DateTime? _fixedStartTime; // El dato real se guarda aquí

        public StepTimeManager(NewStepLink owner, double theoreticalDuration)
        {
            _owner = owner;
            TheoreticalDuration = theoreticalDuration;
        }

        // EL MÉTODO QUE FALTABA:
        public void SetFixedStart(DateTime date) => _fixedStartTime = date;

        public DateTime StartDate => _fixedStartTime
                                     ?? _owner.PreviousStep?.TimeManager.ExpectedEndDate
                                     ?? _owner.CurrentDate;

        // LÓGICA DE SUBESTIMACIÓN:
        // Si WorkedSeconds > TheoreticalDuration, el tiempo base es WorkedSeconds.
        // $$ExpectedEndDate = StartDate + \max(TheoreticalDuration, WorkedSeconds) + StarvationSeconds$$
        public DateTime ExpectedEndDate => StartDate.AddSeconds(
            Math.Max(TheoreticalDuration, WorkedSeconds) + StarvationSeconds
        );

        public DateTime TheoreticalEndDate => StartDate.AddSeconds(TheoreticalDuration);
        public DateTime? RealEndDate { get; private set; }

        public void AddWork(double seconds) => WorkedSeconds += seconds;
        public void AddStarvation()
        {
            StarvationSeconds += 1;
           
        }

        public void CloseStep(DateTime finalDate) => RealEndDate = finalDate;
    }



}
