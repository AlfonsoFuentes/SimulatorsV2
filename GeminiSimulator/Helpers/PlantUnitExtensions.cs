//namespace GeminiSimulator.Helpers
//{
//    public static class PlantUnitExtensions
//    {
//        /// <summary>
//        /// Verifica si la unidad recibe flujo de un equipo tipo T, 
//        /// ya sea directo o a través de una bomba.
//        /// </summary>
//        public static bool ReceivesFrom<T>(this PlantUnit unit) where T : PlantUnit
//        {
//            // 1. Conexión directa
//            if (unit.Inputs.Any(u => u is T)) return true;

//            // 2. A través de una bomba (Salto de nivel)
//            return unit.Inputs.OfType<Pump>().Any(p => p.Inputs.Any(u => u is T));
//        }

//        /// <summary>
//        /// Verifica si la unidad alimenta a un equipo tipo T, 
//        /// ya sea directo o a través de una bomba.
//        /// </summary>
//        public static bool FeedsTo<T>(this PlantUnit unit) where T : PlantUnit
//        {
//            // 1. Conexión directa
//            if (unit.Outputs.Any(u => u is T)) return true;

//            // 2. A través de una bomba (Salto de nivel)
//            return unit.Outputs.OfType<Pump>().Any(p => p.Outputs.Any(u => u is T));
//        }
//    }
//}
