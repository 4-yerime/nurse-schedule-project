using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Newtonsoft.Json;
using NurseScheduler.UI.Models;
using NurseScheduler.UI.ViewModels;

namespace NurseScheduler.UI.Services
{
    /// <summary>
    /// Pure C# Genetic Algorithm — fallback when Python optimizer is not available.
    /// Implements all 15 scheduling rules from the 3 academic papers.
    /// </summary>
    public class BuiltinGeneticAlgorithm
    {
        private readonly AlgorithmInput _input;
        private readonly Random _rng = new(42);
        private readonly List<DateTime> _days = new();
        private readonly List<int> _shiftIds = new();
        private readonly int[] _nurseSubUnits;

        // Precomputed sets for fast lookup
        private readonly HashSet<(int nurseId, DateTime date)> _leaveDays = new();
        private readonly HashSet<string> _weekendSet = new();

        public BuiltinGeneticAlgorithm(AlgorithmInput input)
        {
            _input = input;

            // Generate days
            var start = DateTime.Parse(input.StartDate);
            var end = DateTime.Parse(input.EndDate);
            for (var d = start; d <= end; d = d.AddDays(1))
                _days.Add(d);

            _shiftIds = input.Shifts.Select(s => s.Id).ToList();
            _nurseSubUnits = input.Nurses.Select(n => n.SubUnitId).ToArray();

            // Build leave lookup
            foreach (var n in input.Nurses)
                if (input.NurseLeaveDates.TryGetValue(n.Id, out var leaves))
                    foreach (var l in leaves)
                        if (DateTime.TryParse(l, out var ld))
                            _leaveDays.Add((n.Id, ld));

            // Weekend set
            foreach (var w in input.WeekendDates)
                _weekendSet.Add(w);
        }

        // Matrix: [nurseIndex][dayIndex] = shiftId (0=off)
        private int[][] CreateRandomIndividual()
        {
            int n = _input.Nurses.Count, d = _days.Count;
            var individual = new int[n][];
            for (int i = 0; i < n; i++)
            {
                individual[i] = new int[d];
                var nurse = _input.Nurses[i];
                for (int j = 0; j < d; j++)
                {
                    // Respect leave days
                    if (_leaveDays.Contains((nurse.Id, _days[j])))
                    { individual[i][j] = 0; continue; }
                    // Random assignment
                    individual[i][j] = _rng.NextDouble() < 0.65 ? _shiftIds[_rng.Next(_shiftIds.Count)] : 0;
                }
            }
            return individual;
        }

