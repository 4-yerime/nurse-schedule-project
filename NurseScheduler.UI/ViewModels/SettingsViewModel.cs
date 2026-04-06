using System;
using System.IO;
using System.Windows;
using NurseScheduler.UI.Helpers;

namespace NurseScheduler.UI.ViewModels
{
    public class SettingsViewModel : BaseViewModel
    {
        private string _hospitalName = "";
        private string _defaultMode = "BALANCED";
        private string _dbVersion = "1.0";
        private string _dbPath = "";

        public string HospitalName { get => _hospitalName; set => SetProperty(ref _hospitalName, value); }
        public string DefaultMode { get => _defaultMode; set => SetProperty(ref _defaultMode, value); }
        public string DbVersion { get => _dbVersion; set => SetProperty(ref _dbVersion, value); }
        public string DbPath { get => _dbPath; set => SetProperty(ref _dbPath, value); }

        public RelayCommand SaveCommand { get; }
        public RelayCommand BackupCommand { get; }
        public RelayCommand RestoreCommand { get; }

        public SettingsViewModel()
        {
            SaveCommand = new RelayCommand(Save);
            BackupCommand = new RelayCommand(Backup);
            RestoreCommand = new RelayCommand(Restore);
            Load();
        }

        private void Load()
        {
            HospitalName = App.Database.GetSetting("HospitalName", "Hastane");
            DefaultMode = App.Database.GetSetting("AlgorithmDefaultMode", "BALANCED");
            DbVersion = App.Database.GetSetting("DatabaseVersion", "1.0");
            DbPath = App.Database.GetDatabasePath();
        }

        private void Save()
        {
            App.Database.SetSetting("HospitalName", HospitalName);
            App.Database.SetSetting("AlgorithmDefaultMode", DefaultMode);
            // Update window title
            if (Application.Current.MainWindow is MainWindow mw)
                mw.DataContext = new MainWindowViewModel(mw.NavigateTo);
            MessageBox.Show("Ayarlar kaydedildi.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void Backup()
        {
            try
            {
                var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);
                var backupPath = Path.Combine(desktop, $"nursescheduler_backup_{DateTime.Now:yyyyMMdd_HHmm}.db");
                File.Copy(DbPath, backupPath, true);
                MessageBox.Show($"Yedek alındı:\n{backupPath}", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Yedekleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Restore()
        {
            var dlg = new Microsoft.Win32.OpenFileDialog { Filter = "SQLite DB (*.db)|*.db", Title = "Yedek Dosyası Seç" };
            if (dlg.ShowDialog() != true) return;
            if (MessageBox.Show("Mevcut veriler silinecek ve yedeğe dönülecek. Emin misiniz?", "Uyarı", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                File.Copy(dlg.FileName, DbPath, true);
                MessageBox.Show("Geri yükleme başarılı. Uygulamayı yeniden başlatın.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Geri yükleme hatası: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}
