namespace QWENShared.BaseClases.Equipments
{
    public interface IPlannedDownTimeState
    {
        bool CheckStatus(DateTime currentdate);
    }
}
