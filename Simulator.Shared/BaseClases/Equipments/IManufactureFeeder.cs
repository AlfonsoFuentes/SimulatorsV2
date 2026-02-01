namespace QWENShared.BaseClases.Equipments
{
    public interface IManufactureFeeder : IEquipment
    {
        Amount Flow { get; set; }
        Amount ActualFlow { get; set; }
        bool IsForWashout { get; set; }
        bool IsAnyTankInletStarved();
        bool IsAnyTankInletStarvedRealesed();
        IEquipment OcuppiedBy { get; set; }
        void EnqueueWaitingEquipment(IEquipment equipment);

        int GetWaitingQueueLength();
        void NotifyNextWaitingEquipment();


        bool StarvedByInletState { get; set; }
        LinkedList<IEquipment> WaitingQueue { get; }
    }
}
