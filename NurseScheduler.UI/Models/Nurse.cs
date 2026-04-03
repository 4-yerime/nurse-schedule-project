using System;
using System.Collections.Generic;
using System.Text;

namespace NurseScheduler.UI.Models
{
    public class Nurse
    {
        public int Id { get; set; }
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public int SubUnitId { get; set; }
        public bool IsHeadNurse { get; set; }
        public string EmploymentType { get; set; } = "FULL";
        public double MaxMonthlyHours { get; set; } = 160.0;
        public int AnnualLeaveBalance { get; set; } = 14;
    }
}