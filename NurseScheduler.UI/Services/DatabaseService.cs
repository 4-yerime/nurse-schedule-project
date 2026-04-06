using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Data.Sqlite;
using NurseScheduler.UI.Models;
using Newtonsoft.Json;

namespace NurseScheduler.UI.Services
{
    public class DatabaseService
    {
        private readonly string _dbPath;
        private readonly string _connectionString;

        public DatabaseService()
        {
            var appData = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "NurseSchedulerPro");
            Directory.CreateDirectory(appData);
            _dbPath = Path.Combine(appData, "nursescheduler.db");
            _connectionString = $"Data Source={_dbPath}";
            InitializeDatabase();
        }

        private SqliteConnection OpenConnection()
        {
            var conn = new SqliteConnection(_connectionString);
            conn.Open();
            using var pragmaCmd = conn.CreateCommand();
            pragmaCmd.CommandText = "PRAGMA foreign_keys = ON;";
            pragmaCmd.ExecuteNonQuery();
            return conn;
        }

        private void InitializeDatabase()
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
CREATE TABLE IF NOT EXISTS Units (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    Description TEXT,
    ColorHex TEXT DEFAULT '#3498DB',
    IsActive INTEGER DEFAULT 1,
    SortOrder INTEGER DEFAULT 0,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    UpdatedAt TEXT
);

CREATE TABLE IF NOT EXISTS SubUnits (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    UnitId INTEGER NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    MinNursesPerShift INTEGER DEFAULT 1,
    MaxNursesPerShift INTEGER DEFAULT 10,
    RequiresHeadNurse INTEGER DEFAULT 0,
    IsActive INTEGER DEFAULT 1,
    SortOrder INTEGER DEFAULT 0,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    UpdatedAt TEXT,
    FOREIGN KEY (UnitId) REFERENCES Units(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ShiftDefinitions (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL UNIQUE,
    ShortCode TEXT NOT NULL UNIQUE,
    StartTime TEXT NOT NULL,
    EndTime TEXT NOT NULL,
    DurationHours REAL NOT NULL,
    ColorHex TEXT DEFAULT '#AED6F1',
    IsNightShift INTEGER DEFAULT 0,
    IsActive INTEGER DEFAULT 1,
    SortOrder INTEGER DEFAULT 0,
    CreatedAt TEXT DEFAULT (datetime('now','localtime'))
);

CREATE TABLE IF NOT EXISTS Nurses (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    FirstName TEXT NOT NULL,
    LastName TEXT NOT NULL,
    EmployeeCode TEXT UNIQUE,
    SubUnitId INTEGER NOT NULL,
    IsHeadNurse INTEGER DEFAULT 0,
    EmploymentType TEXT DEFAULT 'FULL',
    MaxMonthlyHours REAL DEFAULT 160.0,
    AnnualLeaveBalance INTEGER DEFAULT 14,
    PreferredShiftIds TEXT DEFAULT '[]',
    Notes TEXT,
    IsActive INTEGER DEFAULT 1,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    UpdatedAt TEXT,
    FOREIGN KEY (SubUnitId) REFERENCES SubUnits(Id)
);

CREATE TABLE IF NOT EXISTS NurseLeaves (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    NurseId INTEGER NOT NULL,
    LeaveDate TEXT NOT NULL,
    LeaveType TEXT DEFAULT 'PERSONAL',
    Reason TEXT,
    IsApproved INTEGER DEFAULT 1,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    FOREIGN KEY (NurseId) REFERENCES Nurses(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ScheduleRules (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    RuleCode TEXT UNIQUE NOT NULL,
    Name TEXT NOT NULL,
    Description TEXT,
    Category TEXT NOT NULL,
    PenaltyScore INTEGER DEFAULT 5,
    IsActive INTEGER DEFAULT 1,
    IsSystemRule INTEGER DEFAULT 0,
    Parameters TEXT DEFAULT '{}',
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    UpdatedAt TEXT
);

CREATE TABLE IF NOT EXISTS Schedules (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    Name TEXT NOT NULL,
    StartDate TEXT NOT NULL,
    EndDate TEXT NOT NULL,
    Status TEXT DEFAULT 'DRAFT',
    AlgorithmMode TEXT DEFAULT 'BALANCED',
    FitnessScore REAL,
    GenerationCount INTEGER,
    ExecutionTimeMs INTEGER,
    TotalViolations INTEGER DEFAULT 0,
    Notes TEXT,
    CreatedAt TEXT DEFAULT (datetime('now','localtime')),
    UpdatedAt TEXT
);

CREATE TABLE IF NOT EXISTS ScheduleEntries (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ScheduleId INTEGER NOT NULL,
    NurseId INTEGER NOT NULL,
    EntryDate TEXT NOT NULL,
    ShiftId INTEGER,
    IsLeave INTEGER DEFAULT 0,
    IsHeadNurseDay INTEGER DEFAULT 0,
    Notes TEXT,
    FOREIGN KEY (ScheduleId) REFERENCES Schedules(Id) ON DELETE CASCADE,
    FOREIGN KEY (NurseId) REFERENCES Nurses(Id),
    FOREIGN KEY (ShiftId) REFERENCES ShiftDefinitions(Id)
);

CREATE TABLE IF NOT EXISTS AlgorithmLogs (
    Id INTEGER PRIMARY KEY AUTOINCREMENT,
    ScheduleId INTEGER NOT NULL,
    LogTime TEXT DEFAULT (datetime('now','localtime')),
    Generation INTEGER,
    BestFitness REAL,
    AvgFitness REAL,
    ViolationCount INTEGER,
    HardViolations INTEGER DEFAULT 0,
    SoftViolations INTEGER DEFAULT 0,
    LogMessage TEXT,
    FOREIGN KEY (ScheduleId) REFERENCES Schedules(Id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS AppSettings (
    Key TEXT PRIMARY KEY,
    Value TEXT NOT NULL,
    UpdatedAt TEXT DEFAULT (datetime('now','localtime'))
);
";
            cmd.ExecuteNonQuery();
            SeedDefaultData(conn);
        }

        private void SeedDefaultData(SqliteConnection conn)
        {
            // AppSettings
            var settingsCmd = conn.CreateCommand();
            settingsCmd.CommandText = @"
INSERT OR IGNORE INTO AppSettings VALUES ('HospitalName', 'Hastane Adı Giriniz', datetime('now','localtime'));
INSERT OR IGNORE INTO AppSettings VALUES ('Theme', 'Dark', datetime('now','localtime'));
INSERT OR IGNORE INTO AppSettings VALUES ('AlgorithmDefaultMode', 'BALANCED', datetime('now','localtime'));
INSERT OR IGNORE INTO AppSettings VALUES ('LastScheduleId', '0', datetime('now','localtime'));
INSERT OR IGNORE INTO AppSettings VALUES ('DatabaseVersion', '1.0', datetime('now','localtime'));
";
            settingsCmd.ExecuteNonQuery();

            // Default Rules
            var rulesCmd = conn.CreateCommand();
            rulesCmd.CommandText = @"
INSERT OR IGNORE INTO ScheduleRules (Id,RuleCode,Name,Description,Category,PenaltyScore,IsActive,IsSystemRule,Parameters) VALUES
(1,'MIN_NURSES_PER_SHIFT','Vardiya Başına Minimum Hemşire','Her vardiyada alt birim için tanımlanan minimum hemşire sayısı karşılanmalıdır.','HARD',10,1,1,'{}'),
(2,'NO_WORK_ON_LEAVE','İzinli Hemşire Çalışamaz','İzin günü olarak işaretlenen günlerde hemşire hiçbir vardiyaya atanamaz.','HARD',10,1,1,'{}'),
(3,'NIGHT_SHIFT_REST','Gece Sonrası Dinlenme','Gece vardiyasında çalışan hemşire ertesi gün hiçbir vardiyaya atanamaz.','HARD',10,1,1,'{}'),
(4,'EVENING_TO_MORNING_FORBIDDEN','Akşam Sonrası Sabah Yasak','Akşam vardiyasında çalışan hemşire ertesi gün sabah vardiyasına atanamaz.','HARD',10,1,1,'{}'),
(5,'MAX_MONTHLY_HOURS','Aylık Maksimum Çalışma Saati','Hemşirenin toplam çalışma saati tanımlanan maksimum aylık saati aşamaz.','HARD',10,1,1,'{}'),
(6,'ONE_SHIFT_PER_DAY','Günde Tek Vardiya','Bir hemşire aynı gün içinde birden fazla vardiyaya atanamaz.','HARD',10,1,1,'{}'),
(7,'HEAD_NURSE_REQUIRED','Baş Hemşire Zorunluluğu','RequiresHeadNurse işaretli alt birimlerde her vardiyada en az bir baş hemşire bulunmalıdır.','HARD',10,0,1,'{}'),
(8,'MAX_CONSECUTIVE_DAYS','Maksimum Arka Arkaya Çalışma','Hemşire arka arkaya parametrede belirtilen günden fazla çalışamaz.','SOFT',8,1,1,'{""maxDays"":5}'),
(9,'EQUAL_TOTAL_SHIFTS','Eşit Toplam Vardiya Dağılımı','Tüm hemşirelerin aylık toplam vardiya sayıları birbirine mümkün olduğunca eşit olmalıdır.','SOFT',7,1,1,'{}'),
(10,'EQUAL_WEEKEND_SHIFTS','Eşit Hafta Sonu Dağılımı','Bir hemşire bir hafta sonu çalıştıysa aynı ay içinde mümkün olduğunca az hafta sonu daha çalışmalıdır.','SOFT',7,1,1,'{}'),
(11,'EQUAL_NIGHT_DAY_SHIFTS','Eşit Gece/Gündüz Dağılımı','Her hemşirenin gece ve gündüz vardiya sayıları birbirine mümkün olduğunca eşit olmalıdır.','SOFT',6,1,1,'{}'),
(12,'AVOID_WORK_OFF_WORK','Çalışma-İzin-Çalışma Deseni','Çalış-izin-çalış şeklindeki tek günlük izin desenini minimize et.','SOFT',4,1,1,'{}'),
(13,'AVOID_OFF_WORK_OFF','İzin-Çalışma-İzin Deseni','İzin-çalış-izin şeklindeki tek günlük çalışma desenini minimize et.','SOFT',4,1,1,'{}'),
(14,'PREFERRED_SHIFTS','Hemşire Vardiya Tercihleri','Hemşireler mümkün olduğunca tercih ettikleri vardiyalara atanmalıdır.','SOFT',3,1,1,'{}'),
(15,'MIN_REST_BETWEEN_SHIFTS','Vardiyalar Arası Minimum Dinlenme','İki vardiya arasında en az parametrede belirtilen saat kadar dinlenme olmalıdır.','SOFT',6,1,1,'{""minHours"":11}');
";
            rulesCmd.ExecuteNonQuery();
        }

        // ===================== SETTINGS =====================
        public string GetSetting(string key, string defaultValue = "")
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Value FROM AppSettings WHERE Key = @key";
            cmd.Parameters.AddWithValue("@key", key);
            var result = cmd.ExecuteScalar();
            return result?.ToString() ?? defaultValue;
        }

        public void SetSetting(string key, string value)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO AppSettings (Key,Value,UpdatedAt) VALUES (@k,@v,datetime('now','localtime'))
                               ON CONFLICT(Key) DO UPDATE SET Value=@v, UpdatedAt=datetime('now','localtime')";
            cmd.Parameters.AddWithValue("@k", key);
            cmd.Parameters.AddWithValue("@v", value);
            cmd.ExecuteNonQuery();
        }

        // ===================== UNITS =====================
        public List<Unit> GetAllUnits()
        {
            var list = new List<Unit>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Units WHERE IsActive=1 ORDER BY SortOrder, Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapUnit(r));
            return list;
        }

        public List<Unit> GetAllUnitsIncludeInactive()
        {
            var list = new List<Unit>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Units ORDER BY SortOrder, Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapUnit(r));
            return list;
        }

        public int AddUnit(Unit unit)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Units (Name,Description,ColorHex,IsActive,SortOrder) 
                                VALUES (@n,@d,@c,@a,@s);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@n", unit.Name);
            cmd.Parameters.AddWithValue("@d", (object?)unit.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@c", unit.ColorHex);
            cmd.Parameters.AddWithValue("@a", unit.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@s", unit.SortOrder);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void UpdateUnit(Unit unit)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE Units SET Name=@n,Description=@d,ColorHex=@c,IsActive=@a,SortOrder=@s,
                                UpdatedAt=datetime('now','localtime') WHERE Id=@id";
            cmd.Parameters.AddWithValue("@n", unit.Name);
            cmd.Parameters.AddWithValue("@d", (object?)unit.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@c", unit.ColorHex);
            cmd.Parameters.AddWithValue("@a", unit.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@s", unit.SortOrder);
            cmd.Parameters.AddWithValue("@id", unit.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteUnit(int id)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Units WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Unit MapUnit(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
            ColorHex = r.GetString(r.GetOrdinal("ColorHex")),
            IsActive = r.GetInt32(r.GetOrdinal("IsActive")) == 1,
            SortOrder = r.GetInt32(r.GetOrdinal("SortOrder")),
        };

        // ===================== SUBUNITS =====================
        public List<SubUnit> GetSubUnitsByUnit(int unitId)
        {
            var list = new List<SubUnit>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT s.*, u.Name as UnitName FROM SubUnits s
                                JOIN Units u ON s.UnitId=u.Id
                                WHERE s.UnitId=@uid AND s.IsActive=1 ORDER BY s.SortOrder, s.Name";
            cmd.Parameters.AddWithValue("@uid", unitId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapSubUnit(r));
            return list;
        }

        public List<SubUnit> GetAllSubUnits()
        {
            var list = new List<SubUnit>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT s.*, u.Name as UnitName FROM SubUnits s
                                JOIN Units u ON s.UnitId=u.Id
                                WHERE s.IsActive=1 ORDER BY u.SortOrder, s.SortOrder, s.Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapSubUnit(r));
            return list;
        }

        public void AddSubUnit(SubUnit su)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO SubUnits (UnitId,Name,Description,MinNursesPerShift,MaxNursesPerShift,RequiresHeadNurse,IsActive,SortOrder)
                                VALUES (@uid,@n,@d,@min,@max,@rhn,@a,@s)";
            cmd.Parameters.AddWithValue("@uid", su.UnitId);
            cmd.Parameters.AddWithValue("@n", su.Name);
            cmd.Parameters.AddWithValue("@d", (object?)su.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@min", su.MinNursesPerShift);
            cmd.Parameters.AddWithValue("@max", su.MaxNursesPerShift);
            cmd.Parameters.AddWithValue("@rhn", su.RequiresHeadNurse ? 1 : 0);
            cmd.Parameters.AddWithValue("@a", su.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@s", su.SortOrder);
            cmd.ExecuteNonQuery();
        }

        public void UpdateSubUnit(SubUnit su)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE SubUnits SET UnitId=@uid,Name=@n,Description=@d,MinNursesPerShift=@min,
                                MaxNursesPerShift=@max,RequiresHeadNurse=@rhn,IsActive=@a,SortOrder=@s,
                                UpdatedAt=datetime('now','localtime') WHERE Id=@id";
            cmd.Parameters.AddWithValue("@uid", su.UnitId);
            cmd.Parameters.AddWithValue("@n", su.Name);
            cmd.Parameters.AddWithValue("@d", (object?)su.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@min", su.MinNursesPerShift);
            cmd.Parameters.AddWithValue("@max", su.MaxNursesPerShift);
            cmd.Parameters.AddWithValue("@rhn", su.RequiresHeadNurse ? 1 : 0);
            cmd.Parameters.AddWithValue("@a", su.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@s", su.SortOrder);
            cmd.Parameters.AddWithValue("@id", su.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteSubUnit(int id)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM SubUnits WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static SubUnit MapSubUnit(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            UnitId = r.GetInt32(r.GetOrdinal("UnitId")),
            Name = r.GetString(r.GetOrdinal("Name")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
            MinNursesPerShift = r.GetInt32(r.GetOrdinal("MinNursesPerShift")),
            MaxNursesPerShift = r.GetInt32(r.GetOrdinal("MaxNursesPerShift")),
            RequiresHeadNurse = r.GetInt32(r.GetOrdinal("RequiresHeadNurse")) == 1,
            IsActive = r.GetInt32(r.GetOrdinal("IsActive")) == 1,
            SortOrder = r.GetInt32(r.GetOrdinal("SortOrder")),
            UnitName = r.IsDBNull(r.GetOrdinal("UnitName")) ? "" : r.GetString(r.GetOrdinal("UnitName")),
        };

        // ===================== SHIFTS =====================
        public List<ShiftDefinition> GetAllShifts()
        {
            var list = new List<ShiftDefinition>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM ShiftDefinitions WHERE IsActive=1 ORDER BY SortOrder, Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapShift(r));
            return list;
        }

        public List<ShiftDefinition> GetAllShiftsIncludeInactive()
        {
            var list = new List<ShiftDefinition>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM ShiftDefinitions ORDER BY SortOrder, Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapShift(r));
            return list;
        }

        public void AddShift(ShiftDefinition s)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ShiftDefinitions (Name,ShortCode,StartTime,EndTime,DurationHours,ColorHex,IsNightShift,IsActive,SortOrder)
                                VALUES (@n,@sc,@st,@et,@dh,@c,@ins,@a,@s)";
            cmd.Parameters.AddWithValue("@n", s.Name);
            cmd.Parameters.AddWithValue("@sc", s.ShortCode);
            cmd.Parameters.AddWithValue("@st", s.StartTime);
            cmd.Parameters.AddWithValue("@et", s.EndTime);
            cmd.Parameters.AddWithValue("@dh", s.DurationHours);
            cmd.Parameters.AddWithValue("@c", s.ColorHex);
            cmd.Parameters.AddWithValue("@ins", s.IsNightShift ? 1 : 0);
            cmd.Parameters.AddWithValue("@a", s.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@s", s.SortOrder);
            cmd.ExecuteNonQuery();
        }

        public void UpdateShift(ShiftDefinition s)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE ShiftDefinitions SET Name=@n,ShortCode=@sc,StartTime=@st,EndTime=@et,
                                DurationHours=@dh,ColorHex=@c,IsNightShift=@ins,IsActive=@a,SortOrder=@s WHERE Id=@id";
            cmd.Parameters.AddWithValue("@n", s.Name);
            cmd.Parameters.AddWithValue("@sc", s.ShortCode);
            cmd.Parameters.AddWithValue("@st", s.StartTime);
            cmd.Parameters.AddWithValue("@et", s.EndTime);
            cmd.Parameters.AddWithValue("@dh", s.DurationHours);
            cmd.Parameters.AddWithValue("@c", s.ColorHex);
            cmd.Parameters.AddWithValue("@ins", s.IsNightShift ? 1 : 0);
            cmd.Parameters.AddWithValue("@a", s.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@s", s.SortOrder);
            cmd.Parameters.AddWithValue("@id", s.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteShift(int id)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ShiftDefinitions WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static ShiftDefinition MapShift(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            ShortCode = r.GetString(r.GetOrdinal("ShortCode")),
            StartTime = r.GetString(r.GetOrdinal("StartTime")),
            EndTime = r.GetString(r.GetOrdinal("EndTime")),
            DurationHours = r.GetDouble(r.GetOrdinal("DurationHours")),
            ColorHex = r.GetString(r.GetOrdinal("ColorHex")),
            IsNightShift = r.GetInt32(r.GetOrdinal("IsNightShift")) == 1,
            IsActive = r.GetInt32(r.GetOrdinal("IsActive")) == 1,
            SortOrder = r.GetInt32(r.GetOrdinal("SortOrder")),
        };

        // ===================== NURSES =====================
        public List<Nurse> GetAllNurses()
        {
            var list = new List<Nurse>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT n.*, s.Name as SubUnitName, u.Name as UnitName
                                FROM Nurses n
                                JOIN SubUnits s ON n.SubUnitId=s.Id
                                JOIN Units u ON s.UnitId=u.Id
                                WHERE n.IsActive=1 ORDER BY u.Name, s.Name, n.LastName, n.FirstName";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapNurse(r));
            return list;
        }

        public List<Nurse> GetNursesBySubUnit(int subUnitId)
        {
            var list = new List<Nurse>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT n.*, s.Name as SubUnitName, u.Name as UnitName
                                FROM Nurses n
                                JOIN SubUnits s ON n.SubUnitId=s.Id
                                JOIN Units u ON s.UnitId=u.Id
                                WHERE n.SubUnitId=@suid AND n.IsActive=1 ORDER BY n.LastName, n.FirstName";
            cmd.Parameters.AddWithValue("@suid", subUnitId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapNurse(r));
            return list;
        }

        public void AddNurse(Nurse nurse)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Nurses (FirstName,LastName,EmployeeCode,SubUnitId,IsHeadNurse,EmploymentType,
                                MaxMonthlyHours,AnnualLeaveBalance,PreferredShiftIds,Notes,IsActive)
                                VALUES (@fn,@ln,@ec,@suid,@ihn,@et,@mmh,@alb,@psi,@notes,@a)";
            cmd.Parameters.AddWithValue("@fn", nurse.FirstName);
            cmd.Parameters.AddWithValue("@ln", nurse.LastName);
            cmd.Parameters.AddWithValue("@ec", (object?)nurse.EmployeeCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@suid", nurse.SubUnitId);
            cmd.Parameters.AddWithValue("@ihn", nurse.IsHeadNurse ? 1 : 0);
            cmd.Parameters.AddWithValue("@et", nurse.EmploymentType);
            cmd.Parameters.AddWithValue("@mmh", nurse.MaxMonthlyHours);
            cmd.Parameters.AddWithValue("@alb", nurse.AnnualLeaveBalance);
            cmd.Parameters.AddWithValue("@psi", nurse.PreferredShiftIds);
            cmd.Parameters.AddWithValue("@notes", (object?)nurse.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", nurse.IsActive ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void UpdateNurse(Nurse nurse)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE Nurses SET FirstName=@fn,LastName=@ln,EmployeeCode=@ec,SubUnitId=@suid,
                                IsHeadNurse=@ihn,EmploymentType=@et,MaxMonthlyHours=@mmh,AnnualLeaveBalance=@alb,
                                PreferredShiftIds=@psi,Notes=@notes,IsActive=@a,
                                UpdatedAt=datetime('now','localtime') WHERE Id=@id";
            cmd.Parameters.AddWithValue("@fn", nurse.FirstName);
            cmd.Parameters.AddWithValue("@ln", nurse.LastName);
            cmd.Parameters.AddWithValue("@ec", (object?)nurse.EmployeeCode ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@suid", nurse.SubUnitId);
            cmd.Parameters.AddWithValue("@ihn", nurse.IsHeadNurse ? 1 : 0);
            cmd.Parameters.AddWithValue("@et", nurse.EmploymentType);
            cmd.Parameters.AddWithValue("@mmh", nurse.MaxMonthlyHours);
            cmd.Parameters.AddWithValue("@alb", nurse.AnnualLeaveBalance);
            cmd.Parameters.AddWithValue("@psi", nurse.PreferredShiftIds);
            cmd.Parameters.AddWithValue("@notes", (object?)nurse.Notes ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@a", nurse.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", nurse.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteNurse(int id)
        {
            using var conn = OpenConnection();
            using var trans = conn.BeginTransaction();
            try
            {
                // Delete cascading dependencies explicitly if foreign keys are not strictly configured for cascade drop
                var cmd1 = conn.CreateCommand();
                cmd1.CommandText = "DELETE FROM NurseLeaves WHERE NurseId=@id";
                cmd1.Parameters.AddWithValue("@id", id);
                cmd1.ExecuteNonQuery();

                var cmd2 = conn.CreateCommand();
                cmd2.CommandText = "DELETE FROM ScheduleEntries WHERE NurseId=@id";
                cmd2.Parameters.AddWithValue("@id", id);
                cmd2.ExecuteNonQuery();

                var cmd3 = conn.CreateCommand();
                cmd3.CommandText = "DELETE FROM Nurses WHERE Id=@id";
                cmd3.Parameters.AddWithValue("@id", id);
                cmd3.ExecuteNonQuery();

                trans.Commit();
            }
            catch
            {
                trans.Rollback();
                throw;
            }
        }

        private static Nurse MapNurse(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            FirstName = r.GetString(r.GetOrdinal("FirstName")),
            LastName = r.GetString(r.GetOrdinal("LastName")),
            EmployeeCode = r.IsDBNull(r.GetOrdinal("EmployeeCode")) ? null : r.GetString(r.GetOrdinal("EmployeeCode")),
            SubUnitId = r.GetInt32(r.GetOrdinal("SubUnitId")),
            IsHeadNurse = r.GetInt32(r.GetOrdinal("IsHeadNurse")) == 1,
            EmploymentType = r.GetString(r.GetOrdinal("EmploymentType")),
            MaxMonthlyHours = r.GetDouble(r.GetOrdinal("MaxMonthlyHours")),
            AnnualLeaveBalance = r.GetInt32(r.GetOrdinal("AnnualLeaveBalance")),
            PreferredShiftIds = r.GetString(r.GetOrdinal("PreferredShiftIds")),
            Notes = r.IsDBNull(r.GetOrdinal("Notes")) ? null : r.GetString(r.GetOrdinal("Notes")),
            IsActive = r.GetInt32(r.GetOrdinal("IsActive")) == 1,
            SubUnitName = r.IsDBNull(r.GetOrdinal("SubUnitName")) ? "" : r.GetString(r.GetOrdinal("SubUnitName")),
            UnitName = r.IsDBNull(r.GetOrdinal("UnitName")) ? "" : r.GetString(r.GetOrdinal("UnitName")),
        };

        // ===================== NURSE LEAVES =====================
        public List<NurseLeave> GetLeavesByNurse(int nurseId)
        {
            var list = new List<NurseLeave>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM NurseLeaves WHERE NurseId=@nid ORDER BY LeaveDate";
            cmd.Parameters.AddWithValue("@nid", nurseId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapLeave(r));
            return list;
        }

        public void AddLeave(NurseLeave leave)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO NurseLeaves (NurseId,LeaveDate,LeaveType,Reason,IsApproved)
                                VALUES (@nid,@ld,@lt,@r,@ia)";
            cmd.Parameters.AddWithValue("@nid", leave.NurseId);
            cmd.Parameters.AddWithValue("@ld", leave.LeaveDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@lt", leave.LeaveType);
            cmd.Parameters.AddWithValue("@r", (object?)leave.Reason ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@ia", leave.IsApproved ? 1 : 0);
            cmd.ExecuteNonQuery();
        }

        public void DeleteLeave(int id)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM NurseLeaves WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static NurseLeave MapLeave(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            NurseId = r.GetInt32(r.GetOrdinal("NurseId")),
            LeaveDate = DateTime.Parse(r.GetString(r.GetOrdinal("LeaveDate"))),
            LeaveType = r.GetString(r.GetOrdinal("LeaveType")),
            Reason = r.IsDBNull(r.GetOrdinal("Reason")) ? null : r.GetString(r.GetOrdinal("Reason")),
            IsApproved = r.GetInt32(r.GetOrdinal("IsApproved")) == 1,
        };

        // ===================== RULES =====================
        public List<ScheduleRule> GetAllRules()
        {
            var list = new List<ScheduleRule>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM ScheduleRules ORDER BY IsSystemRule DESC, PenaltyScore DESC, Name";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapRule(r));
            return list;
        }

        public List<ScheduleRule> GetActiveRules()
        {
            var list = new List<ScheduleRule>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM ScheduleRules WHERE IsActive=1 ORDER BY PenaltyScore DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapRule(r));
            return list;
        }

        public void AddRule(ScheduleRule rule)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO ScheduleRules (RuleCode,Name,Description,Category,PenaltyScore,IsActive,IsSystemRule,Parameters)
                                VALUES (@rc,@n,@d,@cat,@ps,@a,0,@p)";
            cmd.Parameters.AddWithValue("@rc", rule.RuleCode);
            cmd.Parameters.AddWithValue("@n", rule.Name);
            cmd.Parameters.AddWithValue("@d", (object?)rule.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cat", rule.Category);
            cmd.Parameters.AddWithValue("@ps", rule.PenaltyScore);
            cmd.Parameters.AddWithValue("@a", rule.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@p", rule.Parameters);
            cmd.ExecuteNonQuery();
        }

        public void UpdateRule(ScheduleRule rule)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE ScheduleRules SET Name=@n,Description=@d,Category=@cat,PenaltyScore=@ps,
                                IsActive=@a,Parameters=@p,UpdatedAt=datetime('now','localtime') WHERE Id=@id";
            cmd.Parameters.AddWithValue("@n", rule.Name);
            cmd.Parameters.AddWithValue("@d", (object?)rule.Description ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@cat", rule.Category);
            cmd.Parameters.AddWithValue("@ps", rule.PenaltyScore);
            cmd.Parameters.AddWithValue("@a", rule.IsActive ? 1 : 0);
            cmd.Parameters.AddWithValue("@p", rule.Parameters);
            cmd.Parameters.AddWithValue("@id", rule.Id);
            cmd.ExecuteNonQuery();
        }

        public void DeleteRule(int id)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM ScheduleRules WHERE Id=@id AND IsSystemRule=0";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static ScheduleRule MapRule(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            RuleCode = r.GetString(r.GetOrdinal("RuleCode")),
            Name = r.GetString(r.GetOrdinal("Name")),
            Description = r.IsDBNull(r.GetOrdinal("Description")) ? null : r.GetString(r.GetOrdinal("Description")),
            Category = r.GetString(r.GetOrdinal("Category")),
            PenaltyScore = r.GetInt32(r.GetOrdinal("PenaltyScore")),
            IsActive = r.GetInt32(r.GetOrdinal("IsActive")) == 1,
            IsSystemRule = r.GetInt32(r.GetOrdinal("IsSystemRule")) == 1,
            Parameters = r.GetString(r.GetOrdinal("Parameters")),
        };

        // ===================== SCHEDULES =====================
        public List<Schedule> GetAllSchedules()
        {
            var list = new List<Schedule>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM Schedules ORDER BY CreatedAt DESC";
            using var r = cmd.ExecuteReader();
            while (r.Read())
                list.Add(MapSchedule(r));
            return list;
        }

        public int AddSchedule(Schedule schedule)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO Schedules (Name,StartDate,EndDate,Status,AlgorithmMode,Notes)
                                VALUES (@n,@sd,@ed,@st,@am,@notes);
                                SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@n", schedule.Name);
            cmd.Parameters.AddWithValue("@sd", schedule.StartDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@ed", schedule.EndDate.ToString("yyyy-MM-dd"));
            cmd.Parameters.AddWithValue("@st", schedule.Status);
            cmd.Parameters.AddWithValue("@am", schedule.AlgorithmMode);
            cmd.Parameters.AddWithValue("@notes", (object?)schedule.Notes ?? DBNull.Value);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public void UpdateScheduleResult(int scheduleId, double fitnessScore, int genCount, int execMs, int totalViolations)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"UPDATE Schedules SET Status='GENERATED',FitnessScore=@fs,GenerationCount=@gc,
                                ExecutionTimeMs=@em,TotalViolations=@tv,UpdatedAt=datetime('now','localtime')
                                WHERE Id=@id";
            cmd.Parameters.AddWithValue("@fs", fitnessScore);
            cmd.Parameters.AddWithValue("@gc", genCount);
            cmd.Parameters.AddWithValue("@em", execMs);
            cmd.Parameters.AddWithValue("@tv", totalViolations);
            cmd.Parameters.AddWithValue("@id", scheduleId);
            cmd.ExecuteNonQuery();
        }

        public void DeleteSchedule(int id)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "DELETE FROM Schedules WHERE Id=@id";
            cmd.Parameters.AddWithValue("@id", id);
            cmd.ExecuteNonQuery();
        }

        private static Schedule MapSchedule(SqliteDataReader r) => new()
        {
            Id = r.GetInt32(r.GetOrdinal("Id")),
            Name = r.GetString(r.GetOrdinal("Name")),
            StartDate = DateTime.Parse(r.GetString(r.GetOrdinal("StartDate"))),
            EndDate = DateTime.Parse(r.GetString(r.GetOrdinal("EndDate"))),
            Status = r.GetString(r.GetOrdinal("Status")),
            AlgorithmMode = r.GetString(r.GetOrdinal("AlgorithmMode")),
            FitnessScore = r.IsDBNull(r.GetOrdinal("FitnessScore")) ? null : r.GetDouble(r.GetOrdinal("FitnessScore")),
            GenerationCount = r.IsDBNull(r.GetOrdinal("GenerationCount")) ? null : r.GetInt32(r.GetOrdinal("GenerationCount")),
            ExecutionTimeMs = r.IsDBNull(r.GetOrdinal("ExecutionTimeMs")) ? null : r.GetInt32(r.GetOrdinal("ExecutionTimeMs")),
            TotalViolations = r.GetInt32(r.GetOrdinal("TotalViolations")),
            Notes = r.IsDBNull(r.GetOrdinal("Notes")) ? null : r.GetString(r.GetOrdinal("Notes")),
            CreatedAt = DateTime.Parse(r.GetString(r.GetOrdinal("CreatedAt"))),
        };

        // ===================== SCHEDULE ENTRIES =====================
        public void SaveScheduleEntries(int scheduleId, List<ScheduleEntry> entries)
        {
            using var conn = OpenConnection();
            using var tx = conn.BeginTransaction();
            var delCmd = conn.CreateCommand();
            delCmd.CommandText = "DELETE FROM ScheduleEntries WHERE ScheduleId=@sid";
            delCmd.Parameters.AddWithValue("@sid", scheduleId);
            delCmd.ExecuteNonQuery();

            foreach (var e in entries)
            {
                var cmd = conn.CreateCommand();
                cmd.CommandText = @"INSERT INTO ScheduleEntries (ScheduleId,NurseId,EntryDate,ShiftId,IsLeave,IsHeadNurseDay,Notes)
                                    VALUES (@sid,@nid,@ed,@shid,@il,@ihnd,@notes)";
                cmd.Parameters.AddWithValue("@sid", scheduleId);
                cmd.Parameters.AddWithValue("@nid", e.NurseId);
                cmd.Parameters.AddWithValue("@ed", e.EntryDate.ToString("yyyy-MM-dd"));
                cmd.Parameters.AddWithValue("@shid", (object?)e.ShiftId ?? DBNull.Value);
                cmd.Parameters.AddWithValue("@il", e.IsLeave ? 1 : 0);
                cmd.Parameters.AddWithValue("@ihnd", e.IsHeadNurseDay ? 1 : 0);
                cmd.Parameters.AddWithValue("@notes", (object?)e.Notes ?? DBNull.Value);
                cmd.ExecuteNonQuery();
            }
            tx.Commit();
        }

        public List<ScheduleEntry> GetScheduleEntries(int scheduleId)
        {
            var list = new List<ScheduleEntry>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT se.*, 
                                n.FirstName||' '||n.LastName as NurseName,
                                COALESCE(sd.Name,'') as ShiftName,
                                COALESCE(sd.ColorHex,'') as ShiftColorHex
                                FROM ScheduleEntries se
                                JOIN Nurses n ON se.NurseId=n.Id
                                LEFT JOIN ShiftDefinitions sd ON se.ShiftId=sd.Id
                                WHERE se.ScheduleId=@sid
                                ORDER BY se.EntryDate, n.LastName";
            cmd.Parameters.AddWithValue("@sid", scheduleId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new ScheduleEntry
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    ScheduleId = r.GetInt32(r.GetOrdinal("ScheduleId")),
                    NurseId = r.GetInt32(r.GetOrdinal("NurseId")),
                    EntryDate = DateTime.Parse(r.GetString(r.GetOrdinal("EntryDate"))),
                    ShiftId = r.IsDBNull(r.GetOrdinal("ShiftId")) ? null : r.GetInt32(r.GetOrdinal("ShiftId")),
                    IsLeave = r.GetInt32(r.GetOrdinal("IsLeave")) == 1,
                    IsHeadNurseDay = r.GetInt32(r.GetOrdinal("IsHeadNurseDay")) == 1,
                    NurseName = r.GetString(r.GetOrdinal("NurseName")),
                    ShiftName = r.GetString(r.GetOrdinal("ShiftName")),
                    ShiftColorHex = r.GetString(r.GetOrdinal("ShiftColorHex")),
                });
            }
            return list;
        }

        // ===================== ALGORITHM LOGS =====================
        public void AddAlgorithmLog(AlgorithmLog log)
        {
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO AlgorithmLogs (ScheduleId,Generation,BestFitness,AvgFitness,ViolationCount,
                                HardViolations,SoftViolations,LogMessage)
                                VALUES (@sid,@gen,@bf,@af,@vc,@hv,@sv,@msg)";
            cmd.Parameters.AddWithValue("@sid", log.ScheduleId);
            cmd.Parameters.AddWithValue("@gen", (object?)log.Generation ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@bf", (object?)log.BestFitness ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@af", (object?)log.AvgFitness ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@vc", (object?)log.ViolationCount ?? DBNull.Value);
            cmd.Parameters.AddWithValue("@hv", log.HardViolations);
            cmd.Parameters.AddWithValue("@sv", log.SoftViolations);
            cmd.Parameters.AddWithValue("@msg", (object?)log.LogMessage ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }

        public List<AlgorithmLog> GetLogsBySchedule(int scheduleId)
        {
            var list = new List<AlgorithmLog>();
            using var conn = OpenConnection();
            var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT * FROM AlgorithmLogs WHERE ScheduleId=@sid ORDER BY LogTime";
            cmd.Parameters.AddWithValue("@sid", scheduleId);
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new AlgorithmLog
                {
                    Id = r.GetInt32(r.GetOrdinal("Id")),
                    ScheduleId = r.GetInt32(r.GetOrdinal("ScheduleId")),
                    Generation = r.IsDBNull(r.GetOrdinal("Generation")) ? null : r.GetInt32(r.GetOrdinal("Generation")),
                    BestFitness = r.IsDBNull(r.GetOrdinal("BestFitness")) ? null : r.GetDouble(r.GetOrdinal("BestFitness")),
                    ViolationCount = r.IsDBNull(r.GetOrdinal("ViolationCount")) ? null : r.GetInt32(r.GetOrdinal("ViolationCount")),
                    HardViolations = r.GetInt32(r.GetOrdinal("HardViolations")),
                    SoftViolations = r.GetInt32(r.GetOrdinal("SoftViolations")),
                    LogMessage = r.IsDBNull(r.GetOrdinal("LogMessage")) ? null : r.GetString(r.GetOrdinal("LogMessage")),
                });
            }
            return list;
        }

        // ===================== DASHBOARD STATS =====================
        public (int nurses, int units, int subUnits, int shifts, int activeRules, Schedule? lastSchedule) GetDashboardStats()
        {
            using var conn = OpenConnection();
            int nurses = 0, units = 0, subUnits = 0, shifts = 0, activeRules = 0;
            Schedule? lastSchedule = null;

            var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                SELECT COUNT(*) FROM Nurses WHERE IsActive=1;
                SELECT COUNT(*) FROM Units WHERE IsActive=1;
                SELECT COUNT(*) FROM SubUnits WHERE IsActive=1;
                SELECT COUNT(*) FROM ShiftDefinitions WHERE IsActive=1;
                SELECT COUNT(*) FROM ScheduleRules WHERE IsActive=1;
            ";
            // Execute individually
            cmd.CommandText = "SELECT COUNT(*) FROM Nurses WHERE IsActive=1";
            nurses = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM Units WHERE IsActive=1";
            units = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM SubUnits WHERE IsActive=1";
            subUnits = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM ShiftDefinitions WHERE IsActive=1";
            shifts = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT COUNT(*) FROM ScheduleRules WHERE IsActive=1";
            activeRules = Convert.ToInt32(cmd.ExecuteScalar());
            cmd.CommandText = "SELECT * FROM Schedules ORDER BY CreatedAt DESC LIMIT 1";
            using var r = cmd.ExecuteReader();
            if (r.Read())
                lastSchedule = MapSchedule(r);

            return (nurses, units, subUnits, shifts, activeRules, lastSchedule);
        }

        public string GetDatabasePath() => _dbPath;
    }
}
