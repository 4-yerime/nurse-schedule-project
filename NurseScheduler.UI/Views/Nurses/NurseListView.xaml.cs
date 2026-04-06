using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;
namespace NurseScheduler.UI.Views.Nurses
{
    public partial class NurseListView : Page
    {
        public NurseListView() { InitializeComponent(); DataContext = new NurseListViewModel(); }
    }
}
