using GeminiSimulator.NewFilesSimulations.ManufactureEquipments;
using GeminiSimulator.NewFilesSimulations.PackageLines;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using Color = System.Drawing.Color;

namespace Simulator.Client.Services.ExportServices
{
    public interface IPlantExportService
    {
        byte[] GenerateMasterPlantReport(List<NewLine> lines, List<NewMixer> mixers);
    }

    public class PlantExportService : IPlantExportService
    {
        private readonly Color _bgMixer = ColorTranslator.FromHtml("#673AB7");
        private readonly Color _bgLine = ColorTranslator.FromHtml("#009688");

        public byte[] GenerateMasterPlantReport(List<NewLine> lines, List<NewMixer> mixers)
        {

            using var package = new ExcelPackage();

            // 1. GLOBAL
            var globalSheet = package.Workbook.Worksheets.Add("GLOBAL_TOTALS");
            BuildGlobalSummary(globalSheet, lines, mixers);

            // 2. MIXERS
            foreach (var mixer in mixers)
            {
                var sheet = package.Workbook.Worksheets.Add($"M_{CleanSheetName(mixer.Name)}");
                BuildMixerSheet(sheet, mixer);
            }

            // 3. LINES
            foreach (var line in lines)
            {
                var sheet = package.Workbook.Worksheets.Add($"L_{CleanSheetName(line.Name)}");
                BuildLineSheet(sheet, line);
            }

            return package.GetAsByteArray();
        }

        private void BuildGlobalSummary(ExcelWorksheet sheet, List<NewLine> lines, List<NewMixer> mixers)
        {
            // --- IZQUIERDA: MIXERS (Agrupado por RECURSO) ---
            int row = 1;
            sheet.Cells[row, 1, row, 3].Merge = true;
            sheet.Cells[row, 1].Value = "GLOBAL MIXER RESOURCES USAGE";
            StyleHeader(sheet.Cells[row, 1], _bgMixer);

            row++;
            string[] headMix = { "RESOURCE / STEP TYPE", "TOTAL SUCCESS (Min)", "TOTAL STARVED (Min)" };
            for (int i = 0; i < headMix.Length; i++) sheet.Cells[row, i + 1].Value = headMix[i];
            StyleHeader(sheet.Cells[row, 1, row, 3], Color.Gray);

            // 1. Aplanamos todos los pasos
            var allSteps = mixers
                .SelectMany(m => m.BatchManager.BatchRecord)
                .SelectMany(b => b.Steps)
                .ToList();

            // 2. AGRUPACIÓN DIRECTA POR RESOURCE NAME
            // Esto consolidará "Pump A" en una sola fila, "Operator X" en otra, etc.
            var mixerStats = allSteps
                .GroupBy(s => s.ResourceName) // <--- LA CLAVE QUE PEDISTE
                .Select(g => new
                {
                    Resource = g.Key,
                    SuccessMin = g.Sum(x => x.RealDurationSeconds) / 60.0,
                    StarvedMin = g.Sum(x => x.AccumulatedStarvation) / 60.0
                })
                .OrderByDescending(x => x.SuccessMin)
                .ToList();

            row++;
            foreach (var stat in mixerStats)
            {
                sheet.Cells[row, 1].Value = stat.Resource; // "Pump 101", "Operator A", "Agitation"
                sheet.Cells[row, 2].Value = Math.Round(stat.SuccessMin, 1);
                sheet.Cells[row, 3].Value = Math.Round(stat.StarvedMin, 1);

                if (stat.StarvedMin > 0.1) sheet.Cells[row, 3].Style.Font.Color.SetColor(Color.Red);
                row++;
            }

            // --- DERECHA: LINEAS (Igual que antes, funciona bien) ---
            row = 1;
            int col = 5;
            sheet.Cells[row, col, row, col + 2].Merge = true;
            sheet.Cells[row, col].Value = "GLOBAL LINES TOTALS";
            StyleHeader(sheet.Cells[row, col], _bgLine);

            row++;
            string[] headLine = { "STATE", "DURATION (Min)", "%" };
            for (int i = 0; i < headLine.Length; i++) sheet.Cells[row, col + i].Value = headLine[i];
            StyleHeader(sheet.Cells[row, col, row, col + 2], Color.Gray);

            var reports = lines.SelectMany(l => l.OrderHistory).ToList();
            double grandTotal = reports.Sum(r => r.WallClockTime);
       
           
            if (grandTotal == 0) grandTotal = 1;

            int rL = row + 1;
            // Sumamos propiedades directas
            AddLineRow(sheet, ref rL, col, "Producing", reports.Sum(r => r.ProducingSeconds), grandTotal);
            AddLineRow(sheet, ref rL, col, "Inlet Starvation", reports.Sum(r => r.InletStarvationSeconds), grandTotal);
            AddLineRow(sheet, ref rL, col, "Blocked", reports.Sum(r => r.BlockedByResourceSeconds), grandTotal);
            AddLineRow(sheet, ref rL, col, "Internal Loss", reports.Sum(r => r.InternalLossSeconds), grandTotal);
            AddLineRow(sheet, ref rL, col, "Change Over", reports.Sum(r => r.ChangeOverSeconds), grandTotal);
            AddLineRow(sheet, ref rL, col, "Scheduled Down", reports.Sum(r => r.PlannedDownTimeSeconds), grandTotal);
            AddLineRow(sheet, ref rL, col, "Washing pump", reports.Sum(r => r.WashingPumpWaitSeconds), grandTotal);
            sheet.Cells.AutoFitColumns();
        }

