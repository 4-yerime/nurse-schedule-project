using System;
using System.Collections.Generic;
using System.Text;

namespace NurseScheduler.UI.ViewModels
{
    public class MainWindowViewModel : BaseViewModel
    {
        private string _title = "NurseScheduler Pro - Ana Sayfa";
        public string Title
        {
            get => _title;
            set
            {
                if (_title != value)
                {
                    _title = value;
                    OnPropertyChanged();
                }
            }
        }
    }
}