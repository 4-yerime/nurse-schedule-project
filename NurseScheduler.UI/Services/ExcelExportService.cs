using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using NurseScheduler.UI.Models;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace NurseScheduler.UI.Services
{
    public class ExcelExportService
    {
        public void Export(Schedule schedule, string hospitalName, string filePath)
        {
            var entries = App.Database.GetScheduleEntries(schedule.Id);
            var nurses = App.Database.GetAllNurses();
            var shifts = App.Database.GetAllShifts();
            var rules = App.Database.GetAllRules();

            var days = new List<DateTime>();
            for (var d = schedule.StartDate; d <= schedule.EndDate; d = d.AddDays(1))
                days.Add(d);

            if (days.Count == 0)
                throw new Exception("Çizelge tarih aralığı geçerli değil veya boş.");

            if (entries.Count == 0)
                throw new Exception("Çizelge içeriği boş. Lütfen önce çizelgeyi oluşturun veya algoritmanın başarıyla tamamlandığından emin olun.");

            using var package = new ExcelPackage();

            CreateOzetSheet(package, schedule, entries, nurses, shifts, days, hospitalName);
            CreateDetaySheet(package, schedule, entries, nurses, shifts, days, hospitalName);
            CreateIstatistiklerSheet(package, schedule, entries, nurses, shifts, days, rules);

            package.SaveAs(new FileInfo(filePath));
        }

        private static void CreateOzetSheet(ExcelPackage pkg, Schedule schedule,
            List<ScheduleEntry> entries, List<Nurse> nurses, List<ShiftDefinition> shifts,
            List<DateTime> days, string hospitalName)
        {
            var ws = pkg.Workbook.Worksheets.Add("Özet");
            int row = 1;

            // Title
            ws.Cells[row, 1, row, days.Count + 4].Merge = true;
            ws.Cells[row, 1].Value = $"🏥 {hospitalName}  —  {schedule.Name}";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 16;
            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(30, 30, 60));
            ws.Cells[row, 1].Style.Font.Color.SetColor(Color.White);
            ws.Row(row).Height = 30;
            row++;

            // Info row
            ws.Cells[row, 1].Value = $"Tarih: {schedule.StartDate:dd.MM.yyyy} – {schedule.EndDate:dd.MM.yyyy}";
            ws.Cells[row, 3].Value = $"Algoritma: {schedule.AlgorithmMode}";
            ws.Cells[row, 5].Value = $"Fitness: {schedule.FitnessScore:F1}";
            ws.Cells[row, 7].Value = $"İhlal: {schedule.TotalViolations}";
            foreach (int c in new[] { 1, 3, 5, 7 })
            {
                ws.Cells[row, c].Style.Font.Color.SetColor(Color.FromArgb(180, 180, 180));
                ws.Cells[row, c].Style.Font.Italic = true;
            }
            row += 2;

            // Column headers
            ws.Cells[row, 1].Value = "Hemşire";
            ws.Cells[row, 2].Value = "Alt Birim";
            for (int d = 0; d < days.Count; d++)
            {
                ws.Cells[row, d + 3].Value = days[d].Day;
                bool isWeekend = days[d].DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                ws.Cells[row, d + 3].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, d + 3].Style.Fill.BackgroundColor.SetColor(isWeekend ? Color.FromArgb(50, 20, 20) : Color.FromArgb(25, 25, 45));
                ws.Cells[row, d + 3].Style.Font.Color.SetColor(isWeekend ? Color.FromArgb(230, 100, 100) : Color.FromArgb(150, 150, 200));
                ws.Cells[row, d + 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, d + 3].Style.Font.Size = 9;
            }
            ws.Cells[row, days.Count + 3].Value = "Toplam";
            ws.Cells[row, days.Count + 4].Value = "H.Sonu";
            StyleHeaderRow(ws.Cells[row, 1, row, days.Count + 4]);
            row++;

            // Day names row
            ws.Cells[row, 1].Value = "";
            ws.Cells[row, 2].Value = "";
            for (int d = 0; d < days.Count; d++)
            {
                ws.Cells[row, d + 3].Value = days[d].ToString("ddd");
                ws.Cells[row, d + 3].Style.Font.Size = 8;
                ws.Cells[row, d + 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, d + 3].Style.Font.Color.SetColor(Color.FromArgb(100, 100, 150));
            }
            row++;

            // Nurse data rows
            var entryDict = entries.GroupBy(e => e.NurseId).ToDictionary(g => g.Key, g => g.ToDictionary(e => e.EntryDate.Date));
            var shiftDict = shifts.ToDictionary(s => s.Id);

            foreach (var nurse in nurses.Where(n => entryDict.ContainsKey(n.Id)))
            {
                ws.Cells[row, 1].Value = nurse.FullName + (nurse.IsHeadNurse ? " [BH]" : "");
                ws.Cells[row, 1].Style.Font.Bold = nurse.IsHeadNurse;
                ws.Cells[row, 1].Style.Font.Color.SetColor(Color.White);
                ws.Cells[row, 2].Value = nurse.SubUnitName;
                ws.Cells[row, 2].Style.Font.Color.SetColor(Color.FromArgb(120, 150, 180));

                int total = 0, weekendTotal = 0;
                for (int d = 0; d < days.Count; d++)
                {
                    var cell = ws.Cells[row, d + 3];
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                    cell.Style.VerticalAlignment = ExcelVerticalAlignment.Center;
                    cell.Style.Font.Size = 7.5f; // Slightly smaller to fit names
                    cell.Style.ShrinkToFit = true;

                    if (entryDict[nurse.Id].TryGetValue(days[d].Date, out var entry))
                    {
                        if (entry.IsLeave) 
                        { 
                            cell.Value = "İ"; 
                            SetCellColor(cell, Color.FromArgb(20, 60, 20), Color.FromArgb(100, 200, 100)); 
                        }
                        else if (entry.ShiftId.HasValue && shiftDict.TryGetValue(entry.ShiftId.Value, out var shift))
                        {
                            cell.Value = shift.Name; // Full name as requested
                            
                            var bg = HexToColor(shift.ColorHex);
                            SetCellColor(cell, bg, Color.White);
                            if (entry.IsHeadNurseDay) cell.Style.Font.Bold = true;
                            total++;
                            if (days[d].DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) weekendTotal++;
                        }
                        else { cell.Value = "-"; }
                    }
                }
                ws.Cells[row, days.Count + 3].Value = total;
                ws.Cells[row, days.Count + 3].Style.Font.Bold = true;
                ws.Cells[row, days.Count + 3].Style.Font.Color.SetColor(Color.FromArgb(0, 188, 212));
                ws.Cells[row, days.Count + 3].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Cells[row, days.Count + 4].Value = weekendTotal;
                ws.Cells[row, days.Count + 4].Style.Font.Color.SetColor(Color.FromArgb(255, 179, 0));
                ws.Cells[row, days.Count + 4].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;

                ws.Row(row).Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Row(row).Style.Fill.BackgroundColor.SetColor(row % 2 == 0 ? Color.FromArgb(24, 24, 40) : Color.FromArgb(30, 30, 50));
                row++;
            }

            // Column widths - adapted for names
            ws.Column(1).Width = 20;
            ws.Column(2).Width = 12;
            for (int d = 1; d <= days.Count; d++) ws.Column(d + 2).Width = 7.0; // Wider columns for names
            ws.Column(days.Count + 3).Width = 8;
            ws.Column(days.Count + 4).Width = 8;
            ws.View.FreezePanes(5, 3);
        }

        private static void CreateDetaySheet(ExcelPackage pkg, Schedule schedule,
            List<ScheduleEntry> entries, List<Nurse> nurses, List<ShiftDefinition> shifts,
            List<DateTime> days, string hospitalName)
        {
            var ws = pkg.Workbook.Worksheets.Add("Detay");
            int row = 1;

            ws.Cells[row, 1, row, days.Count + 6].Merge = true;
            ws.Cells[row, 1].Value = $"{hospitalName} — {schedule.Name} — Detaylı Çizelge";
            ws.Cells[row, 1].Style.Font.Bold = true;
            ws.Cells[row, 1].Style.Font.Size = 14;
            ws.Cells[row, 1].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
            ws.Cells[row, 1].Style.Fill.PatternType = ExcelFillStyle.Solid;
            ws.Cells[row, 1].Style.Fill.BackgroundColor.SetColor(Color.FromArgb(20, 20, 55));
            ws.Cells[row, 1].Style.Font.Color.SetColor(Color.White);
            row++;

            // Headers
            ws.Cells[row, 1].Value = "No";
            ws.Cells[row, 2].Value = "Ad Soyad";
            ws.Cells[row, 3].Value = "Alt Birim";
            ws.Cells[row, 4].Value = "Tip";
            for (int d = 0; d < days.Count; d++)
            {
                ws.Cells[row, d + 5].Value = days[d].Day;
                ws.Cells[row, d + 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                bool isWeekend = days[d].DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
                ws.Cells[row, d + 5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Cells[row, d + 5].Style.Fill.BackgroundColor.SetColor(isWeekend ? Color.FromArgb(60, 20, 20) : Color.FromArgb(25, 25, 50));
                ws.Cells[row, d + 5].Style.Font.Color.SetColor(isWeekend ? Color.FromArgb(239, 83, 80) : Color.FromArgb(100, 130, 200));
            }
            ws.Cells[row, days.Count + 5].Value = "Toplam";
            ws.Cells[row, days.Count + 6].Value = "Saat";
            StyleHeaderRow(ws.Cells[row, 1, row, days.Count + 6]);
            row++;

            // Day name row
            ws.Cells[row, 1].Value = "";
            ws.Cells[row, 2].Value = "";
            ws.Cells[row, 3].Value = "";
            ws.Cells[row, 4].Value = "";
            for (int d = 0; d < days.Count; d++)
            {
                ws.Cells[row, d + 5].Value = days[d].ToString("ddd");
                ws.Cells[row, d + 5].Style.Font.Size = 8;
                ws.Cells[row, d + 5].Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                ws.Cells[row, d + 5].Style.Font.Color.SetColor(Color.FromArgb(100, 100, 140));
            }
            row++;

            var entryDict = entries.GroupBy(e => e.NurseId).ToDictionary(g => g.Key, g => g.ToDictionary(e => e.EntryDate.Date));
            var shiftDict = shifts.ToDictionary(s => s.Id);
            int no = 1;

            foreach (var nurse in nurses.Where(n => entryDict.ContainsKey(n.Id)))
            {
                ws.Cells[row, 1].Value = no++;
                ws.Cells[row, 2].Value = nurse.FullName + (nurse.IsHeadNurse ? " ★" : "");
                ws.Cells[row, 2].Style.Font.Bold = nurse.IsHeadNurse;
                ws.Cells[row, 3].Value = nurse.SubUnitName;
                ws.Cells[row, 4].Value = nurse.EmploymentType == "FULL" ? "Tam" : "Yarı";

                int total = 0; double totalHours = 0;
                for (int d = 0; d < days.Count; d++)
                {
                    var cell = ws.Cells[row, d + 5];
                    if (entryDict[nurse.Id].TryGetValue(days[d].Date, out var entry))
                    {
                        if (entry.IsLeave) { cell.Value = "İ"; SetCellColor(cell, Color.FromArgb(20, 60, 20), Color.FromArgb(100, 200, 100)); }
                        else if (entry.ShiftId.HasValue && shiftDict.TryGetValue(entry.ShiftId.Value, out var shift))
                        {
                            cell.Value = shift.ShortCode;
                            SetCellColor(cell, HexToColor(shift.ColorHex), Color.White);
                            if (entry.IsHeadNurseDay) cell.Style.Font.Bold = true;
                            total++; totalHours += shift.DurationHours;
                        }
                    }
                    cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
                }
                ws.Cells[row, days.Count + 5].Value = total;
                ws.Cells[row, days.Count + 5].Style.Font.Bold = true;
                ws.Cells[row, days.Count + 5].Style.Font.Color.SetColor(Color.FromArgb(0, 188, 212));
                ws.Cells[row, days.Count + 6].Value = totalHours;
                ws.Cells[row, days.Count + 6].Style.Font.Color.SetColor(Color.FromArgb(255, 179, 0));

                ws.Row(row).Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Row(row).Style.Fill.BackgroundColor.SetColor(row % 2 == 0 ? Color.FromArgb(15, 15, 30) : Color.FromArgb(22, 22, 40));
                row++;
            }

            ws.Column(1).Width = 5; ws.Column(2).Width = 22; ws.Column(3).Width = 14; ws.Column(4).Width = 6;
            for (int d = 0; d < days.Count; d++) ws.Column(d + 5).Width = 4.2;
            ws.Column(days.Count + 5).Width = 9; ws.Column(days.Count + 6).Width = 8;
            ws.View.FreezePanes(4, 5);
        }

        private static void CreateIstatistiklerSheet(ExcelPackage pkg, Schedule schedule,
            List<ScheduleEntry> entries, List<Nurse> nurses, List<ShiftDefinition> shifts,
            List<DateTime> days, List<ScheduleRule> rules)
        {
            var ws = pkg.Workbook.Worksheets.Add("İstatistikler");
            int row = 1;

            ws.Cells[row, 1, row, 8].Merge = true;
            ws.Cells[row, 1].Value = "📊 Çizelge İstatistikleri";
            StyleTitleCell(ws.Cells[row, 1], 14);
            row++;

            // General stats
            var headers = new[] { "Ad Soyad", "Alt Birim", "BH", "Toplam Vardiya", "Toplam Saat", "H.İçi", "H.Sonu", "Gece", "Max Art Arka" };
            for (int c = 0; c < headers.Length; c++) { ws.Cells[row, c + 1].Value = headers[c]; ws.Cells[row, c + 1].Style.Font.Bold = true; ws.Cells[row, c + 1].Style.Font.Color.SetColor(Color.FromArgb(0, 188, 212)); }
            row++;

            var entryD = entries.GroupBy(e => e.NurseId).ToDictionary(g => g.Key, g => g.ToDictionary(e => e.EntryDate.Date));
            var shiftD = shifts.ToDictionary(s => s.Id);
            var weekendSet = new HashSet<DateTime>(days.Where(d => d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday));

            foreach (var nurse in nurses.Where(n => entryD.ContainsKey(n.Id)))
            {
                int total = 0, weekday = 0, weekend = 0, night = 0, consecutive = 0, maxCons = 0;
                double totalH = 0;
                var nd = entryD[nurse.Id];
                foreach (var day in days)
                {
                    if (!nd.TryGetValue(day.Date, out var e) || e.IsLeave || !e.ShiftId.HasValue) { consecutive = 0; continue; }
                    if (!shiftD.TryGetValue(e.ShiftId.Value, out var shift)) continue;
                    total++; totalH += shift.DurationHours;
                    if (weekendSet.Contains(day)) weekend++; else weekday++;
                    if (shift.IsNightShift) night++;
                    consecutive++;
                    if (consecutive > maxCons) maxCons = consecutive;
                }
                ws.Cells[row, 1].Value = nurse.FullName;
                ws.Cells[row, 2].Value = nurse.SubUnitName;
                ws.Cells[row, 3].Value = nurse.IsHeadNurse ? "✓" : "";
                ws.Cells[row, 4].Value = total;
                ws.Cells[row, 5].Value = totalH;
                ws.Cells[row, 6].Value = weekday;
                ws.Cells[row, 7].Value = weekend;
                ws.Cells[row, 8].Value = night;
                ws.Cells[row, 9].Value = maxCons;
                ws.Row(row).Style.Fill.PatternType = ExcelFillStyle.Solid;
                ws.Row(row).Style.Fill.BackgroundColor.SetColor(row % 2 == 0 ? Color.FromArgb(15, 15, 30) : Color.FromArgb(22, 22, 40));
                ws.Row(row).Style.Font.Color.SetColor(Color.FromArgb(200, 200, 220));
                row++;
            }

            for (int c = 1; c <= 9; c++) ws.Column(c).AutoFit();
        }

        private static void StyleHeaderRow(ExcelRange range)
        {
            range.Style.Fill.PatternType = ExcelFillStyle.Solid;
            range.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(25, 25, 55));
            range.Style.Font.Bold = true;
            range.Style.Font.Color.SetColor(Color.FromArgb(180, 180, 220));
            range.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }

        private static void StyleTitleCell(ExcelRange cell, int size)
        {
            cell.Style.Font.Bold = true; cell.Style.Font.Size = size;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(20, 20, 55));
            cell.Style.Font.Color.SetColor(Color.White);
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Left;
        }

        private static void SetCellColor(ExcelRange cell, Color bg, Color fg)
        {
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(bg);
            cell.Style.Font.Color.SetColor(fg);
        }

        private static Color HexToColor(string hex)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(hex)) return Color.FromArgb(30, 30, 60);
                hex = hex.Trim().TrimStart('#');

                if (hex.Length == 3) // RGB -> RRGGBB
                    hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}";
                
                if (hex.Length == 4) // ARGB -> AARRGGBB
                    hex = $"{hex[0]}{hex[0]}{hex[1]}{hex[1]}{hex[2]}{hex[2]}{hex[3]}{hex[3]}";

                if (hex.Length == 8) // AARRGGBB
                {
                    return Color.FromArgb(
                        int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                        int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                        int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber),
                        int.Parse(hex.Substring(6, 2), System.Globalization.NumberStyles.HexNumber));
                }
                
                if (hex.Length == 6) // RRGGBB
                {
                    return Color.FromArgb(
                        int.Parse(hex.Substring(0, 2), System.Globalization.NumberStyles.HexNumber),
                        int.Parse(hex.Substring(2, 2), System.Globalization.NumberStyles.HexNumber),
                        int.Parse(hex.Substring(4, 2), System.Globalization.NumberStyles.HexNumber));
                }
                
                return Color.FromArgb(61, 90, 254);
            }
            catch { return Color.FromArgb(61, 90, 254); }
        }

        private static string SanitizeName(string name)
            => string.Concat(name.Select(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' ? c : '_')).Trim('_');
    }
}
