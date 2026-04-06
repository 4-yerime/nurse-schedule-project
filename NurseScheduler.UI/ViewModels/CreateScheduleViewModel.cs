using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class CreateScheduleViewModel : BaseViewModel
    {
        public ObservableCollection<SubUnitSelection> SubUnits { get; } = new();

        private int _step = 1;
        private string _scheduleName = "";
        private DateTime _startDate = new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        private DateTime _endDate;
        private string _algorithmMode = "BALANCED";
        private string _notes = "";
        private bool _isGenerating;

        public int Step { get => _step; set { SetProperty(ref _step, value); OnPropertyChanged(nameof(IsStep1)); OnPropertyChanged(nameof(IsStep2)); OnPropertyChanged(nameof(IsStep3)); } }
        public bool IsStep1 => _step == 1;
        public bool IsStep2 => _step == 2;
        public bool IsStep3 => _step == 3;

        public string ScheduleName { get => _scheduleName; set => SetProperty(ref _scheduleName, value); }
        public DateTime StartDate 
        { 
            get => _startDate; 
            set 
            { 
                if (SetProperty(ref _startDate, value))
                {
                    AutoNameSchedule();
                    // Automatically set end date to end of the month
                    EndDate = new DateTime(_startDate.Year, _startDate.Month, DateTime.DaysInMonth(_startDate.Year, _startDate.Month));
                }
            } 
        }
        public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
        public string AlgorithmMode { get => _algorithmMode; set { SetProperty(ref _algorithmMode, value); OnPropertyChanged(nameof(ModeDescription)); } }
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
        public bool IsGenerating { get => _isGenerating; set => SetProperty(ref _isGenerating, value); }

        public string ModeDescription => _algorithmMode switch
        {
            "FAST" => "Popülasyon: 50 | Nesil: 100 | Tahmini süre: 5-15 saniye",
            "BALANCED" => "Popülasyon: 100 | Nesil: 300 | Tahmini süre: 30-60 saniye",
            "QUALITY" => "Popülasyon: 200 | Nesil: 500 | Tahmini süre: 2-5 dakika",
            _ => ""
        };

        public int SelectedNurseCount => SubUnits.SelectMany(s => s.Nurses).Count(n => n.IsIncluded);
        public int SelectedSubUnitCount => SubUnits.Count(s => s.IsIncluded);
        public int ActiveRuleCount => App.Database.GetActiveRules().Count;

        public RelayCommand NextCommand { get; }
        public RelayCommand BackCommand { get; }
        public RelayCommand GenerateCommand { get; }

        public CreateScheduleViewModel()
        {
            NextCommand = new RelayCommand(Next);
            BackCommand = new RelayCommand(() => { if (_step > 1) Step--; });
            GenerateCommand = new RelayCommand(Generate, () => !_isGenerating);

            AutoNameSchedule();
            _endDate = new DateTime(_startDate.Year, _startDate.Month, DateTime.DaysInMonth(_startDate.Year, _startDate.Month));
            LoadSubUnits();
        }

        private void AutoNameSchedule()
        {
            ScheduleName = _startDate.ToString("MMMM yyyy") + " Çizelgesi";
        }

        private void LoadSubUnits()
        {
            SubUnits.Clear();
            foreach (var su in App.Database.GetAllSubUnits())
            {
                var item = new SubUnitSelection(su);
                foreach (var n in App.Database.GetNursesBySubUnit(su.Id))
                    item.Nurses.Add(new NurseSelection(n));
                SubUnits.Add(item);
            }
        }

        private void Next()
        {
            if (_step == 1 && string.IsNullOrWhiteSpace(ScheduleName)) return;
            if (_step < 3) { Step++; if (_step == 3) Refresh(); }
        }

        private void Refresh()
        {
            OnPropertyChanged(nameof(SelectedNurseCount));
            OnPropertyChanged(nameof(SelectedSubUnitCount));
            OnPropertyChanged(nameof(ActiveRuleCount));
        }

        private async void Generate()
        {
            IsGenerating = true;
            try
            {
                if (EndDate < StartDate)
                {
                    MessageBox.Show("Bitiş tarihi başlangıç tarihinden önce olamaz.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                if (SelectedNurseCount == 0)
                {
                    MessageBox.Show("Lütfen en az bir hemşire seçildiğinden emin olun.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var schedule = new Schedule
                {
                    Name = ScheduleName,
                    StartDate = StartDate,
                    EndDate = EndDate,
                    AlgorithmMode = AlgorithmMode,
                    Notes = Notes,
                    Status = "DRAFT"
                };
                int scheduleId = App.Database.AddSchedule(schedule);

                var progressVm = new AlgorithmProgressViewModel(scheduleId, BuildInput(scheduleId));
                var progressWindow = new Views.Schedule.AlgorithmProgressWindow(progressVm);
                progressWindow.ShowDialog();
            }
            finally
            {
                IsGenerating = false;
            }
        }

        private AlgorithmInput BuildInput(int scheduleId)
        {
            var nurses = SubUnits
                .Where(su => su.IsIncluded)
                .SelectMany(su => su.Nurses)
                .Where(n => n.IsIncluded)
                .Select(n => n.Nurse)
                .ToList();

            return new AlgorithmInput
            {
                ScheduleId = scheduleId,
                ScheduleName = ScheduleName,
                StartDate = StartDate.ToString("yyyy-MM-dd"),
                EndDate = EndDate.ToString("yyyy-MM-dd"),
                AlgorithmMode = AlgorithmMode,
                Shifts = App.Database.GetAllShifts(),
                SubUnits = SubUnits.Where(su => su.IsIncluded).Select(su => su.SubUnit).ToList(),
                Nurses = nurses,
                Rules = App.Database.GetActiveRules(),
                WeekendDates = GetWeekendDates(StartDate, EndDate),
                NurseLeaveDates = nurses.ToDictionary(
                    n => n.Id,
                    n => App.Database.GetLeavesByNurse(n.Id).Select(l => l.LeaveDate.ToString("yyyy-MM-dd")).ToList()
                )
            };
        }

        private static List<string> GetWeekendDates(DateTime start, DateTime end)
        {
            var dates = new List<string>();
            for (var d = start; d <= end; d = d.AddDays(1))
                if (d.DayOfWeek == DayOfWeek.Saturday || d.DayOfWeek == DayOfWeek.Sunday)
                    dates.Add(d.ToString("yyyy-MM-dd"));
            return dates;
        }
    }

    public class SubUnitSelection : BaseViewModel
    {
        public SubUnit SubUnit { get; }
        public ObservableCollection<NurseSelection> Nurses { get; } = new();
        private bool _isIncluded = true;

        public bool IsIncluded { get => _isIncluded; set => SetProperty(ref _isIncluded, value); }
        public string DisplayName => $"{SubUnit.UnitName} / {SubUnit.Name}";

        public SubUnitSelection(SubUnit su) { SubUnit = su; }
    }

    public class NurseSelection : BaseViewModel
    {
        public Nurse Nurse { get; }
        private bool _isIncluded = true;
        public bool IsIncluded { get => _isIncluded; set => SetProperty(ref _isIncluded, value); }
        public string DisplayName => Nurse.FullName;
        public NurseSelection(Nurse n) { Nurse = n; }
    }

    public class AlgorithmInput
    {
        public int ScheduleId { get; set; }
        public string ScheduleName { get; set; } = "";
        public string StartDate { get; set; } = "";
        public string EndDate { get; set; } = "";
        public string AlgorithmMode { get; set; } = "BALANCED";
        public List<ShiftDefinition> Shifts { get; set; } = new();
        public List<SubUnit> SubUnits { get; set; } = new();
        public List<Nurse> Nurses { get; set; } = new();
        public List<ScheduleRule> Rules { get; set; } = new();
        public List<string> WeekendDates { get; set; } = new();
        public Dictionary<int, List<string>> NurseLeaveDates { get; set; } = new();
    }
}
