namespace QWENShared.BaseClases.Equipments
{
    public class WashoutTime
    {
        public ProductCategory ProductCategoryCurrent { get; set; } = ProductCategory.None;
        public ProductCategory ProductCategoryNext { get; set; } = ProductCategory.None;
        public Amount MixerWashoutTime { get; set; } = new Amount(0, TimeUnits.Minute);
        public Amount LineWashoutTime { get; set; } = new Amount(0, TimeUnits.Minute);
    }
}
