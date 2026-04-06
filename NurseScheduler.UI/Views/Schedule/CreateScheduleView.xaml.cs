using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;
namespace NurseScheduler.UI.Views.Schedule
{
    public partial class CreateScheduleView : Page
    {
        public CreateScheduleView() { InitializeComponent(); DataContext = new CreateScheduleViewModel(); }
    }
}
