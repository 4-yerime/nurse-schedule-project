using System.Windows;
using NurseScheduler.UI.ViewModels;
using NurseScheduler.UI.Views.Dashboard;
using NurseScheduler.UI.Views.Nurses;
using NurseScheduler.UI.Views.Units;
using NurseScheduler.UI.Views.Shifts;
using NurseScheduler.UI.Views.Rules;
using NurseScheduler.UI.Views.Schedule;
using NurseScheduler.UI.Views.Reports;
using NurseScheduler.UI.Views.Settings;

namespace NurseScheduler.UI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            DataContext = new MainWindowViewModel(NavigateTo);
            NavigateTo("Dashboard");
        }

        public void NavigateTo(string page)
        {
            ContentFrame.Content = page switch
            {
                "Dashboard"      => new DashboardView(),
                "Nurses"         => new NurseListView(),
                "Units"          => new UnitsView(),
                "Shifts"         => new ShiftsView(),
                "Rules"          => new RulesView(),
                "CreateSchedule" => new CreateScheduleView(),
                "ViewSchedule"   => new ScheduleDisplayView(),
                "Reports"        => new ReportsView(),
                "Settings"       => new SettingsView(),
                _                => new DashboardView()
            };
        }
    }
}