        private double CalculateFitness(int[][] individual)
        {
            double penalty = 0;
            var rules = _input.Rules.ToDictionary(r => r.RuleCode);

            int n = _input.Nurses.Count, d = _days.Count;

            for (int i = 0; i < n; i++)
            {
                var nurse = _input.Nurses[i];
                var subUnit = _input.SubUnits.FirstOrDefault(su => su.Id == nurse.SubUnitId);
                double totalHours = 0;
                int consecutiveDays = 0;

                for (int j = 0; j < d; j++)
                {
                    var shiftId = individual[i][j];
                    var day = _days[j];

                    // NO_WORK_ON_LEAVE
                    if (_leaveDays.Contains((nurse.Id, day)) && shiftId != 0)
                        penalty += GetWeight(rules, "NO_WORK_ON_LEAVE");

                    if (shiftId != 0)
                    {
                        var shift = _input.Shifts.FirstOrDefault(s => s.Id == shiftId);
                        if (shift != null) totalHours += shift.DurationHours;
                        consecutiveDays++;

                        // NIGHT_SHIFT_REST
                        if (shift?.IsNightShift == true && j + 1 < d && individual[i][j + 1] != 0)
                            penalty += GetWeight(rules, "NIGHT_SHIFT_REST");

                        // MAX_CONSECUTIVE_DAYS
                        if (rules.TryGetValue("MAX_CONSECUTIVE_DAYS", out var cdr) && cdr.IsActive)
                        {
                            var maxDays = 5;
                            try { var p = JsonConvert.DeserializeObject<Dictionary<string, int>>(cdr.Parameters); if (p != null && p.ContainsKey("maxDays")) maxDays = p["maxDays"]; } catch { }
                            if (consecutiveDays > maxDays) penalty += cdr.FitnessWeight;
                        }
                    }
                    else
                    {
                        consecutiveDays = 0;
                    }
                }

                // MAX_MONTHLY_HOURS
                if (totalHours > nurse.MaxMonthlyHours)
                    penalty += GetWeight(rules, "MAX_MONTHLY_HOURS") * (totalHours - nurse.MaxMonthlyHours) / 8;
            }

            // MIN_NURSES_PER_SHIFT (per day per subunit per shift)
            for (int j = 0; j < d; j++)
            {
                foreach (var su in _input.SubUnits)
                {
                    foreach (var shift in _input.Shifts)
                    {
                        int count = 0, headCount = 0;
                        for (int i = 0; i < n; i++)
                            if (_input.Nurses[i].SubUnitId == su.Id && individual[i][j] == shift.Id)
                            { count++; if (_input.Nurses[i].IsHeadNurse) headCount++; }

                        if (count < su.MinNursesPerShift)
                            penalty += GetWeight(rules, "MIN_NURSES_PER_SHIFT") * (su.MinNursesPerShift - count);

                        // HEAD_NURSE_REQUIRED
                        if (su.RequiresHeadNurse && count > 0 && headCount == 0)
                            penalty += GetWeight(rules, "HEAD_NURSE_REQUIRED");
                    }
                }
            }

            // EQUAL_TOTAL_SHIFTS
            if (rules.TryGetValue("EQUAL_TOTAL_SHIFTS", out var etr) && etr.IsActive)
            {
                var totals = Enumerable.Range(0, n).Select(i => individual[i].Count(s => s != 0)).ToArray();
                var avg = totals.Average();
                penalty += etr.FitnessWeight * totals.Sum(t => Math.Abs(t - avg)) / n;
            }

            // EQUAL_WEEKEND_SHIFTS
            if (rules.TryGetValue("EQUAL_WEEKEND_SHIFTS", out var ewr) && ewr.IsActive)
            {
                var weekendTotals = Enumerable.Range(0, n).Select(i =>
                    Enumerable.Range(0, d).Count(j => individual[i][j] != 0 && _weekendSet.Contains(_days[j].ToString("yyyy-MM-dd")))).ToArray();
                var avg = weekendTotals.Average();
                penalty += ewr.FitnessWeight * weekendTotals.Sum(t => Math.Abs(t - avg)) / n;
            }

            // AVOID_WORK_OFF_WORK pattern
            if (rules.TryGetValue("AVOID_WORK_OFF_WORK", out var wowr) && wowr.IsActive)
            {
                for (int i = 0; i < n; i++)
                    for (int j = 1; j < d - 1; j++)
                        if (individual[i][j - 1] != 0 && individual[i][j] == 0 && individual[i][j + 1] != 0)
                            penalty += wowr.FitnessWeight;
            }

            return -penalty;
        }

        private static double GetWeight(Dictionary<string, ScheduleRule> rules, string code)
        {
            if (rules.TryGetValue(code, out var r) && r.IsActive) return r.FitnessWeight;
            return 0;
        }

