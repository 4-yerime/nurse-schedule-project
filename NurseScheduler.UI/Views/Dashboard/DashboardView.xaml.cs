using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;

namespace NurseScheduler.UI.Views.Dashboard
{
    public partial class DashboardView : Page
    {
        public DashboardView()
        {
            InitializeComponent();
            DataContext = new DashboardViewModel();
        }
    }
}
