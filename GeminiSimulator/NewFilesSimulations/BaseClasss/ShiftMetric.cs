using System;
using System.Collections.Generic;
using System.Text;

namespace GeminiSimulator.NewFilesSimulations.BaseClasss
{
    public class ShiftMetric
    {
        public string Name { get; }      // Ej: "Feb-03 Shift 1"
        public DateTime Date { get; }    // Fecha Operativa
        public int ShiftId { get; }      // 1, 2, 3

        public double Total { get; private set; }
        public double Available { get; private set; }
        public double Utilized { get; private set; }

        public ShiftMetric(string name, DateTime date, int shiftId)
        {
            Name = name;
            Date = date;
            ShiftId = shiftId;
        }

        public void AddTotal(double val) => Total += val;
        public void AddAvailable(double val) => Available += val;
        public void AddUtilized(double val) => Utilized += val;

        public double Availability => Total == 0 ? 0 : (Available / Total) * 100.0;
        public double Utilization => Available == 0 ? 0 : (Utilized / Available) * 100.0;
    }
}
