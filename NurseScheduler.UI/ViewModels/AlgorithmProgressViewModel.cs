using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json;
using NurseScheduler.UI.Helpers;
using NurseScheduler.UI.Models;
using NurseScheduler.UI.ViewModels;

namespace NurseScheduler.UI.ViewModels
{
    public class LogEntry
    {
        public string Time { get; set; } = "";
        public string Message { get; set; } = "";
        public string Color { get; set; } = "#FFFFFF";
    }

    public class AlgorithmProgressViewModel : BaseViewModel
    {
        private readonly int _scheduleId;
        private readonly AlgorithmInput _input;
        private CancellationTokenSource _cts = new();

        private int _generation;
        private int _maxGenerations;
        private double _bestFitness;
        private int _violations;
        private string _elapsedTime = "00:00:00";
        private double _progress;
        private bool _isDone;
        private bool _isCancelled;
        private string _statusMessage = "Algoritma başlatılıyor...";

        public ObservableCollection<LogEntry> Logs { get; } = new();

        public int Generation { get => _generation; set => SetProperty(ref _generation, value); }
        public int MaxGenerations { get => _maxGenerations; set => SetProperty(ref _maxGenerations, value); }
        public double BestFitness { get => _bestFitness; set => SetProperty(ref _bestFitness, value); }
        public int Violations { get => _violations; set => SetProperty(ref _violations, value); }
        public string ElapsedTime { get => _elapsedTime; set => SetProperty(ref _elapsedTime, value); }
        public double Progress { get => _progress; set => SetProperty(ref _progress, value); }
        public bool IsDone { get => _isDone; set => SetProperty(ref _isDone, value); }
        public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

        public RelayCommand CancelCommand { get; }
        public RelayCommand ViewScheduleCommand { get; }
        public Action? OnCloseRequired { get; set; }

        public AlgorithmProgressViewModel(int scheduleId, AlgorithmInput input)
        {
            _scheduleId = scheduleId;
            _input = input;
            MaxGenerations = input.AlgorithmMode switch { "FAST" => 100, "QUALITY" => 500, _ => 300 };

            CancelCommand = new RelayCommand(Cancel, () => !_isDone);
            ViewScheduleCommand = new RelayCommand(ViewSchedule, () => _isDone && !_isCancelled);

            _ = RunAsync();
        }

        private async Task RunAsync()
        {
            AddLog("🚀 Algoritma motoru hazırlanıyor...", "#00BCD4");
            var sw = Stopwatch.StartNew();
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (_, _) => { ElapsedTime = sw.Elapsed.ToString(@"hh\:mm\:ss"); };
            timer.Start();

            try
            {
                var bridge = new Services.AlgorithmBridgeService();
                await bridge.RunAsync(_input, _cts.Token, OnProgress, OnLog, OnResult);
            }
            catch (OperationCanceledException)
            {
                _isCancelled = true;
                AddLog("İşlem kullanıcı tarafından iptal edildi.", "#EF5350");
            }
            catch (Exception ex)
            {
                AddLog($"KRİTİK HATA: {ex.Message}", "#EF5350");
                if (ex.InnerException != null) AddLog($"Detay: {ex.InnerException.Message}", "#EF5350");
            }
            finally
            {
                timer.Stop();
                IsDone = true;
                StatusMessage = _isCancelled ? "İptal Edildi" : "Tamamlandı!";
            }
        }

        private void OnProgress(double pct, int gen, double fitness, int violations)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            
            dispatcher.Invoke(() =>
            {
                Progress = pct;
                Generation = gen;
                BestFitness = fitness;
                Violations = violations;
            });
        }

        private void OnLog(string message, string color)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;

            dispatcher.Invoke(() =>
            {
                Logs.Add(new LogEntry { Time = DateTime.Now.ToString("HH:mm:ss"), Message = message, Color = color });
            });
        }

        private void OnResult(AlgorithmResult result)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                App.Database.UpdateScheduleResult(_scheduleId, result.FitnessScore, result.TotalGenerations, result.ExecutionTimeMs, result.TotalViolations);
                var entries = new System.Collections.Generic.List<ScheduleEntry>();
                foreach (var e in result.Entries)
                    entries.Add(new ScheduleEntry { ScheduleId = _scheduleId, NurseId = e.NurseId, EntryDate = DateTime.Parse(e.Date), ShiftId = e.ShiftId, IsLeave = e.IsLeave, IsHeadNurseDay = e.IsHeadNurseDay });
                App.Database.SaveScheduleEntries(_scheduleId, entries);
                StatusMessage = $"Tamamlandı! Fitness: {result.FitnessScore:F1} | İhlal: {result.TotalViolations}";
                AddLog($"✅ Çizelge başarıyla oluşturuldu. Fitness: {result.FitnessScore:F1}", "#00C853");
            });
        }

        private void AddLog(string msg, string color = "#FFFFFF") => OnLog(msg, color);
        private void Cancel() { _cts.Cancel(); }
        private void ViewSchedule()
        {
            if (Application.Current.MainWindow is MainWindow mw)
                mw.NavigateTo("ViewSchedule");
            OnCloseRequired?.Invoke();
        }
    }
}
