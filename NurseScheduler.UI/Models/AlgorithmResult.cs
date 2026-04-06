using System.Collections.Generic;
using Newtonsoft.Json;

namespace NurseScheduler.UI.Models
{
    public class AlgorithmResult
    {
        [JsonProperty("scheduleId")] public int ScheduleId { get; set; }
        [JsonProperty("status")] public string Status { get; set; } = "";
        [JsonProperty("fitnessScore")] public double FitnessScore { get; set; }
        [JsonProperty("totalGenerations")] public int TotalGenerations { get; set; }
        [JsonProperty("executionTimeMs")] public int ExecutionTimeMs { get; set; }
        [JsonProperty("totalViolations")] public int TotalViolations { get; set; }
        [JsonProperty("hardViolations")] public int HardViolations { get; set; }
        [JsonProperty("softViolations")] public int SoftViolations { get; set; }
        [JsonProperty("entries")] public List<ResultEntry> Entries { get; set; } = new();
        [JsonProperty("violationDetails")] public List<ViolationDetail> ViolationDetails { get; set; } = new();
    }

    public class ResultEntry
    {
        [JsonProperty("nurseId")] public int NurseId { get; set; }
        [JsonProperty("date")] public string Date { get; set; } = "";
        [JsonProperty("shiftId")] public int? ShiftId { get; set; }
        [JsonProperty("isLeave")] public bool IsLeave { get; set; }
        [JsonProperty("isHeadNurseDay")] public bool IsHeadNurseDay { get; set; }
    }

    public class ViolationDetail
    {
        [JsonProperty("ruleCode")] public string RuleCode { get; set; } = "";
        [JsonProperty("ruleName")] public string RuleName { get; set; } = "";
        [JsonProperty("violationCount")] public int ViolationCount { get; set; }
        [JsonProperty("description")] public string Description { get; set; } = "";
    }
}
