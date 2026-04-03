using System;
using System.Windows.Input;
using NurseScheduler.UI.Models;
using NurseScheduler.UI.Services;
using NurseScheduler.UI.Helpers;

namespace NurseScheduler.UI.ViewModels
{
    public class NurseEditViewModel : BaseViewModel
    {
        private readonly DatabaseService _databaseService;

        private Nurse _currentNurse;
        public Nurse CurrentNurse
        {
            get => _currentNurse;
            set
            {
                _currentNurse = value;
                OnPropertyChanged();
            }
        }

        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }

        // Hemşire düzenleme ekranını kapatmak için event
        public event Action? RequestClose;

        public NurseEditViewModel(DatabaseService databaseService)
        {
            _databaseService = databaseService;

            CurrentNurse = new Nurse();

            SaveCommand = new RelayCommand(SaveNurse, CanSave);
            CancelCommand = new RelayCommand(CancelEdit);
        }

        private bool CanSave(object? parameter)
        {
            return !string.IsNullOrWhiteSpace(CurrentNurse.FirstName) && !string.IsNullOrWhiteSpace(CurrentNurse.LastName);
        }

        private void SaveNurse(object? parameter)
        {
            _databaseService.AddNurse(CurrentNurse);
            RequestClose?.Invoke();
        }

        private void CancelEdit(object? parameter)
        {
            RequestClose?.Invoke();
        }
    }
}