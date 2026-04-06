using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;
namespace NurseScheduler.UI.Views.Shifts
{
    public partial class ShiftsView : Page
    {
        public ShiftsView() { InitializeComponent(); DataContext = new ShiftsViewModel(); }
    }
}
