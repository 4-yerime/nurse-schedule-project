using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class UnitsViewModel : BaseViewModel
    {
        public ObservableCollection<Unit> Units { get; } = new();
        public ObservableCollection<SubUnit> SubUnits { get; } = new();

        private Unit? _selectedUnit;
        private SubUnit? _selectedSubUnit;
        private bool _isEditingUnit;
        private bool _isEditingSubUnit;

        // Unit form fields
        private string _unitName = "";
        private string _unitDesc = "";
        private string _unitColor = "#3498DB";
        private bool _unitActive = true;

        // SubUnit form fields
        private string _subName = "";
        private string _subDesc = "";
        private int _subMin = 1;
        private int _subMax = 10;
        private bool _subRequiresHead;
        private bool _subActive = true;

        public Unit? SelectedUnit
        {
            get => _selectedUnit;
            set
            {
                SetProperty(ref _selectedUnit, value);
                SelectedSubUnit = null;
                IsEditingSubUnit = false;
                SubName = "";
                LoadSubUnits();
                if (value != null) PopulateUnitForm(value);
                OnPropertyChanged(nameof(UnitSelected));
            }
        }

        public SubUnit? SelectedSubUnit
        {
            get => _selectedSubUnit;
            set
            {
                SetProperty(ref _selectedSubUnit, value);
                if (value != null) PopulateSubUnitForm(value);
            }
        }

        public bool IsEditingUnit { get => _isEditingUnit; set => SetProperty(ref _isEditingUnit, value); }
        public bool IsEditingSubUnit { get => _isEditingSubUnit; set => SetProperty(ref _isEditingSubUnit, value); }
        public bool UnitSelected => _selectedUnit != null;

        public string UnitName { get => _unitName; set => SetProperty(ref _unitName, value); }
        public string UnitDesc { get => _unitDesc; set => SetProperty(ref _unitDesc, value); }
        public string UnitColor { get => _unitColor; set => SetProperty(ref _unitColor, value); }
        public bool UnitActive { get => _unitActive; set => SetProperty(ref _unitActive, value); }

        public string SubName { get => _subName; set => SetProperty(ref _subName, value); }
        public string SubDesc { get => _subDesc; set => SetProperty(ref _subDesc, value); }
        public int SubMin { get => _subMin; set => SetProperty(ref _subMin, value); }
        public int SubMax { get => _subMax; set => SetProperty(ref _subMax, value); }
        public bool SubRequiresHead { get => _subRequiresHead; set => SetProperty(ref _subRequiresHead, value); }
        public bool SubActive { get => _subActive; set => SetProperty(ref _subActive, value); }

        public RelayCommand NewUnitCommand { get; }
        public RelayCommand SaveUnitCommand { get; }
        public RelayCommand DeleteUnitCommand { get; }
        public RelayCommand CancelUnitCommand { get; }
        public RelayCommand NewSubUnitCommand { get; }
        public RelayCommand SaveSubUnitCommand { get; }
        public RelayCommand DeleteSubUnitCommand { get; }
        public RelayCommand CancelSubUnitCommand { get; }

        public UnitsViewModel()
        {
            NewUnitCommand = new RelayCommand(NewUnit);
            SaveUnitCommand = new RelayCommand(SaveUnit, () => !string.IsNullOrWhiteSpace(UnitName));
            DeleteUnitCommand = new RelayCommand(DeleteUnit, () => SelectedUnit != null);
            CancelUnitCommand = new RelayCommand(() => { IsEditingUnit = false; LoadUnits(); });
            NewSubUnitCommand = new RelayCommand(NewSubUnit, () => SelectedUnit != null);
            SaveSubUnitCommand = new RelayCommand(SaveSubUnit, () => !string.IsNullOrWhiteSpace(SubName));
            DeleteSubUnitCommand = new RelayCommand(DeleteSubUnit, () => SelectedSubUnit != null);
            CancelSubUnitCommand = new RelayCommand(() => { IsEditingSubUnit = false; });
            LoadUnits();
        }

        private void LoadUnits()
        {
            Units.Clear();
            foreach (var u in App.Database.GetAllUnitsIncludeInactive())
                Units.Add(u);
        }

        private void LoadSubUnits()
        {
            SubUnits.Clear();
            if (_selectedUnit == null) return;
            foreach (var s in App.Database.GetSubUnitsByUnit(_selectedUnit.Id))
                SubUnits.Add(s);
        }

        private void NewUnit()
        {
            SelectedUnit = null;
            UnitName = ""; UnitDesc = ""; UnitColor = "#3498DB"; UnitActive = true;
            IsEditingUnit = true;
        }

        private void SaveUnit()
        {
            if (string.IsNullOrWhiteSpace(UnitName)) return;
            if (_selectedUnit == null || _selectedUnit.Id == 0)
            {
                int unitId = App.Database.AddUnit(new Unit { Name = UnitName, Description = UnitDesc, ColorHex = UnitColor, IsActive = UnitActive });
                // Automatically add a "Genel" sub-unit for the new unit
                App.Database.AddSubUnit(new SubUnit
                {
                    UnitId = unitId,
                    Name = "Genel",
                    Description = "Otomatik oluşturulan varsayılan alt birim",
                    IsActive = true
                });
            }
            else
            {
                _selectedUnit.Name = UnitName; _selectedUnit.Description = UnitDesc;
                _selectedUnit.ColorHex = UnitColor; _selectedUnit.IsActive = UnitActive;
                App.Database.UpdateUnit(_selectedUnit);
            }
            IsEditingUnit = false;
            LoadUnits();
        }

        private void DeleteUnit()
        {
            if (_selectedUnit == null) return;
            if (MessageBox.Show($"'{_selectedUnit.Name}' birimini silmek istediğinize emin misiniz?\nBağlı tüm alt birimler de silinecek!", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            App.Database.DeleteUnit(_selectedUnit.Id);
            LoadUnits();
        }

        private void NewSubUnit()
        {
            SelectedSubUnit = null;
            SubName = ""; SubDesc = ""; SubMin = 1; SubMax = 10; SubRequiresHead = false; SubActive = true;
            IsEditingSubUnit = true;
        }

        private void SaveSubUnit()
        {
            if (_selectedUnit == null || string.IsNullOrWhiteSpace(SubName)) return;
            if (_selectedSubUnit == null || _selectedSubUnit.Id == 0)
            {
                App.Database.AddSubUnit(new SubUnit
                {
                    UnitId = _selectedUnit.Id, Name = SubName, Description = SubDesc,
                    MinNursesPerShift = SubMin, MaxNursesPerShift = SubMax,
                    RequiresHeadNurse = SubRequiresHead, IsActive = SubActive
                });
            }
            else
            {
                _selectedSubUnit.Name = SubName; _selectedSubUnit.Description = SubDesc;
                _selectedSubUnit.MinNursesPerShift = SubMin; _selectedSubUnit.MaxNursesPerShift = SubMax;
                _selectedSubUnit.RequiresHeadNurse = SubRequiresHead; _selectedSubUnit.IsActive = SubActive;
                App.Database.UpdateSubUnit(_selectedSubUnit);
            }
            IsEditingSubUnit = false;
            LoadSubUnits();
        }

        private void DeleteSubUnit()
        {
            if (_selectedSubUnit == null) return;
            if (MessageBox.Show($"'{_selectedSubUnit.Name}' alt birimini silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            App.Database.DeleteSubUnit(_selectedSubUnit.Id);
            LoadSubUnits();
        }

        private void PopulateUnitForm(Unit u) { UnitName = u.Name; UnitDesc = u.Description ?? ""; UnitColor = u.ColorHex; UnitActive = u.IsActive; IsEditingUnit = true; }
        private void PopulateSubUnitForm(SubUnit s) { SubName = s.Name; SubDesc = s.Description ?? ""; SubMin = s.MinNursesPerShift; SubMax = s.MaxNursesPerShift; SubRequiresHead = s.RequiresHeadNurse; SubActive = s.IsActive; IsEditingSubUnit = true; }
    }
}
