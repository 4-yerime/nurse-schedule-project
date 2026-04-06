using System;
using NurseScheduler.UI.Helpers;

namespace NurseScheduler.UI.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private readonly Action<string> _navigate;
        private string _hospitalName = "Hastane";
        private string _windowTitle = "NurseScheduler Pro";

        public string HospitalName
        {
            get => _hospitalName;
            set => SetProperty(ref _hospitalName, value);
        }

        public string WindowTitle
        {
            get => _windowTitle;
            set => SetProperty(ref _windowTitle, value);
        }

        public RelayCommand NavigateCommand { get; }

        public MainWindowViewModel(Action<string> navigate)
        {
            _navigate = navigate;
            NavigateCommand = new RelayCommand(p => _navigate(p?.ToString() ?? "Dashboard"));
            LoadSettings();
        }

        private void LoadSettings()
        {
            var db = App.Database;
            HospitalName = db.GetSetting("HospitalName", "Hastane Adı Giriniz");
            WindowTitle = $"NurseScheduler Pro — {HospitalName}";
        }
    }
}