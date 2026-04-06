using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using Newtonsoft.Json;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class NurseListViewModel : BaseViewModel
    {
        public ObservableCollection<Nurse> Nurses { get; } = new();
        public ObservableCollection<Unit> Units { get; } = new();
        public ObservableCollection<SubUnit> SubUnits { get; } = new();
        public ObservableCollection<ShiftDefinition> AllShifts { get; } = new();
        public ObservableCollection<ShiftDefinition> PreferredShifts { get; } = new();
        public ObservableCollection<NurseLeave> Leaves { get; } = new();

        private Nurse? _selected;
        private Unit? _selectedUnit;
        private SubUnit? _selectedSubUnit;
        private string _searchText = "";
        private bool _isEditing;

        // Form fields
        private string _firstName = "", _lastName = "";
        private bool _isHead;
        private string _empType = "FULL";
        private double _maxHours = 160;
        private int _leaveBalance = 14;
        private string _notes = "";
        private bool _nurseActive = true;

        // New leave fields
        private DateTime _newLeaveDate = DateTime.Today;
        private string _newLeaveType = "PERSONAL";
        private string _newLeaveReason = "";

        public Nurse? Selected { get => _selected; set { SetProperty(ref _selected, value); if (value != null) PopulateForm(value); SelectedChanged(); } }
        public Unit? SelectedUnit { get => _selectedUnit; set { SetProperty(ref _selectedUnit, value); LoadSubUnits(); } }
        public SubUnit? SelectedSubUnit { get => _selectedSubUnit; set => SetProperty(ref _selectedSubUnit, value); }
        public string SearchText { get => _searchText; set { SetProperty(ref _searchText, value); FilterNurses(); } }
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }

        public string FirstName { get => _firstName; set => SetProperty(ref _firstName, value); }
        public string LastName { get => _lastName; set => SetProperty(ref _lastName, value); }
        
        public bool IsHead { get => _isHead; set => SetProperty(ref _isHead, value); }
        public string EmpType { get => _empType; set => SetProperty(ref _empType, value); }
        public double MaxHours { get => _maxHours; set => SetProperty(ref _maxHours, value); }
        public int LeaveBalance { get => _leaveBalance; set => SetProperty(ref _leaveBalance, value); }
        public string Notes { get => _notes; set => SetProperty(ref _notes, value); }
        public bool NurseActive { get => _nurseActive; set => SetProperty(ref _nurseActive, value); }
        public DateTime NewLeaveDate { get => _newLeaveDate; set => SetProperty(ref _newLeaveDate, value); }
        public string NewLeaveType { get => _newLeaveType; set => SetProperty(ref _newLeaveType, value); }
        public string NewLeaveReason { get => _newLeaveReason; set => SetProperty(ref _newLeaveReason, value); }
        public bool NurseSelected => _selected != null;

        public RelayCommand NewCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand AddLeaveCommand { get; }
        public RelayCommand<NurseLeave> DeleteLeaveCommand { get; }

        private ObservableCollection<Nurse> _allNurses = new();

        public NurseListViewModel()
        {
            NewCommand = new RelayCommand(New);
            SaveCommand = new RelayCommand(Save, () => !string.IsNullOrWhiteSpace(FirstName) && !string.IsNullOrWhiteSpace(LastName) && (SelectedSubUnit != null || SelectedUnit != null));
            DeleteCommand = new RelayCommand(Delete, () => Selected != null);
            CancelCommand = new RelayCommand(() => { IsEditing = false; });
            AddLeaveCommand = new RelayCommand(AddLeave, () => Selected != null);
            DeleteLeaveCommand = new RelayCommand<NurseLeave>(DeleteLeave);
            LoadUnits();
            LoadAllShifts();
            LoadNurses();
        }

        private void LoadUnits()
        {
            Units.Clear();
            foreach (var u in App.Database.GetAllUnits())
                Units.Add(u);
        }

        private void LoadSubUnits()
        {
            SubUnits.Clear();
            if (_selectedUnit == null) return;
            foreach (var s in App.Database.GetSubUnitsByUnit(_selectedUnit.Id))
                SubUnits.Add(s);
        }

        private void LoadAllShifts()
        {
            AllShifts.Clear();
            foreach (var s in App.Database.GetAllShifts())
                AllShifts.Add(s);
        }

        private void LoadNurses()
        {
            _allNurses.Clear();
            Nurses.Clear();
            foreach (var n in App.Database.GetAllNurses())
            {
                _allNurses.Add(n);
                Nurses.Add(n);
            }
        }

        private void FilterNurses()
        {
            Nurses.Clear();
            var lower = _searchText.ToLower();
            foreach (var n in _allNurses)
                if (string.IsNullOrWhiteSpace(lower) || n.FullName.ToLower().Contains(lower) || n.UnitName.ToLower().Contains(lower) || n.SubUnitName.ToLower().Contains(lower))
                    Nurses.Add(n);
        }

        private void SelectedChanged()
        {
            OnPropertyChanged(nameof(NurseSelected));
            LoadLeaves();
        }

        private void LoadLeaves()
        {
            Leaves.Clear();
            if (_selected == null) return;
            foreach (var l in App.Database.GetLeavesByNurse(_selected.Id))
                Leaves.Add(l);
        }

        private void New()
        {
            Selected = null;
            FirstName = ""; LastName = ""; IsHead = false;
            EmpType = "FULL"; MaxHours = 160; LeaveBalance = 14; Notes = "";
            NurseActive = true; PreferredShifts.Clear();
            SelectedUnit = null; SelectedSubUnit = null;
            IsEditing = true;
        }

        private void Save()
        {
            if (SelectedSubUnit == null && SelectedUnit == null) return;
            
            int targetSubUnitId = 0;
            if (SelectedSubUnit != null)
            {
                targetSubUnitId = SelectedSubUnit.Id;
            }
            else if (SelectedUnit != null)
            {
                // Find or create "Genel" sub-unit for this unit
                var subUnits = App.Database.GetSubUnitsByUnit(SelectedUnit.Id);
                var generalSub = subUnits.FirstOrDefault(s => s.Name.Equals("Genel", StringComparison.OrdinalIgnoreCase)) ?? subUnits.FirstOrDefault();
                
                if (generalSub == null)
                {
                    // If no sub-unit exists at all for this unit, create a "Genel" one
                    App.Database.AddSubUnit(new SubUnit
                    {
                        UnitId = SelectedUnit.Id,
                        Name = "Genel",
                        Description = "Otomatik oluşturulan varsayılan alt birim",
                        IsActive = true
                    });
                    generalSub = App.Database.GetSubUnitsByUnit(SelectedUnit.Id).FirstOrDefault();
                }
                
                if (generalSub != null) targetSubUnitId = generalSub.Id;
            }

            if (targetSubUnitId == 0)
            {
                MessageBox.Show("Lütfen geçerli bir birim veya alt birim seçin.", "Hata", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            var prefIds = PreferredShifts.Select(s => s.Id).ToList();
            var prefJson = JsonConvert.SerializeObject(prefIds);

            if (_selected == null || _selected.Id == 0)
            {
                App.Database.AddNurse(new Nurse
                {
                    FirstName = FirstName, LastName = LastName,
                    SubUnitId = targetSubUnitId, IsHeadNurse = IsHead, EmploymentType = EmpType,
                    MaxMonthlyHours = MaxHours, AnnualLeaveBalance = LeaveBalance, Notes = Notes,
                    PreferredShiftIds = prefJson, IsActive = NurseActive
                });
            }
            else
            {
                _selected.FirstName = FirstName; _selected.LastName = LastName;
                _selected.SubUnitId = targetSubUnitId; _selected.IsHeadNurse = IsHead;
                _selected.EmploymentType = EmpType; _selected.MaxMonthlyHours = MaxHours;
                _selected.AnnualLeaveBalance = LeaveBalance; _selected.Notes = Notes;
                _selected.PreferredShiftIds = prefJson; _selected.IsActive = NurseActive;
                App.Database.UpdateNurse(_selected);
            }
            IsEditing = false;
            LoadNurses();
        }

        private void Delete()
        {
            if (_selected == null) return;
            if (MessageBox.Show($"'{_selected.FullName}' isimli hemşireyi ve ona ait tüm izin kayıtlarını TAMAMEN SİLMEK istediğinize emin misiniz?", "Kalıcı Silme Onayı", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            App.Database.DeleteNurse(_selected.Id);
            LoadNurses();
        }

        private void AddLeave()
        {
            if (_selected == null) return;
            App.Database.AddLeave(new NurseLeave { NurseId = _selected.Id, LeaveDate = NewLeaveDate, LeaveType = NewLeaveType, Reason = NewLeaveReason });
            NewLeaveReason = "";
            LoadLeaves();
        }

        private void DeleteLeave(NurseLeave? leave)
        {
            if (leave == null) return;
            App.Database.DeleteLeave(leave.Id);
            LoadLeaves();
        }

        private void PopulateForm(Nurse n)
        {
            FirstName = n.FirstName; LastName = n.LastName;
            IsHead = n.IsHeadNurse; EmpType = n.EmploymentType; MaxHours = n.MaxMonthlyHours;
            LeaveBalance = n.AnnualLeaveBalance; Notes = n.Notes ?? ""; NurseActive = n.IsActive;

            // Load preferred shifts
            PreferredShifts.Clear();
            try
            {
                var ids = JsonConvert.DeserializeObject<List<int>>(n.PreferredShiftIds) ?? new();
                foreach (var s in AllShifts.Where(s => ids.Contains(s.Id)))
                    PreferredShifts.Add(s);
            }
            catch { }

            // Set unit/subunit
            var su = App.Database.GetAllSubUnits().FirstOrDefault(s => s.Id == n.SubUnitId);
            if (su != null)
            {
                SelectedUnit = Units.FirstOrDefault(u => u.Id == su.UnitId);
                SelectedSubUnit = SubUnits.FirstOrDefault(s => s.Id == n.SubUnitId);
            }
            IsEditing = true;
        }
    }
}
