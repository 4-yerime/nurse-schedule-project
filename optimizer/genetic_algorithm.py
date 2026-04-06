#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Genetic Algorithm Implementation for Nurse Scheduling
Based on three academic papers:
1. Arslan & Özcan (2022) - BAUN
2. Bayraktar & Aytaç-Adalı (2022) - ÖHÜ
3. Kaya (2022) - UMAGD
"""

import random
import time
import json
from datetime import datetime, timedelta
from copy import deepcopy
from typing import List, Dict, Tuple, Optional, Callable
from fitness_calculator import FitnessCalculator


class GeneticAlgorithm:
    def __init__(self, data: dict, log_fn: Callable, progress_fn: Callable):
        self.data = data
        self.log = log_fn
        self.progress = progress_fn

        self.nurses = data['nurses']
        self.shifts = data['shifts']
        self.sub_units = data['subUnits']
        self.rules = data['rules']
        self.weekend_dates = set(data.get('weekendDates', []))

        # Parse dates
        start = datetime.strptime(data['startDate'], '%Y-%m-%d')
        end = datetime.strptime(data['endDate'], '%Y-%m-%d')
        self.days = []
        d = start
        while d <= end:
            self.days.append(d.strftime('%Y-%m-%d'))
            d += timedelta(days=1)

        self.n_nurses = len(self.nurses)
        self.n_days = len(self.days)
        self.shift_ids = [s.get('id') or s.get('Id') for s in self.shifts] + [0]  # 0 = day off

        # Build leave day set for fast lookup
        self.leave_set = set()
        for n in self.nurses:
            for ld in n.get('leaveDates', []):
                self.leave_set.add((n['id'], ld))

        # Algorithm params
        params = data.get('algorithmParams', {})
        self.pop_size = params.get('populationSize', 100)
        self.max_gen = params.get('maxGenerations', 300)
        self.crossover_rate = params.get('crossoverRate', 0.8)
        self.mutation_rate = params.get('mutationRate', 0.02)
        self.elitism_count = params.get('elitismCount', 5)
        self.tournament_size = params.get('tournamentSize', 3)
        self.stagnation_limit = params.get('stagnationLimit', 50)

        self.fitness_calc = FitnessCalculator(
            self.nurses, self.shifts, self.sub_units, self.rules,
            self.days, self.weekend_dates, self.leave_set
        )

    def create_individual(self) -> List[List[int]]:
        """Create a random but feasible individual (N nurses × D days matrix)."""
        individual = []
        for i, nurse in enumerate(self.nurses):
            row = []
            for day in self.days:
                # Respect leave days
                if (nurse['id'], day) in self.leave_set:
                    row.append(0)
                else:
                    # 65% chance of getting a shift
                    if random.random() < 0.65:
                        row.append(random.choice([s['id'] for s in self.shifts]))
                    else:
                        row.append(0)
            individual.append(row)
        return individual

    def tournament_select(self, population: list, fitnesses: list) -> list:
        """Tournament selection."""
        candidates = random.sample(range(len(population)), min(self.tournament_size, len(population)))
        best = max(candidates, key=lambda i: fitnesses[i])
        return deepcopy(population[best])

    def crossover(self, p1: list, p2: list) -> Tuple[list, list]:
        """Single-point crossover at nurse level."""
        if random.random() > self.crossover_rate:
            return deepcopy(p1), deepcopy(p2)
        point = random.randint(1, self.n_nurses - 1)
        c1 = p1[:point] + p2[point:]
        c2 = p2[:point] + p1[point:]
        return c1, c2

    def mutate(self, individual: list) -> list:
        """Uniform mutation: randomly change shift assignments."""
        for i, nurse in enumerate(self.nurses):
            for j, day in enumerate(self.days):
                if random.random() < self.mutation_rate:
                    if (nurse['id'], day) in self.leave_set:
                        individual[i][j] = 0
                    elif random.random() < 0.4:
                        individual[i][j] = 0  # day off
                    else:
                        individual[i][j] = random.choice([s['id'] for s in self.shifts])
        return individual

    def run(self) -> dict:
        """Main GA loop."""
        start_time = time.time()
        random.seed(42)

        self.log("🧬 Başlangıç popülasyonu oluşturuluyor...")
        population = [self.create_individual() for _ in range(self.pop_size)]
        fitnesses = [self.fitness_calc.calculate(ind) for ind in population]

        best_idx = max(range(len(fitnesses)), key=lambda i: fitnesses[i])
        best_individual = deepcopy(population[best_idx])
        best_fitness = fitnesses[best_idx]

        self.log(f"📊 Başlangıç en iyi fitness: {best_fitness:.2f}")
        self.progress(0, best_fitness, self.fitness_calc.count_violations(best_individual))

        stagnation = 0

        for gen in range(1, self.max_gen + 1):
            # Sort population by fitness
            sorted_indices = sorted(range(len(fitnesses)), key=lambda i: fitnesses[i], reverse=True)

            # Check improvement
            if fitnesses[sorted_indices[0]] > best_fitness:
                best_fitness = fitnesses[sorted_indices[0]]
                best_individual = deepcopy(population[sorted_indices[0]])
                stagnation = 0
                violations = self.fitness_calc.count_violations(best_individual)
                self.log(f"✨ YENİ EN İYİ BULUNAN! Nesil {gen} | Fitness: {best_fitness:.2f} | İhlal: {violations}")
            else:
                stagnation += 1

            if stagnation >= self.stagnation_limit:
                self.log(f"⏹️  Durma kriteri: {self.stagnation_limit} nesil iyileşme yok.")
                break

            # New generation
            new_pop = []

            # Elitism
            for i in range(min(self.elitism_count, len(sorted_indices))):
                new_pop.append(deepcopy(population[sorted_indices[i]]))

            # Fill rest with crossover + mutation
            while len(new_pop) < self.pop_size:
                p1 = self.tournament_select(population, fitnesses)
                p2 = self.tournament_select(population, fitnesses)
                c1, c2 = self.crossover(p1, p2)
                c1 = self.mutate(c1)
                c2 = self.mutate(c2)
                new_pop.append(c1)
                if len(new_pop) < self.pop_size:
                    new_pop.append(c2)

            population = new_pop
            fitnesses = [self.fitness_calc.calculate(ind) for ind in population]

            # Log every 10 generations
            if gen % 10 == 0 or gen <= 5:
                violations = self.fitness_calc.count_violations(best_individual)
                pct = gen / self.max_gen * 100
                self.log(f"[Nesil {gen:04d}] Fitness: {best_fitness:.2f} | İhlal: {violations} | Durgunluk: {stagnation}")
                self.progress(gen, best_fitness, violations)

        exec_ms = int((time.time() - start_time) * 1000)
        violations = self.fitness_calc.count_violations(best_individual)
        self.log(f"✅ Algoritma tamamlandı. Toplam süre: {exec_ms}ms | Son fitness: {best_fitness:.2f}")
        self.progress(gen, best_fitness, violations)

        return self._build_result(best_individual, gen, exec_ms, best_fitness, violations)

    def _build_result(self, individual: list, total_gen: int, exec_ms: int,
                      fitness: float, total_violations: int) -> dict:
        entries = []
        for i, nurse in enumerate(self.nurses):
            for j, day in enumerate(self.days):
                shift_id = individual[i][j]
                is_leave = (nurse['id'], day) in self.leave_set
                is_head_day = shift_id != 0 and nurse.get('isHeadNurse', False)
                entries.append({
                    'nurseId': nurse['id'],
                    'date': day,
                    'shiftId': shift_id if shift_id != 0 else None,
                    'isLeave': is_leave,
                    'isHeadNurseDay': is_head_day
                })

        # Per-nurse statistics
        shift_map = {s['id']: s for s in self.shifts}
        per_nurse = []
        for i, nurse in enumerate(self.nurses):
            total_shifts = sum(1 for s in individual[i] if s != 0)
            total_hours = sum(shift_map[s]['durationHours'] for s in individual[i] if s != 0 and s in shift_map)
            weekend_shifts = sum(1 for j, s in enumerate(individual[i]) if s != 0 and self.days[j] in self.weekend_dates)
            night_shifts = sum(1 for s in individual[i] if s != 0 and shift_map.get(s, {}).get('isNightShift', False))
            per_nurse.append({
                'nurseId': nurse['id'],
                'nurseName': f"{nurse['firstName']} {nurse['lastName']}",
                'totalShifts': total_shifts,
                'totalHours': total_hours,
                'weekendShifts': weekend_shifts,
                'nightShifts': night_shifts,
                'dayShifts': total_shifts - night_shifts,
            })

        avg_shifts = sum(s['totalShifts'] for s in per_nurse) / max(len(per_nurse), 1)

        return {
            'scheduleId': self.data['scheduleId'],
            'status': 'SUCCESS',
            'fitnessScore': fitness,
            'totalGenerations': total_gen,
            'executionTimeMs': exec_ms,
            'totalViolations': total_violations,
            'hardViolations': self.fitness_calc.count_hard_violations(individual),
            'softViolations': max(0, total_violations - self.fitness_calc.count_hard_violations(individual)),
            'entries': entries,
            'violationDetails': [],
            'statistics': {
                'perNurse': per_nurse,
                'overall': {
                    'totalEntries': len(entries),
                    'avgShiftsPerNurse': round(avg_shifts, 1),
                    'minShifts': min(s['totalShifts'] for s in per_nurse) if per_nurse else 0,
                    'maxShifts': max(s['totalShifts'] for s in per_nurse) if per_nurse else 0,
                }
            }
        }
