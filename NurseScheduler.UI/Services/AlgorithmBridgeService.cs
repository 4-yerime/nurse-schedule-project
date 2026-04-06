using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using NurseScheduler.UI.Models;
using NurseScheduler.UI.ViewModels;

namespace NurseScheduler.UI.Services
{
    public class AlgorithmBridgeService
    {
        private string GetOptimizerPath()
        {
            // Look for optimizer.exe alongside the main app
            var appDir = AppDomain.CurrentDomain.BaseDirectory;
            var candidates = new[]
            {
                Path.Combine(appDir, "optimizer.exe"),
                Path.Combine(appDir, "optimizer", "optimizer.exe"),
            };
            foreach (var c in candidates)
                if (File.Exists(c)) return c;
            return ""; // Will fallback to embedded Python
        }

        public async Task RunAsync(
            AlgorithmInput input,
            CancellationToken ct,
            Action<double, int, double, int> onProgress,
            Action<string, string> onLog,
            Action<AlgorithmResult> onResult)
        {
            var jsonInput = BuildJson(input);
            var optimizerPath = GetOptimizerPath();
            bool isPython = false;

            if (string.IsNullOrEmpty(optimizerPath))
            {
                optimizerPath = FindPython();
                if (string.IsNullOrEmpty(optimizerPath))
                {
                    await RunBuiltinOptimizerAsync(input, ct, onProgress, onLog, onResult);
                    return;
                }
                isPython = true;
            }

            if (isPython || optimizerPath.ToLower().EndsWith("python.exe") || optimizerPath.ToLower() == "python" || optimizerPath.ToLower() == "python3" || optimizerPath.ToLower() == "py")
            {
                await EnsureDependenciesAsync(optimizerPath, onLog);
            }

            var utf8WithoutBOM = new UTF8Encoding(false);
            var psi = new ProcessStartInfo
            {
                FileName = optimizerPath,
                RedirectStandardInput = true, RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8, 
                StandardInputEncoding = utf8WithoutBOM,
            };

            if (isPython)
            {
                var optDir = FindOptimizerFolder();
                var mainScript = Path.Combine(optDir, "main.py");
                psi.Arguments = $"\"{mainScript}\"";
            }

            using var process = Process.Start(psi)!;
            ct.Register(() => { try { process.Kill(); } catch { } });

            // Concurrent reading of stdout and stderr
            process.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) ParseOutputLine(e.Data, input, onProgress, onLog, onResult); };
            process.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) onLog($"HATA (Sistem): {e.Data}", "#EF5350"); };
            
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            onLog($"📦 Veri gönderiliyor ({jsonInput.Length} byte)...", "#B0BEC5");
            await process.StandardInput.WriteAsync(jsonInput);
            process.StandardInput.Close();

            await process.WaitForExitAsync(ct);
        }

        private string FindOptimizerFolder()
        {
            var current = AppDomain.CurrentDomain.BaseDirectory;
            while (!string.IsNullOrEmpty(current))
            {
                var optDir = Path.Combine(current, "optimizer");
                if (Directory.Exists(optDir)) return optDir;
                var parent = Directory.GetParent(current);
                if (parent == null || parent.FullName == current) break;
                current = parent.FullName;
            }
            // Fallback to alongside exe
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "optimizer");
        }

        private async Task EnsureDependenciesAsync(string pythonPath, Action<string, string> onLog)
        {
            try
            {
                onLog("🔍 Python bağımlılıkları kontrol ediliyor...", "#00BCD4");
                var checkPsi = new ProcessStartInfo
                {
                    FileName = pythonPath,
                    Arguments = "-c \"import deap, numpy\"",
                    UseShellExecute = false, CreateNoWindow = true, RedirectStandardError = true
                };
                using var checkProc = Process.Start(checkPsi)!;
                await checkProc.WaitForExitAsync();

                if (checkProc.ExitCode != 0)
                {
                    onLog("⚠️  Kütüphaneler eksik, internetten indiriliyor (30-60sn sürebilir)...", "#FFB300");
                    var installPsi = new ProcessStartInfo
                    {
                        FileName = pythonPath,
                        Arguments = "-m pip install deap numpy",
                        UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true
                    };
                    using var installProc = Process.Start(installPsi)!;
                    installProc.OutputDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) onLog(e.Data, "#B0BEC5"); };
                    installProc.ErrorDataReceived += (s, e) => { if (!string.IsNullOrEmpty(e.Data)) onLog(e.Data, "#B0BEC5"); };
                    installProc.BeginOutputReadLine();
                    installProc.BeginErrorReadLine();
                    await installProc.WaitForExitAsync();
                    
                    if (installProc.ExitCode == 0) onLog("✅ Bağımlılıklar başarıyla kuruldu.", "#00C853");
                    else onLog("❌ Bağımlılık kurulumu başarısız! İnternet bağlantınızı kontrol edin.", "#EF5350");
                }
                else
                {
                    onLog("✅ Gerekli kütüphaneler mevcut.", "#00C853");
                }
            }
            catch (Exception ex)
            {
                onLog($"⚠️  Bağımlılık kontrolü sırasında hata: {ex.Message}", "#FFB300");
            }
        }

        private static void ParseOutputLine(string line, AlgorithmInput input,
            Action<double, int, double, int> onProgress,
            Action<string, string> onLog,
            Action<AlgorithmResult> onResult)
        {
            if (line.StartsWith("LOG:"))
            {
                var msg = line[4..];
                var color = msg.Contains("YENİ EN İYİ") || msg.Contains("NEW BEST") ? "#00C853"
                           : msg.Contains("UYARI") || msg.Contains("WARN") ? "#FFB300"
                           : msg.Contains("HATA") || msg.Contains("ERROR") ? "#EF5350"
                           : "#B0BEC5";
                onLog(msg, color);
            }
            else if (line.StartsWith("PROGRESS:"))
            {
                try
                {
                    var parts = line[9..].Split(',');
                    if (parts.Length >= 3)
                    {
                        var gen = int.Parse(parts[0]);
                        var fitness = double.Parse(parts[1], System.Globalization.CultureInfo.InvariantCulture);
                        var violations = int.Parse(parts[2]);
                        var maxGen = input.AlgorithmMode switch { "FAST" => 100, "QUALITY" => 500, _ => 300 };
                        var pct = Math.Min(100.0, (double)gen / maxGen * 100);
                        onProgress(pct, gen, fitness, violations);
                    }
                }
                catch { }
            }
            else if (line.StartsWith("RESULT:"))
            {
                try
                {
                    var json = line[7..];
                    var result = JsonConvert.DeserializeObject<AlgorithmResult>(json);
                    if (result != null) onResult(result);
                }
                catch (Exception ex)
                {
                    onLog($"Sonuç parse hatası: {ex.Message}", "#EF5350");
                }
            }
        }

        private static string BuildJson(AlgorithmInput input)
        {
            try
            {
                var (popSize, maxGen, crossover, mutation, elitism, tournament, stagnation) = input.AlgorithmMode switch
                {
                    "FAST" => (50, 100, 0.8, 0.05, 3, 3, 20),
                    "QUALITY" => (200, 500, 0.85, 0.01, 10, 5, 100),
                    _ => (100, 300, 0.8, 0.02, 5, 3, 50)
                };

                var shiftList = input.Shifts.Select(s => new { id = s.Id, name = s.Name, shortCode = s.ShortCode, startTime = s.StartTime, endTime = s.EndTime, durationHours = s.DurationHours, isNightShift = s.IsNightShift }).ToList();
                var subunitList = input.SubUnits.Select(su => new { id = su.Id, unitId = su.UnitId, unitName = su.UnitName, name = su.Name, minNursesPerShift = su.MinNursesPerShift, maxNursesPerShift = su.MaxNursesPerShift, requiresHeadNurse = su.RequiresHeadNurse }).ToList();
                
                var nurseList = input.Nurses.Select(n => {
                    List<int> prefs;
                    try { 
                        prefs = string.IsNullOrWhiteSpace(n.PreferredShiftIds) ? new() : JsonConvert.DeserializeObject<List<int>>(n.PreferredShiftIds) ?? new(); 
                    } catch { prefs = new(); }
                    
                    return new
                    {
                        id = n.Id,
                        firstName = n.FirstName,
                        lastName = n.LastName,
                        subUnitId = n.SubUnitId,
                        isHeadNurse = n.IsHeadNurse,
                        employmentType = n.EmploymentType,
                        maxMonthlyHours = n.MaxMonthlyHours,
                        preferredShiftIds = prefs,
                        leaveDates = input.NurseLeaveDates.ContainsKey(n.Id) ? input.NurseLeaveDates[n.Id] : new()
                    };
                }).ToList();

                var ruleList = input.Rules.Select(r => {
                    object? ruleParams;
                    try { 
                        ruleParams = string.IsNullOrWhiteSpace(r.Parameters) ? new object() : JsonConvert.DeserializeObject(r.Parameters);
                    } catch { ruleParams = new object(); }
                    
                    return new { id = r.Id, ruleCode = r.RuleCode, name = r.Name, penaltyScore = r.PenaltyScore, fitnessWeight = r.FitnessWeight, isActive = r.IsActive, parameters = ruleParams };
                }).ToList();

                var obj = new
                {
                    scheduleId = input.ScheduleId,
                    scheduleName = input.ScheduleName,
                    startDate = input.StartDate,
                    endDate = input.EndDate,
                    algorithmMode = input.AlgorithmMode,
                    algorithmParams = new { populationSize = popSize, maxGenerations = maxGen, crossoverRate = crossover, mutationRate = mutation, elitismCount = elitism, tournamentSize = tournament, stagnationLimit = stagnation },
                    shifts = shiftList,
                    subUnits = subunitList,
                    nurses = nurseList,
                    rules = ruleList,
                    weekendDates = input.WeekendDates,
                    previousMonthLastDayNightShiftNurseIds = new int[] { }
                };
                return JsonConvert.SerializeObject(obj, Formatting.None);
            }
            catch (Exception ex)
            {
                throw new Exception($"JSON oluşturma hatası: {ex.Message}");
            }
        }

        private static string FindPython()
        {
            foreach (var candidate in new[] { "python", "python3", "py" })
                try
                {
                    var p = Process.Start(new ProcessStartInfo { FileName = candidate, Arguments = "--version", UseShellExecute = false, CreateNoWindow = true });
                    p?.WaitForExit(2000);
                    if (p?.ExitCode == 0) return candidate;
                }
                catch { }
            return "";
        }

        // ===== BUILT-IN C# FALLBACK OPTIMIZER =====
        private async Task RunBuiltinOptimizerAsync(AlgorithmInput input, CancellationToken ct,
            Action<double, int, double, int> onProgress, Action<string, string> onLog, Action<AlgorithmResult> onResult)
        {
            onLog("Python bulunamadı. Dahili C# optimizasyonu çalıştırılıyor...", "#FFB300");
            await Task.Run(() =>
            {
                var optimizer = new BuiltinGeneticAlgorithm(input);
                optimizer.Run(ct, onProgress, onLog, onResult);
            }, ct);
        }
    }
}
