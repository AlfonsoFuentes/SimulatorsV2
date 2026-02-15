//namespace GeminiSimulator.PlantUnits.Tanks
//{
//    public static class TankSpecializer
//    {
//        public static StorageTank CreateSpecialized(StorageTank generic)
//        {
//            // Usamos los métodos de extensión directamente sobre el objeto 'generic'
//            bool fromMixer = generic.ReceivesFrom<BatchMixer>();
//            bool fromContinuous = generic.ReceivesFrom<ContinuousSystem>();
//            bool toPackaging = generic.FeedsTo<PackagingLine>();
//            bool toMixer = generic.FeedsTo<BatchMixer>();
//            bool toContinuous = generic.FeedsTo<ContinuousSystem>();

//            // TIPO 4: WIP de Mixer -> Línea
//            if (toPackaging && fromMixer)
//                return new BatchWipTank(generic);

//            // TIPO 3: WIP de Sistema Continuo -> Línea
//            if (toPackaging && fromContinuous)
//                return new ContinuousWipTank(generic);

//            // TIPO 2: InHouse (Mixer -> Tank -> Mixer/Cont)
//            if (fromMixer && (toMixer || toContinuous))
//                return new InHouseTank(generic);

//            // TIPO 1: Raw Material (Por descarte)
//            return new RawMaterialTank(generic);
//        }
//    }
//}
