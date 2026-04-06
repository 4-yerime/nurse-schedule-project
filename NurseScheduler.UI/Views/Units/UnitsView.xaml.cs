using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;

namespace NurseScheduler.UI.Views.Units
{
    public partial class UnitsView : Page
    {
        public UnitsView() { InitializeComponent(); DataContext = new UnitsViewModel(); }
    }
}
