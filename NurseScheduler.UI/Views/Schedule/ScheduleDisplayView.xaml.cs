using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;
namespace NurseScheduler.UI.Views.Schedule
{
    public partial class ScheduleDisplayView : Page
    {
        public ScheduleDisplayView() { InitializeComponent(); DataContext = new ScheduleDisplayViewModel(); }
    }
}