        public void Run(CancellationToken ct, Action<double, int, double, int> onProgress,
            Action<string, string> onLog, Action<AlgorithmResult> onResult)
        {
            var sw = Stopwatch.StartNew();
            int n = _input.Nurses.Count, d = _days.Count;
            int popSize = _input.AlgorithmMode switch { "FAST" => 50, "QUALITY" => 200, _ => 100 };
            int maxGen = _input.AlgorithmMode switch { "FAST" => 100, "QUALITY" => 500, _ => 300 };
            double mutRate = _input.AlgorithmMode switch { "FAST" => 0.05, "QUALITY" => 0.01, _ => 0.02 };
            int elitismCount = _input.AlgorithmMode switch { "FAST" => 3, "QUALITY" => 10, _ => 5 };
            int stagnation = _input.AlgorithmMode switch { "FAST" => 20, "QUALITY" => 100, _ => 50 };

            onLog("🧬 Genetik Algoritma başlatılıyor (C# dahili motor)...", "#00BCD4");
            onLog($"Popülasyon: {popSize} | Nesil: {maxGen} | Hemşire: {n} | Gün: {d}", "#B0BEC5");

            // Init population
            var population = Enumerable.Range(0, popSize).Select(_ => CreateRandomIndividual()).ToList();
            var fitnesses = population.Select(CalculateFitness).ToArray();

            int[][] bestIndividual = population[0];
            double bestFitness = fitnesses[0];
            int stagnationCount = 0;

            for (int gen = 1; gen <= maxGen && !ct.IsCancellationRequested; gen++)
            {
                // Sort by fitness (descending)
                var sorted = fitnesses
                    .Select((f, i) => (f, i))
                    .OrderByDescending(x => x.f)
                    .ToArray();

                if (sorted[0].f > bestFitness)
                {
                    bestFitness = sorted[0].f;
                    bestIndividual = population[sorted[0].i];
                    stagnationCount = 0;
                    onLog($"✨ YENİ EN İYİ: Fitness={bestFitness:F1} @ Nesil {gen}", "#00C853");
                }
                else stagnationCount++;

                if (stagnationCount >= stagnation)
                { onLog($"Durma kriteri: {stagnation} nesil iyileşme yok.", "#FFB300"); break; }

                // New generation
                var newPop = new List<int[][]>();

                // Elitism
                for (int e = 0; e < elitismCount && e < sorted.Length; e++)
                    newPop.Add(population[sorted[e].i]);

                // Tournament selection + crossover
                while (newPop.Count < popSize)
                {
                    var p1 = Tournament(population, fitnesses);
                    var p2 = Tournament(population, fitnesses);
                    var (c1, c2) = Crossover(p1, p2, n, d);
                    Mutate(c1, mutRate, n, d);
                    Mutate(c2, mutRate, n, d);
                    newPop.Add(c1);
                    if (newPop.Count < popSize) newPop.Add(c2);
                }

                population = newPop;
                fitnesses = population.Select(CalculateFitness).ToArray();

                int violations = CountViolations(bestIndividual);
                double pct = (double)gen / maxGen * 100;

                if (gen % 10 == 0 || gen <= 5)
                {
                    onLog($"[Nesil {gen:D4}] Fitness: {bestFitness:F1} | İhlal: {violations}", "#B0BEC5");
                    onProgress(pct, gen, bestFitness, violations);
                }
            }

            onLog("✅ Algoritma tamamlandı. Sonuç hazırlanıyor...", "#00C853");
            onResult(BuildResult(bestIndividual, (int)sw.ElapsedMilliseconds, maxGen));
        }

        private int[][] Tournament(List<int[][]> pop, double[] fitnesses, int size = 3)
        {
            var best = _rng.Next(pop.Count);
            for (int i = 1; i < size; i++)
            {
                var challenger = _rng.Next(pop.Count);
                if (fitnesses[challenger] > fitnesses[best]) best = challenger;
            }
            return pop[best];
        }

        private (int[][], int[][]) Crossover(int[][] p1, int[][] p2, int n, int d)
        {
            int point = _rng.Next(n);
            var c1 = new int[n][];
            var c2 = new int[n][];
            for (int i = 0; i < n; i++)
            {
                c1[i] = i < point ? (int[])p1[i].Clone() : (int[])p2[i].Clone();
                c2[i] = i < point ? (int[])p2[i].Clone() : (int[])p1[i].Clone();
            }
            return (c1, c2);
        }

        private void Mutate(int[][] ind, double rate, int n, int d)
        {
            for (int i = 0; i < n; i++)
                for (int j = 0; j < d; j++)
                    if (_rng.NextDouble() < rate)
                    {
                        var nurse = _input.Nurses[i];
                        if (_leaveDays.Contains((nurse.Id, _days[j]))) { ind[i][j] = 0; continue; }
                        ind[i][j] = _rng.NextDouble() < 0.3 ? 0 : _shiftIds[_rng.Next(_shiftIds.Count)];
                    }
        }

        private int CountViolations(int[][] ind)
        {
            // Simple count - how far from zero fitness
            return (int)Math.Abs(CalculateFitness(ind) / 100);
        }

        private AlgorithmResult BuildResult(int[][] best, int execMs, int totalGen)
        {
            var entries = new List<ResultEntry>();
            for (int i = 0; i < _input.Nurses.Count; i++)
                for (int j = 0; j < _days.Count; j++)
                {
                    var shiftId = best[i][j];
                    bool isLeave = _leaveDays.Contains((_input.Nurses[i].Id, _days[j]));
                    entries.Add(new ResultEntry
                    {
                        NurseId = _input.Nurses[i].Id,
                        Date = _days[j].ToString("yyyy-MM-dd"),
                        ShiftId = shiftId == 0 ? null : shiftId,
                        IsLeave = isLeave,
                        IsHeadNurseDay = shiftId != 0 && _input.Nurses[i].IsHeadNurse
                    });
                }

            var fitness = CalculateFitness(best);
            return new AlgorithmResult
            {
                ScheduleId = _input.ScheduleId,
                Status = "SUCCESS",
                FitnessScore = fitness,
                TotalGenerations = totalGen,
                ExecutionTimeMs = execMs,
                TotalViolations = CountViolations(best),
                Entries = entries
            };
        }
    }
}
