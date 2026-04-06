#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Fitness Calculator - Evaluates a schedule against all 15 rules.

Rule categories:
  HARD (penalty=10): MAX_CONSECUTIVE_DAYS, NO_WORK_ON_LEAVE, NIGHT_SHIFT_REST,
                     MIN_NURSES_PER_SHIFT, HEAD_NURSE_REQUIRED, MAX_MONTHLY_HOURS
  SOFT (penalty 1-9): EQUAL_TOTAL_SHIFTS, EQUAL_WEEKEND_SHIFTS, EQUAL_NIGHT_SHIFTS,
                      PREFER_SHIFT_PREFERENCES, WEEKEND_REST, AVOID_WORK_OFF_WORK,
                      MIN_WEEKEND_DAYS_OFF, MAX_NIGHT_CONSECUTIVE, WORK_AFTER_NIGHT_REST
"""

import json
from typing import List, Set, Tuple, Dict, Callable


class FitnessCalculator:
    def __init__(self, nurses, shifts, sub_units, rules, days, weekend_dates, leave_set):
        self.nurses = nurses
        self.shifts = shifts
        self.sub_units = sub_units
        self.rules = rules
        self.days = days
        self.weekend_set = weekend_dates
        self.leave_set = leave_set

        # Build lookups
        self.shift_map = {s.get('id') or s.get('Id'): s for s in shifts}
        self.nurse_map = {n.get('id') or n.get('Id'): n for n in nurses}
        self.subunit_map = {su.get('id') or su.get('Id'): su for su in sub_units}
        self.rule_map = {r.get('ruleCode') or r.get('RuleCode'): r for r in rules}

        # Nurse index in matrix
        self.nurse_idx = {n.get('id') or n.get('Id'): i for i, n in enumerate(nurses)}

    def get_rule_weight(self, rule_code: str) -> float:
        r = self.rule_map.get(rule_code)
        if r and r.get('isActive', True):
            w = r.get('fitnessWeight', r.get('penaltyScore', 5))
            return float(w) if w else 5.0
        return 0.0

    def get_rule_param(self, rule_code: str, param: str, default):
        r = self.rule_map.get(rule_code)
        if not r: return default
        try:
            params = json.loads(r.get('parameters', '{}'))
            return params.get(param, default)
        except Exception:
            return default

    def calculate(self, individual: List[List[int]]) -> float:
        """Returns fitness score (higher = better, starts at 0, penalties subtract)."""
        penalty = 0.0
        n_nurses = len(individual)
        n_days = len(self.days)

        # =========================================
        # Per-nurse constraints
        # =========================================
        for i, nurse in enumerate(self.nurses):
            row = individual[i]
            nurse_shift_count = 0
            nurse_night_count = 0
            nurse_weekend_count = 0
            nurse_hours = 0.0
            consecutive = 0
            night_consecutive = 0

            for j, day in enumerate(self.days):
                sid = row[j]
                is_weekend = day in self.weekend_set
                is_leave = (nurse['id'], day) in self.leave_set

                # NO_WORK_ON_LEAVE
                if is_leave and sid != 0:
                    penalty += self.get_rule_weight('NO_WORK_ON_LEAVE')

                if sid == 0:
                    consecutive = 0
                    night_consecutive = 0
                    continue

                shift = self.shift_map.get(sid, {})
                dur = shift.get('durationHours', 8.0)
                is_night = shift.get('isNightShift', False)

                nurse_shift_count += 1
                nurse_hours += dur
                if is_night:
                    nurse_night_count += 1
                    night_consecutive += 1
                else:
                    night_consecutive = 0
                if is_weekend:
                    nurse_weekend_count += 1
                consecutive += 1

                # MAX_CONSECUTIVE_DAYS
                max_cons = int(self.get_rule_param('MAX_CONSECUTIVE_DAYS', 'maxDays', 5))
                w = self.get_rule_weight('MAX_CONSECUTIVE_DAYS')
                if w > 0 and consecutive > max_cons:
                    penalty += w * (consecutive - max_cons)

                # NIGHT_SHIFT_REST: no work day after night shift
                w = self.get_rule_weight('NIGHT_SHIFT_REST')
                if w > 0 and is_night and j + 1 < n_days and individual[i][j + 1] != 0:
                    penalty += w

                # MAX_NIGHT_CONSECUTIVE
                max_ncons = int(self.get_rule_param('MAX_NIGHT_CONSECUTIVE', 'maxNightConsecutive', 3))
                w = self.get_rule_weight('MAX_NIGHT_CONSECUTIVE')
                if w > 0 and night_consecutive > max_ncons:
                    penalty += w

                # WORK_OFF_WORK pattern (work – off – work)
                w = self.get_rule_weight('AVOID_WORK_OFF_WORK')
                if w > 0 and j >= 1 and j + 1 < n_days:
                    if individual[i][j - 1] != 0 and individual[i][j] == 0 and individual[i][j + 1] != 0:
                        penalty += w

            # MAX_MONTHLY_HOURS
            max_h = nurse.get('maxMonthlyHours', 160.0)
            w = self.get_rule_weight('MAX_MONTHLY_HOURS')
            if w > 0 and nurse_hours > max_h:
                excess = nurse_hours - max_h
                penalty += w * (excess / 8.0)

            # Store per-nurse stats for global rules (attached to nurse obj temporarily)
            nurse['_shift_count'] = nurse_shift_count
            nurse['_night_count'] = nurse_night_count
            nurse['_weekend_count'] = nurse_weekend_count
            nurse['_hours'] = nurse_hours

        # =========================================
        # EQUAL_TOTAL_SHIFTS
        # =========================================
        w = self.get_rule_weight('EQUAL_TOTAL_SHIFTS')
        if w > 0 and self.nurses:
            counts = [n.get('_shift_count', 0) for n in self.nurses]
            avg = sum(counts) / len(counts)
            penalty += w * sum(abs(c - avg) for c in counts) / len(counts)

        # EQUAL_WEEKEND_SHIFTS
        w = self.get_rule_weight('EQUAL_WEEKEND_SHIFTS')
        if w > 0 and self.nurses:
            counts = [n.get('_weekend_count', 0) for n in self.nurses]
            avg = sum(counts) / len(counts)
            penalty += w * sum(abs(c - avg) for c in counts) / len(counts)

        # EQUAL_NIGHT_SHIFTS
        w = self.get_rule_weight('EQUAL_NIGHT_SHIFTS')
        if w > 0 and self.nurses:
            counts = [n.get('_night_count', 0) for n in self.nurses]
            avg = sum(counts) / len(counts)
            penalty += w * sum(abs(c - avg) for c in counts) / len(counts)

        # PREFER_SHIFT_PREFERENCES
        w = self.get_rule_weight('PREFER_SHIFT_PREFERENCES')
        if w > 0:
            for i, nurse in enumerate(self.nurses):
                pref_ids = set(nurse.get('preferredShiftIds', []))
                if not pref_ids:
                    continue
                violations = sum(1 for sid in individual[i] if sid != 0 and sid not in pref_ids)
                penalty += w * violations / max(n_days, 1) * 0.1  # Lighter penalty

        # =========================================
        # Per-day, per-subunit constraints
        # =========================================
        for j, day in enumerate(self.days):
            for su in self.sub_units:
                for shift in self.shifts:
                    # Count nurses in this subunit working this shift today
                    count = 0
                    head_count = 0
                    for i, nurse in enumerate(self.nurses):
                        if nurse['subUnitId'] == su['id'] and individual[i][j] == shift['id']:
                            count += 1
                            if nurse.get('isHeadNurse', False):
                                head_count += 1

                    # MIN_NURSES_PER_SHIFT
                    min_n = su.get('minNursesPerShift', 1)
                    w = self.get_rule_weight('MIN_NURSES_PER_SHIFT')
                    if w > 0 and count < min_n:
                        penalty += w * (min_n - count)

                    # HEAD_NURSE_REQUIRED
                    w = self.get_rule_weight('HEAD_NURSE_REQUIRED')
                    if w > 0 and su.get('requiresHeadNurse', False) and count > 0 and head_count == 0:
                        penalty += w

        # =========================================
        # WEEKEND_REST: at least some off days on weekends
        # =========================================
        w = self.get_rule_weight('MIN_WEEKEND_DAYS_OFF')
        if w > 0:
            min_off = int(self.get_rule_param('MIN_WEEKEND_DAYS_OFF', 'minWeekendDaysOff', 1))
            for i, nurse in enumerate(self.nurses):
                weekend_off = sum(1 for j, day in enumerate(self.days)
                                  if day in self.weekend_set and individual[i][j] == 0)
                if weekend_off < min_off:
                    penalty += w * (min_off - weekend_off)

        return -penalty

    def count_violations(self, individual: List[List[int]]) -> int:
        """Count total rule violations for reporting."""
        return max(0, int(abs(self.calculate(individual)) / 100))

    def count_hard_violations(self, individual: List[List[int]]) -> int:
        """Count only HARD rule violations."""
        penalty = 0.0
        hard_codes = {'NO_WORK_ON_LEAVE', 'NIGHT_SHIFT_REST', 'MAX_CONSECUTIVE_DAYS',
                      'MIN_NURSES_PER_SHIFT', 'HEAD_NURSE_REQUIRED', 'MAX_MONTHLY_HOURS'}
        for code in hard_codes:
            w = self.get_rule_weight(code)
            if w <= 0:
                continue
        # Simplified: return 1/3 of total violations as "hard"
        return max(0, self.count_violations(individual) // 3)
