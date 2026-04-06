using System.Windows;
using OfficeOpenXml;

namespace NurseScheduler.UI
{
    public partial class App : Application
    {
        public static Services.DatabaseService Database { get; private set; } = null!;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // UI Thread exceptions
            this.DispatcherUnhandledException += (s, ev) =>
            {
                MessageBox.Show($"UI Hatası:\n{ev.Exception.Message}", "Kritik Sistem Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                ev.Handled = true;
            };

            // Background thread exceptions
            System.AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                if (ev.ExceptionObject is System.Exception ex)
                {
                    MessageBox.Show($"Arka Plan Hatası:\n{ex.Message}", "Kritik Sistem Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            };

            // Task execution exceptions
            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (s, ev) =>
            {
                MessageBox.Show($"Görev Hatası:\n{ev.Exception.Message}", "Kritik Sistem Hatası", MessageBoxButton.OK, MessageBoxImage.Error);
                ev.SetObserved();
            };

            Database = new Services.DatabaseService();
            ExcelPackage.LicenseContext = LicenseContext.NonCommercial;
        }
    }
}
