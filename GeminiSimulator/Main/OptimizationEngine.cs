using GeminiSimulator.Materials;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Mixers;
using GeminiSimulator.PlantUnits.ManufacturingEquipments.Skids;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using UnitSystem;

namespace GeminiSimulator.Main
{
    //public class OptimizationEngine
    //{
    //    // Diccionario: ID del Producto -> Tiempo Promedio Real (segundos)
    //    private readonly Dictionary<Guid, double> _realBatchTimes = new();

    //    // Factor Alpha (0.2): Significa que confiamos un 20% en el dato nuevo y 80% en la historia.
    //    // Ayuda a que un solo batch lento no arruine el promedio de golpe.
    //    private const double Alpha = 0.2;

    //    /// <summary>
    //    /// Método para que el Mixer le avise al cerebro cuando termina un batch
    //    /// </summary>
    //    public void RegisterBatchCompletion(Guid productId, double realSeconds)
    //    {
    //        if (!_realBatchTimes.ContainsKey(productId))
    //        {
    //            _realBatchTimes[productId] = realSeconds;
    //        }
    //        else
    //        {
    //            double oldAvg = _realBatchTimes[productId];
    //            // Fórmula de Suavizado Exponencial
    //            _realBatchTimes[productId] = (oldAvg * (1 - Alpha)) + (realSeconds * Alpha);
    //        }
    //    }

    //    /// <summary>
    //    /// Predice cuánto tardará un producto. Si no tiene datos, usa la teoría + 10% de margen.
    //    /// </summary>
    //    public double GetEstimatedBatchTime(ProductDefinition product)
    //    {
    //        if (product == null) return 0;

    //        // 1. Sumamos la duración teórica de todos los pasos de la receta
    //        double theoretical = product.Recipe?.Steps.Sum(s => s.Duration.GetValue(TimeUnits.Second)) ?? 3600;

    //        if (_realBatchTimes.TryGetValue(product.Id, out double realAvg))
    //        {
    //            // Si ya aprendimos, devolvemos la realidad
    //            return realAvg;
    //        }

    //        // Si es la primera vez, devolvemos Teoría + 10% de "Miedo"
    //        return theoretical * 1.10;
    //    }
    //}
    //using System.Collections.Concurrent;

    public class OptimizationEngine
    {
        // CAMBIO 1: La Clave ahora es una Tupla (MixerId, ProductId)
        // Usamos ConcurrentDictionary para que sea Thread-Safe (seguro para hilos)
        private readonly ConcurrentDictionary<(Guid MixerId, Guid ProductId), double> _knowledgeBase = new();

        private const double Alpha = 0.2; // Factor de aprendizaje

        /// <summary>
        /// El Mixer avisa: "Yo (MixerX) terminé el ProductoY en Z segundos"
        /// </summary>
        public void RegisterBatchCompletion(Guid mixerId, Guid productId, double realSeconds)
        {
            var key = (mixerId, productId);

            _knowledgeBase.AddOrUpdate(key,
                // Si es la primera vez que este mixer hace este producto:
                realSeconds,
                // Si ya lo ha hecho antes, actualizamos el promedio:
                (k, oldAvg) => (oldAvg * (1 - Alpha)) + (realSeconds * Alpha)
            );
        }

        /// <summary>
        /// Predice cuánto tarda ESTE mixer específico con ESTE producto
        /// </summary>
        public double PredictBatchDuration(BatchMixer mixer, ProductDefinition product)
        {
            if (product == null || mixer == null) return 3600;

            var key = (mixer.Id, product.Id);

            // 1. ¿Tenemos experiencia previa con este par específico?
            if (_knowledgeBase.TryGetValue(key, out double learnedTime))
            {
                return learnedTime;
            }

            // 2. Si no, usamos la teoría de la receta
            double theoretical = product.Recipe?.Steps.Sum(s => s.Duration.GetValue(TimeUnits.Second)) ?? 3600;

            // 3. FACTOR DE SEGURIDAD INICIAL:
            // Si el mixer es "Viejo" (puedes poner lógica aquí), le damos más margen.
            // Por defecto, Teoría + 10%
            return theoretical * 1.10;
        }
        /// <summary>
        /// NUEVO MÉTODO: Predice el tiempo promedio GLOBAL para un producto (sin importar el mixer).
        /// Útil para la etapa de análisis preliminar (AnalyzeTank) donde aún no asignamos máquina.
        /// </summary>
        public double GetMaxProductTime(ProductDefinition product)
        {
            if (product == null) return 3600;

            // 1. Calculamos la Teoría con margen de seguridad (Plan B)
            double theoretical = product.Recipe?.Steps.Sum(s => s.Duration.GetValue(TimeUnits.Second)) ?? 3600;
            double theoreticalSafe = theoretical * 1.10;

            // 2. Buscamos el Peor Caso Histórico (Plan A)
            var knownTimes = _knowledgeBase
                .Where(k => k.Key.ProductId == product.Id)
                .Select(k => k.Value)
                .ToList();

            double historicalMax = knownTimes.Any() ? knownTimes.Max() : 0;

            // 3. REGLA DE ORO: Nos quedamos con el PEOR de los dos mundos.
            // Si la historia dice 40min pero la teoría dice 60min, asumimos 60min para no arriesgar.
            // Si la historia dice 80min (porque algo falló una vez), asumimos 80min.
            return Math.Max(historicalMax, theoreticalSafe);
        }
    }
}
