using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class ScheduleDisplayViewModel : BaseViewModel
    {
        public ObservableCollection<Schedule> Schedules { get; } = new();
        public ObservableCollection<NurseRow> NurseRows { get; } = new();
        public ObservableCollection<ColumnHeader> DayHeaders { get; } = new();

        private Schedule? _selectedSchedule;
        private bool _hasEntries;

        public Schedule? SelectedSchedule
        {
            get => _selectedSchedule;
            set { SetProperty(ref _selectedSchedule, value); if (value != null) LoadEntries(value.Id); }
        }

        public bool HasEntries { get => _hasEntries; set => SetProperty(ref _hasEntries, value); }

        public RelayCommand RefreshCommand { get; }

        public ScheduleDisplayViewModel()
        {
            RefreshCommand = new RelayCommand(LoadSchedules);
            LoadSchedules();
        }

        private void LoadSchedules()
        {
            Schedules.Clear();
            foreach (var s in App.Database.GetAllSchedules().Where(s => s.Status == "GENERATED"))
                Schedules.Add(s);
            if (Schedules.Count > 0) SelectedSchedule = Schedules[0];
        }

        private void LoadEntries(int scheduleId)
        {
            NurseRows.Clear();
            DayHeaders.Clear();

            var entries = App.Database.GetScheduleEntries(scheduleId);
            var nurses = App.Database.GetAllNurses();

            if (!entries.Any()) { HasEntries = false; return; }
            HasEntries = true;

            var schedule = _selectedSchedule!;
            var days = new List<DateTime>();
            for (var d = schedule.StartDate; d <= schedule.EndDate; d = d.AddDays(1))
                days.Add(d);

            // Build headers
            foreach (var d in days)
                DayHeaders.Add(new ColumnHeader
                {
                    Date = d,
                    DayNum = d.Day.ToString(),
                    DayName = d.ToString("ddd"),
                    IsWeekend = d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday
                });

            // Group entries by nurse
            var grouped = entries.GroupBy(e => e.NurseId).ToDictionary(g => g.Key, g => g.ToDictionary(e => e.EntryDate.Date));

            foreach (var nurse in nurses)
            {
                if (!grouped.ContainsKey(nurse.Id)) continue;
                var row = new NurseRow { NurseName = nurse.FullName, SubUnitName = nurse.SubUnitName, IsHeadNurse = nurse.IsHeadNurse };

                int totalShifts = 0, weekendShifts = 0;
                foreach (var d in days)
                {
                    if (grouped[nurse.Id].TryGetValue(d.Date, out var entry))
                    {
                        var cell = new CellValue
                        {
                            ShiftCode = entry.IsLeave ? "İ" : (string.IsNullOrEmpty(entry.ShiftName) ? "-" : (entry.ShiftName.Length >= 2 ? entry.ShiftName.Substring(0, 2).ToUpper() : entry.ShiftName.ToUpper())),
                            Background = entry.IsLeave ? "#1A3A1A" : (string.IsNullOrEmpty(entry.ShiftColorHex) ? "#1A1A2E" : HexToBackground(entry.ShiftColorHex)),
                            IsHeadNurseDay = entry.IsHeadNurseDay
                        };
                        row.Cells.Add(cell);
                        if (!entry.IsLeave && entry.ShiftId.HasValue) { totalShifts++; if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) weekendShifts++; }
                    }
                    else row.Cells.Add(new CellValue { ShiftCode = "-", Background = "#0A0A15" });
                }
                row.TotalShifts = totalShifts;
                row.WeekendShifts = weekendShifts;
                NurseRows.Add(row);
            }
        }

        private static string HexToBackground(string hex)
        {
            // Darken for dark theme
            return hex.Length > 0 ? hex : "#1A1A2E";
        }
    }

    public class ColumnHeader
    {
        public DateTime Date { get; set; }
        public string DayNum { get; set; } = "";
        public string DayName { get; set; } = "";
        public bool IsWeekend { get; set; }
        public string Background => IsWeekend ? "#2A1A1A" : "#1A1A2E";
    }

    public class NurseRow
    {
        public string NurseName { get; set; } = "";
        public string SubUnitName { get; set; } = "";
        public bool IsHeadNurse { get; set; }
        public int TotalShifts { get; set; }
        public int WeekendShifts { get; set; }
        public List<CellValue> Cells { get; } = new();
    }

    public class CellValue
    {
        public string ShiftCode { get; set; } = "-";
        public string Background { get; set; } = "#1A1A2E";
        public bool IsHeadNurseDay { get; set; }
    }
}
