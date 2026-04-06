using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.ViewModels
{
    public class ReportsViewModel : BaseViewModel
    {
        public ObservableCollection<Schedule> Schedules { get; } = new();
        private Schedule? _selected;
        private string _lastExportPath = "";
        private bool _isExporting;

        public Schedule? Selected { get => _selected; set => SetProperty(ref _selected, value); }
        public string LastExportPath { get => _lastExportPath; set => SetProperty(ref _lastExportPath, value); }
        public bool IsExporting { get => _isExporting; set => SetProperty(ref _isExporting, value); }

        public RelayCommand RefreshCommand { get; }
        public RelayCommand<Schedule> ExportCommand { get; }
        public RelayCommand OpenFolderCommand { get; }
        public RelayCommand<Schedule> DeleteScheduleCommand { get; }

        public ReportsViewModel()
        {
            RefreshCommand = new RelayCommand(Load);
            ExportCommand = new RelayCommand<Schedule>(Export, s => s != null && !IsExporting);
            OpenFolderCommand = new RelayCommand(OpenFolder, () => !string.IsNullOrEmpty(LastExportPath));
            DeleteScheduleCommand = new RelayCommand<Schedule>(Delete, s => s != null);
            Load();
        }

        private void Load()
        {
            Schedules.Clear();
            foreach (var s in App.Database.GetAllSchedules())
                Schedules.Add(s);
        }

        private async void Export(Schedule? schedule)
        {
            if (schedule == null) return;

            var hospitalName = App.Database.GetSetting("HospitalName", "Hastane");
            var fileName = $"{hospitalName.Replace(" ", "_")}_{schedule.Name.Replace(" ", "_")}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx";

            var sfd = new Microsoft.Win32.SaveFileDialog
            {
                FileName = fileName,
                DefaultExt = ".xlsx",
                Filter = "Excel Worksheets (.xlsx)|*.xlsx"
            };

            if (sfd.ShowDialog() != true) return;

            IsExporting = true;
            try
            {
                var filePath = sfd.FileName;
                var svc = new Services.ExcelExportService();
                await System.Threading.Tasks.Task.Run(() =>
                {
                    svc.Export(schedule, hospitalName, filePath);
                });
                LastExportPath = filePath;
                MessageBox.Show($"Excel dosyası başarıyla oluşturuldu.", "Başarılı", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (System.Exception ex)
            {
                MessageBox.Show($"Hata: {ex.Message}", "Hata", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally { IsExporting = false; }
        }

        private void OpenFolder()
        {
            if (string.IsNullOrEmpty(LastExportPath)) return;
            var dir = Path.GetDirectoryName(LastExportPath);
            if (dir != null && Directory.Exists(dir))
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"\"{dir}\"",
                    UseShellExecute = true
                });
            }
        }

        private void Delete(Schedule? schedule)
        {
            if (schedule == null) return;
            if (MessageBox.Show($"'{schedule.Name}' çizelgesini ve tüm kayıtlarını silmek istediğinize emin misiniz?", "Onay", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            App.Database.DeleteSchedule(schedule.Id);
            Load();
        }
    }
}
