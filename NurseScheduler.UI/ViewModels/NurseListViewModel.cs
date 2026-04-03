using System.Collections.ObjectModel;
using NurseScheduler.UI.Models;
using NurseScheduler.UI.Services;

namespace NurseScheduler.UI.ViewModels
{
    public class NurseListViewModel : BaseViewModel
    {
        private ObservableCollection<Nurse>? _nurses;
        public ObservableCollection<Nurse> Nurses
        {
            get => _nurses ??= new ObservableCollection<Nurse>();
            set
            {
                _nurses = value;
                OnPropertyChanged();
            }
        }

        private readonly DatabaseService _databaseService;

        public NurseListViewModel()
        {
            string dbPath = System.IO.Path.Combine(System.AppDomain.CurrentDomain.BaseDirectory, "nurses.db");
            _databaseService = new DatabaseService(dbPath);

            LoadNursesFromDatabase();
        }

        private void LoadNursesFromDatabase()
        {
            var nurseList = _databaseService.GetAllNurses();

            if (nurseList.Count == 0)
            {
                var sampleNurses = new[]
                {
                    new Nurse { FirstName="Ayşe", LastName="Yılmaz", IsHeadNurse=true, SubUnitId=1, EmploymentType="FULL", MaxMonthlyHours=160, AnnualLeaveBalance=14 },
                    new Nurse { FirstName="Fatma", LastName="Kara", IsHeadNurse=false, SubUnitId=1, EmploymentType="PART", MaxMonthlyHours=80, AnnualLeaveBalance=7 },
                    new Nurse { FirstName="Mehmet", LastName="Demir", IsHeadNurse=false, SubUnitId=2, EmploymentType="FULL", MaxMonthlyHours=160, AnnualLeaveBalance=10 }
                };

                foreach (var nurse in sampleNurses)
                {
                    _databaseService.AddNurse(nurse);
                }

                nurseList = _databaseService.GetAllNurses();
            }

            Nurses = new ObservableCollection<Nurse>(nurseList);
        }
    }
}