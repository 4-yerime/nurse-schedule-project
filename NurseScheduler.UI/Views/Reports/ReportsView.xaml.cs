using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;
namespace NurseScheduler.UI.Views.Reports
{
    public partial class ReportsView : Page
    {
        public ReportsView() { InitializeComponent(); DataContext = new ReportsViewModel(); }
    }
}
