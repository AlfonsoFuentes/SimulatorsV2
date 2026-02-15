using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using System;
using System.Collections.Generic;
using System.Text;

namespace GeminiSimulator.NewFilesSimulations.Context
{
    // --- DTOs para el reporte ---
    public class AssetStarvedReport
    {
        public string Name { get; set; } = string.Empty;
        public double StarvedMinutes { get; set; }
        public double OperationalMinutes { get; set; }
        public double Availability => (OperationalMinutes + StarvedMinutes) > 0
            ? (OperationalMinutes / (OperationalMinutes + StarvedMinutes)) * 100
            : 100;
        public double Efficiency => OperationalMinutes > 0
        ? Math.Max(0, (OperationalMinutes - StarvedMinutes) / OperationalMinutes * 100)
        : 100;
    }

    public class StopReasonReport
    {
        public string Reason { get; set; } = string.Empty; // Addition, Transfer, Washing, Setup
        public double TotalMinutes { get; set; }
    }

    //public class FrictionReport
    //{
    //    public string MixerName { get; set; } = string.Empty;
    //    public string ResourceName { get; set; } = string.Empty;
    //    public double LostMinutes { get; set; }
    //    public int StopCount { get; set; } // Nueva propiedad para la frecuencia
    //}

    // --- Servicio de Analítica ---
    //public class PlantAnalyticsService
    //{
    //    List<NewMixer> mixers = new();
    //    public void SetMixer(List<NewMixer> _mixers)
    //    {
    //        mixers = _mixers;
    //    }
    //    public List<AssetStarvedReport> GetMixerRanking() =>
    //        mixers.Select(m => new AssetStarvedReport
    //        {
    //            Name = m.Name,
    //            StarvedMinutes = m.BatchManager.ExecutionHistory.Sum(s => s.AccumulatedStarvation) / 60.0,
    //            OperationalMinutes = m.BatchManager.ExecutionHistory.Sum(s => s.TheroicalDurationSeconds) / 60.0
    //        }).OrderByDescending(r => r.StarvedMinutes).ToList();

    //    public List<AssetStarvedReport> GetPumpRanking() =>
    //        mixers.SelectMany(m => m.BatchManager.ExecutionHistory)
    //              .Where(s => s.ResourceName != "N/A")
    //              .GroupBy(s => s.ResourceName)
    //              .Select(g => new AssetStarvedReport
    //              {
    //                  Name = g.Key,
    //                  StarvedMinutes = g.Sum(s => s.AccumulatedStarvation) / 60.0,
    //                  OperationalMinutes = g.Sum(s => s.TheroicalDurationSeconds) / 60.0
    //              }).OrderByDescending(r => r.StarvedMinutes).ToList();

    //    public List<StopReasonReport> GetStopReasons() =>
    //        mixers.SelectMany(m => m.BatchManager.ExecutionHistory)
    //              .GroupBy(s => s.GetType().Name)
    //              .Select(g => new StopReasonReport
    //              {
    //                  Reason = g.Key.Replace("Step", ""), // Clean name: Mass, Discharge, etc.
    //                  TotalMinutes = g.Sum(s => s.AccumulatedStarvation) / 60.0
    //              }).OrderByDescending(r => r.TotalMinutes).ToList();

    //    public List<FrictionReport> GetFrictionMatrix() =>
    // mixers.SelectMany(m => m.BatchManager.ExecutionHistory)
    //       .Where(s => s.AccumulatedStarvation > 0 && s.ResourceName != "N/A")
    //       // Cambiamos m.Name por s.MixerName (que apunta a s._mixer.Name)
    //       .GroupBy(s => new { s.MixerName, s.ResourceName })
    //       .Select(g => new FrictionReport
    //       {
    //           MixerName = g.Key.MixerName,
    //           ResourceName = g.Key.ResourceName,
    //           LostMinutes = g.Sum(s => s.AccumulatedStarvation) / 60.0
    //       })
    //       .OrderByDescending(r => r.LostMinutes)
    //       .ToList();
    //}
}
