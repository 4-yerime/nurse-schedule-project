using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;
namespace NurseScheduler.UI.Views.Settings
{
    public partial class SettingsView : Page
    {
        public SettingsView() { InitializeComponent(); DataContext = new SettingsViewModel(); }
    }
}
