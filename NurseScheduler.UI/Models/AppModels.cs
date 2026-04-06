using System;

namespace NurseScheduler.UI.Models
{
    public class Unit
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string ColorHex { get; set; } = "#3498DB";
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    public class SubUnit
    {
        public int Id { get; set; }
        public int UnitId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int MinNursesPerShift { get; set; } = 1;
        public int MaxNursesPerShift { get; set; } = 10;
        public bool RequiresHeadNurse { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        // Navigation
        public string UnitName { get; set; } = string.Empty;
    }

    public class ShiftDefinition
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string ShortCode { get; set; } = string.Empty;
        public string StartTime { get; set; } = "08:00";
        public string EndTime { get; set; } = "16:00";
        public double DurationHours { get; set; }
        public string ColorHex { get; set; } = "#AED6F1";
        public bool IsNightShift { get; set; }
        public bool IsActive { get; set; } = true;
        public int SortOrder { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }

    public class Nurse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string FullName => $"{FirstName} {LastName}";
        public string? EmployeeCode { get; set; }
        public int SubUnitId { get; set; }
        public bool IsHeadNurse { get; set; }
        public string EmploymentType { get; set; } = "FULL";
        public double MaxMonthlyHours { get; set; } = 160.0;
        public int AnnualLeaveBalance { get; set; } = 14;
        public string PreferredShiftIds { get; set; } = "[]";
        public string? Notes { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
        // Navigation
        public string SubUnitName { get; set; } = string.Empty;
        public string UnitName { get; set; } = string.Empty;
    }

    public class NurseLeave
    {
        public int Id { get; set; }
        public int NurseId { get; set; }
        public DateTime LeaveDate { get; set; }
        public string LeaveType { get; set; } = "PERSONAL";
        public string? Reason { get; set; }
        public bool IsApproved { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        // Navigation
        public string NurseName { get; set; } = string.Empty;
    }

    public class ScheduleRule
    {
        public int Id { get; set; }
        public string RuleCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string Category { get; set; } = "SOFT";
        public int PenaltyScore { get; set; } = 5;
        public bool IsActive { get; set; } = true;
        public bool IsSystemRule { get; set; }
        public string Parameters { get; set; } = "{}";
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }

        public bool IsHard => PenaltyScore == 10;
        public int FitnessWeight => PenaltyScore switch
        {
            10 => 10000,
            9 => 1000,
            8 => 500,
            7 => 200,
            6 => 100,
            5 => 50,
            4 => 20,
            3 => 10,
            2 => 5,
            _ => 1
        };
    }

    public class Schedule
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }
        public string Status { get; set; } = "DRAFT";
        public string AlgorithmMode { get; set; } = "BALANCED";
        public double? FitnessScore { get; set; }
        public int? GenerationCount { get; set; }
        public int? ExecutionTimeMs { get; set; }
        public int TotalViolations { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime? UpdatedAt { get; set; }
    }

    public class ScheduleEntry
    {
        public int Id { get; set; }
        public int ScheduleId { get; set; }
        public int NurseId { get; set; }
        public DateTime EntryDate { get; set; }
        public int? ShiftId { get; set; }
        public bool IsLeave { get; set; }
        public bool IsHeadNurseDay { get; set; }
        public string? Notes { get; set; }
        // Navigation
        public string NurseName { get; set; } = string.Empty;
        public string ShiftName { get; set; } = string.Empty;
        public string ShiftColorHex { get; set; } = string.Empty;
    }

    public class AlgorithmLog
    {
        public int Id { get; set; }
        public int ScheduleId { get; set; }
        public DateTime LogTime { get; set; } = DateTime.Now;
        public int? Generation { get; set; }
        public double? BestFitness { get; set; }
        public double? AvgFitness { get; set; }
        public int? ViolationCount { get; set; }
        public int HardViolations { get; set; }
        public int SoftViolations { get; set; }
        public string? LogMessage { get; set; }
    }

    public class AppSetting
    {
        public string Key { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
    }
}
