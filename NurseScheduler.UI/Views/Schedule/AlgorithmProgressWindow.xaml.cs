using System.Windows;
using NurseScheduler.UI.ViewModels;

namespace NurseScheduler.UI.Views.Schedule
{
    public partial class AlgorithmProgressWindow : Window
    {
        public AlgorithmProgressWindow(AlgorithmProgressViewModel vm)
        {
            InitializeComponent();
            DataContext = vm;
            vm.OnCloseRequired = () => { Close(); };
            Closing += (s, e) => { if (!vm.IsDone) e.Cancel = true; };
        }
    }
}
