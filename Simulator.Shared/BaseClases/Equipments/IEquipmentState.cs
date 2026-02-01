namespace QWENShared.BaseClases.Equipments
{
    public interface IEquipmentState
    {

        string StateLabel { get; set; }
        void CheckStatus(DateTime CurrentDate);
        void Run(DateTime CurrentDate);
        void Report(DateTime CurrentDate);
        void Calculate(DateTime currentdate);

    }
}