        private void BuildMixerSheet(ExcelWorksheet sheet, NewMixer mixer)
        {
            sheet.Cells["A1:C1"].Merge = true;
            sheet.Cells["A1"].Value = mixer.Name.ToUpper();
            StyleHeader(sheet.Cells["A1"], _bgMixer);

            int row = 2;
            string[] headers = { "RESOURCE / CAUSE", "SUCCESS (Min)", "STARVED (Min)" };
            for (int i = 0; i < headers.Length; i++) sheet.Cells[row, i + 1].Value = headers[i];
            StyleHeader(sheet.Cells[row, 1, row, 3], Color.Gray);

            // Agrupación local por ResourceName
            var steps = mixer.BatchManager.BatchRecord
                .SelectMany(b => b.Steps)
                .GroupBy(s => s.ResourceName) // <--- AGRUPACIÓN PURA POR RECURSO
                .Select(g => new
                {
                    Res = g.Key,
                    Success = g.Sum(x => x.RealDurationSeconds) / 60.0,
                    Starved = g.Sum(x => x.AccumulatedStarvation) / 60.0
                })
                .OrderByDescending(x => x.Success);

            row++;
            foreach (var s in steps)
            {
                sheet.Cells[row, 1].Value = s.Res;
                sheet.Cells[row, 2].Value = Math.Round(s.Success, 1);
                sheet.Cells[row, 3].Value = Math.Round(s.Starved, 1);
                if (s.Starved > 0) sheet.Cells[row, 3].Style.Font.Color.SetColor(Color.Red);
                row++;
            }
            sheet.Cells.AutoFitColumns();
        }

        private void BuildLineSheet(ExcelWorksheet sheet, NewLine line)
        {
            sheet.Cells["A1:C1"].Merge = true;
            sheet.Cells["A1"].Value = line.Name.ToUpper();
            StyleHeader(sheet.Cells["A1"], _bgLine);

            int row = 2;
            string[] headers = { "STATE", "Min", "%" };
            for (int i = 0; i < headers.Length; i++) sheet.Cells[row, i + 1].Value = headers[i];
            StyleHeader(sheet.Cells[row, 1, row, 3], Color.Gray);

            var reps = line.OrderHistory;
            double total = reps.Sum(r => r.WallClockTime);
            if (total == 0) total = 1;

            row++;
            AddLineRow(sheet, ref row, 1, "Producing", reps.Sum(r => r.ProducingSeconds), total);
            AddLineRow(sheet, ref row, 1, "Inlet Starvation", reps.Sum(r => r.InletStarvationSeconds), total);
            AddLineRow(sheet, ref row, 1, "Blocked", reps.Sum(r => r.BlockedByResourceSeconds), total);
            AddLineRow(sheet, ref row, 1, "Internal Loss", reps.Sum(r => r.InternalLossSeconds), total);
            AddLineRow(sheet, ref row, 1, "Change Over", reps.Sum(r => r.ChangeOverSeconds), total);
            AddLineRow(sheet, ref row, 1, "Washout pump", reps.Sum(r => r.WashingPumpWaitSeconds), total);
            sheet.Cells.AutoFitColumns();
        }

        // --- HELPERS ---
        private void AddLineRow(ExcelWorksheet sheet, ref int row, int col, string label, double sec, double total)
        {
            if (sec <= 0.1) return;
            sheet.Cells[row, col].Value = label;
            sheet.Cells[row, col + 1].Value = Math.Round(sec / 60.0, 1);
            sheet.Cells[row, col + 2].Value = sec / total;
            sheet.Cells[row, col + 2].Style.Numberformat.Format = "0.0%";

            if (label.Contains("Starvation") || label.Contains("Blocked"))
                sheet.Cells[row, col].Style.Font.Color.SetColor(Color.Red);

            row++;
        }

        private string CleanSheetName(string name)
        {
            string clean = name.Replace(" ", "").Replace("-", "");
            return clean.Length > 25 ? clean.Substring(0, 25) : clean;
        }

        private void StyleHeader(ExcelRange range, Color color)
        {
            range.Style.Font.Bold = true;
            range.Style.Font.Color.SetColor(Color.White);
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(color);
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            range.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
        }
    }
}