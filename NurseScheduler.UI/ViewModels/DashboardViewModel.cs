using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class DashboardViewModel : BaseViewModel
    {
        private int _nurseCount;
        private int _unitCount;
        private int _shiftCount;
        private int _activeRuleCount;
        private string _lastScheduleName = "—";
        private string _lastScheduleDateRange = "";
        private string _lastScheduleFitness = "—";
        private bool _hasLastSchedule;

        public int NurseCount { get => _nurseCount; set => SetProperty(ref _nurseCount, value); }
        public int UnitCount { get => _unitCount; set => SetProperty(ref _unitCount, value); }
        public int ShiftCount { get => _shiftCount; set => SetProperty(ref _shiftCount, value); }
        public int ActiveRuleCount { get => _activeRuleCount; set => SetProperty(ref _activeRuleCount, value); }
        public string LastScheduleName { get => _lastScheduleName; set => SetProperty(ref _lastScheduleName, value); }
        public string LastScheduleDateRange { get => _lastScheduleDateRange; set => SetProperty(ref _lastScheduleDateRange, value); }
        public string LastScheduleFitness { get => _lastScheduleFitness; set => SetProperty(ref _lastScheduleFitness, value); }
        public bool HasLastSchedule { get => _hasLastSchedule; set { SetProperty(ref _hasLastSchedule, value); OnPropertyChanged(nameof(NoSchedule)); } }
        public bool NoSchedule => !_hasLastSchedule;

        public RelayCommand QuickScheduleCommand { get; }
        public RelayCommand QuickNurseCommand { get; }
        public RelayCommand QuickExcelCommand { get; }

        public DashboardViewModel()
        {
            QuickScheduleCommand = new RelayCommand(() => Navigate("CreateSchedule"));
            QuickNurseCommand = new RelayCommand(() => Navigate("Nurses"));
            QuickExcelCommand = new RelayCommand(() => Navigate("Reports"));
            LoadData();
        }

        private void LoadData()
        {
            var (nurses, units, subUnits, shifts, activeRules, lastSchedule) = App.Database.GetDashboardStats();
            NurseCount = nurses;
            UnitCount = units + subUnits;
            ShiftCount = shifts;
            ActiveRuleCount = activeRules;

            if (lastSchedule != null)
            {
                HasLastSchedule = true;
                LastScheduleName = lastSchedule.Name;
                LastScheduleDateRange = $"{lastSchedule.StartDate:dd MMM yyyy} — {lastSchedule.EndDate:dd MMM yyyy}";
                LastScheduleFitness = lastSchedule.FitnessScore.HasValue
                    ? $"{lastSchedule.FitnessScore.Value:F1}"
                    : "—";
            }
        }

        private static void Navigate(string page)
        {
            if (System.Windows.Application.Current.MainWindow is MainWindow mw)
                mw.NavigateTo(page);
        }
    }
}
