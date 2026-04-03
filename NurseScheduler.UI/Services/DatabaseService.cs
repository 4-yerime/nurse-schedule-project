using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using NurseScheduler.UI.Models;

namespace NurseScheduler.UI.Services
{
    public class DatabaseService
    {
        private readonly string _connectionString;

        public DatabaseService(string databaseFilePath)
        {
            _connectionString = $"Data Source={databaseFilePath};Version=3;";
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            var createTableCmd = connection.CreateCommand();
            createTableCmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Nurses (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    FirstName TEXT NOT NULL,
                    LastName TEXT NOT NULL,
                    SubUnitId INTEGER NOT NULL,
                    IsHeadNurse INTEGER NOT NULL,
                    EmploymentType TEXT NOT NULL,
                    MaxMonthlyHours REAL NOT NULL,
                    AnnualLeaveBalance INTEGER NOT NULL
                );";
            createTableCmd.ExecuteNonQuery();
        }

        public List<Nurse> GetAllNurses()
        {
            var nurses = new List<Nurse>();

            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = "SELECT Id, FirstName, LastName, SubUnitId, IsHeadNurse, EmploymentType, MaxMonthlyHours, AnnualLeaveBalance FROM Nurses";

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                nurses.Add(new Nurse
                {
                    Id = reader.GetInt32(0),
                    FirstName = reader.GetString(1),
                    LastName = reader.GetString(2),
                    SubUnitId = reader.GetInt32(3),
                    IsHeadNurse = reader.GetInt32(4) == 1,
                    EmploymentType = reader.GetString(5),
                    MaxMonthlyHours = reader.GetDouble(6),
                    AnnualLeaveBalance = reader.GetInt32(7)
                });
            }

            return nurses;
        }

        public void AddNurse(Nurse nurse)
        {
            using var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var cmd = connection.CreateCommand();
            cmd.CommandText = @"
                INSERT INTO Nurses (FirstName, LastName, SubUnitId, IsHeadNurse, EmploymentType, MaxMonthlyHours, AnnualLeaveBalance)
                VALUES (@FirstName, @LastName, @SubUnitId, @IsHeadNurse, @EmploymentType, @MaxMonthlyHours, @AnnualLeaveBalance);";

            cmd.Parameters.AddWithValue("@FirstName", nurse.FirstName);
            cmd.Parameters.AddWithValue("@LastName", nurse.LastName);
            cmd.Parameters.AddWithValue("@SubUnitId", nurse.SubUnitId);
            cmd.Parameters.AddWithValue("@IsHeadNurse", nurse.IsHeadNurse ? 1 : 0);
            cmd.Parameters.AddWithValue("@EmploymentType", nurse.EmploymentType);
            cmd.Parameters.AddWithValue("@MaxMonthlyHours", nurse.MaxMonthlyHours);
            cmd.Parameters.AddWithValue("@AnnualLeaveBalance", nurse.AnnualLeaveBalance);

            cmd.ExecuteNonQuery();
        }
    }
}