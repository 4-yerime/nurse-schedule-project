using System.Windows.Controls;
using NurseScheduler.UI.ViewModels;
namespace NurseScheduler.UI.Views.Rules
{
    public partial class RulesView : Page
    {
        public RulesView() { InitializeComponent(); DataContext = new RulesViewModel(); }
    }
}
