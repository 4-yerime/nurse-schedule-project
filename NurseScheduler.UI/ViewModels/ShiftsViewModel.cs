using System;
using System.Collections.ObjectModel;
using System.Windows;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class ShiftsViewModel : BaseViewModel
    {
        public ObservableCollection<ShiftDefinition> Shifts { get; } = new();

        private ShiftDefinition? _selected;
        private string _name = "", _code = "", _startTime = "08:00", _endTime = "16:00";
        private string _colorHex = "#AED6F1";
        private bool _isNight, _isActive = true;
        private bool _isEditing;
        private double _duration;

        public ShiftDefinition? Selected { get => _selected; set { SetProperty(ref _selected, value); if (value != null) Populate(value); } }
        public bool IsEditing { get => _isEditing; set => SetProperty(ref _isEditing, value); }
        public string Name { get => _name; set => SetProperty(ref _name, value); }
        public string Code { get => _code; set => SetProperty(ref _code, value); }
        public string StartTime { get => _startTime; set { SetProperty(ref _startTime, value); CalcDuration(); } }
        public string EndTime { get => _endTime; set { SetProperty(ref _endTime, value); CalcDuration(); } }
        public string ColorHex { get => _colorHex; set => SetProperty(ref _colorHex, value); }
        public bool IsNight { get => _isNight; set => SetProperty(ref _isNight, value); }
        public bool IsActive { get => _isActive; set => SetProperty(ref _isActive, value); }
        public double Duration { get => _duration; set => SetProperty(ref _duration, value); }

        public RelayCommand NewCommand { get; }
        public RelayCommand SaveCommand { get; }
        public RelayCommand DeleteCommand { get; }
        public RelayCommand CancelCommand { get; }

        public ShiftsViewModel()
        {
            NewCommand = new RelayCommand(New);
            SaveCommand = new RelayCommand(Save, () => !string.IsNullOrWhiteSpace(Name));
            DeleteCommand = new RelayCommand(Delete, () => Selected != null);
            CancelCommand = new RelayCommand(() => { IsEditing = false; Load(); });
            Load();
        }

        private void Load()
        {
            Shifts.Clear();
            foreach (var s in App.Database.GetAllShiftsIncludeInactive())
                Shifts.Add(s);
        }

        private void New()
        {
            Selected = null;
            Name = ""; Code = ""; StartTime = "08:00"; EndTime = "16:00";
            ColorHex = "#AED6F1"; IsNight = false; IsActive = true;
            IsEditing = true;
            CalcDuration();
        }

        private void Save()
        {
            CalcDuration();
            if (string.IsNullOrWhiteSpace(Code)) Code = Guid.NewGuid().ToString("N").Substring(0, 8);
            
            if (_selected == null || _selected.Id == 0)
                App.Database.AddShift(new ShiftDefinition { Name = Name, ShortCode = Code, StartTime = StartTime, EndTime = EndTime, DurationHours = Duration, ColorHex = ColorHex, IsNightShift = IsNight, IsActive = IsActive });
            else
            {
                _selected.Name = Name; _selected.ShortCode = Code; _selected.StartTime = StartTime;
                _selected.EndTime = EndTime; _selected.DurationHours = Duration;
                _selected.ColorHex = ColorHex; _selected.IsNightShift = IsNight; _selected.IsActive = IsActive;
                App.Database.UpdateShift(_selected);
            }
            IsEditing = false;
            New(); // Reset for next entry
            Load();
        }

        private void Delete()
        {
            if (_selected == null) return;
            if (MessageBox.Show($"'{_selected.Name}' vardiyasını silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            App.Database.DeleteShift(_selected.Id);
            Load();
        }

        private void Populate(ShiftDefinition s) { Name = s.Name; Code = s.ShortCode; StartTime = s.StartTime; EndTime = s.EndTime; ColorHex = s.ColorHex; IsNight = s.IsNightShift; IsActive = s.IsActive; Duration = s.DurationHours; IsEditing = true; }

        private void CalcDuration()
        {
            if (TimeSpan.TryParse(StartTime, out var st) && TimeSpan.TryParse(EndTime, out var et))
            {
                var diff = et - st;
                Duration = diff.TotalHours <= 0 ? diff.TotalHours + 24 : diff.TotalHours;
            }
        }
    }
}
