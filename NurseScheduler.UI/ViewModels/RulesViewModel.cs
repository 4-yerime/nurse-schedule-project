using System.Collections.ObjectModel;
using System.Windows;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class RulesViewModel : BaseViewModel
    {
        public ObservableCollection<ScheduleRule> Rules { get; } = new();

        private ScheduleRule? _selected;
        private string _name = "", _desc = "", _params = "{}";
        private string _category = "SOFT";
        private int _penalty = 5;
        private bool _active = true;
        private bool _isEditing;

        public ScheduleRule? Selected { get => _selected; set { SetProperty(ref _selected, value); if (value != null) Populate(value); OnPropertyChanged(nameof(CanDelete)); } }
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }
        public string RuleName { get => _name; set => SetProperty(ref _name, value); }
        public string RuleDesc { get => _desc; set => SetProperty(ref _desc, value); }
        public string Params { get => _params; set => SetProperty(ref _params, value); }
        public string Category { get => _category; set { if (SetProperty(ref _category, value)) { if (value == "HARD") Penalty = 10; } } }
        public int Penalty { get => _penalty; set { var v = value > 10 ? 10 : value; if (SetProperty(ref _penalty, v)) { if (v < 10) Category = "SOFT"; else Category = "HARD"; } } }
        public bool Active { get => _active; set => SetProperty(ref _active, value); }
        public bool CanDelete => _selected != null && !_selected.IsSystemRule;

        public RelayCommand NewCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand CancelCommand { get; }
        public RelayCommand<ScheduleRule> ToggleActiveCommand { get; }
        public RelayCommand<ScheduleRule> EditCommand { get; }

        public RulesViewModel()
        {
            NewCommand = new RelayCommand(New);
            SaveCommand = new RelayCommand(Save, () => !string.IsNullOrWhiteSpace(RuleName));
            DeleteCommand = new RelayCommand(Delete, () => CanDelete);
            CancelCommand = new RelayCommand(() => { IsEditing = false; Load(); });
            ToggleActiveCommand = new RelayCommand<ScheduleRule>(Toggle);
            EditCommand = new RelayCommand<ScheduleRule>(r => { Selected = r; });
            Load();
        }

        private void Load()
        {
            Rules.Clear();
            foreach (var r in App.Database.GetAllRules())
                Rules.Add(r);
        }

        private void New()
        {
            Selected = null;
            RuleName = ""; RuleDesc = ""; Category = "SOFT"; Penalty = 5; Params = "{}"; Active = true;
            IsEditing = true;
        }

        private void Save()
        {
            if (_selected == null || _selected.Id == 0)
            {
                App.Database.AddRule(new ScheduleRule
                {
                    RuleCode = RuleName.ToUpper().Replace(" ", "_"),
                    Name = RuleName, Description = RuleDesc, Category = Category,
                    PenaltyScore = Penalty, IsActive = Active, Parameters = Params
                });
            }
            else
            {
                _selected.Name = RuleName; _selected.Description = RuleDesc;
                _selected.Category = Category; _selected.PenaltyScore = Penalty;
                _selected.IsActive = Active; _selected.Parameters = Params;
                App.Database.UpdateRule(_selected);
            }
            IsEditing = false;
            Load();
        }

        private void Delete()
        {
            if (_selected == null || _selected.IsSystemRule) return;
            if (MessageBox.Show($"'{_selected.Name}' kuralını silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            App.Database.DeleteRule(_selected.Id);
            Load();
        }

        private void Toggle(ScheduleRule? rule)
        {
            if (rule == null) return;
            App.Database.UpdateRule(rule);
        }

        private void Populate(ScheduleRule r) { RuleName = r.Name; RuleDesc = r.Description ?? ""; Category = r.Category; Penalty = r.PenaltyScore; Params = r.Parameters; Active = r.IsActive; IsEditing = true; }
    }
}
