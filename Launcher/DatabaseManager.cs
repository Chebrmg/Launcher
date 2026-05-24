using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Data.Sqlite;

namespace Launcher
{
    public class UserAccount
    {
        public int Id { get; set; }
        public string Username { get; set; } = "";
        public bool IsAdmin { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class DuelRecord
    {
        public int Id { get; set; }
        public int Player1Id { get; set; }
        public int Player2Id { get; set; }
        public string Player1Name { get; set; } = "";
        public string Player2Name { get; set; } = "";
        public DateTime PlayedAt { get; set; }

        public string Player1Faction { get; set; } = "";
        public string Player2Faction { get; set; } = "";
        public string Player1Hero { get; set; } = "";
        public string Player2Hero { get; set; } = "";
        public int Player1Level { get; set; }
        public int Player2Level { get; set; }
    }

    public class DuelHeroSnapshot
    {
        public int DuelId { get; set; }
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = "";
        public string Faction { get; set; } = "";
        public string HeroName { get; set; } = "";
        public int HeroLevel { get; set; }
        public int Offence { get; set; }
        public int Defence { get; set; }
        public int Spellpower { get; set; }
        public int Knowledge { get; set; }
        public string SkillsJson { get; set; } = "[]";
        public string PerksJson { get; set; } = "[]";
        public string SpellsJson { get; set; } = "[]";
        public string ArmyJson { get; set; } = "[]";
        public string ArtifactsJson { get; set; } = "[]";
    }

    public static class DatabaseManager
    {
        private static string _dbPath = "";

        public static void Init(string appDirectory)
        {
            _dbPath = Path.Combine(appDirectory, "launcher.db");
            EnsureSchema();
            EnsureAdminExists();
        }

        private static SqliteConnection Open()
        {
            var conn = new SqliteConnection($"Data Source={_dbPath}");
            conn.Open();
            return conn;
        }

        private static void EnsureSchema()
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"
                CREATE TABLE IF NOT EXISTS Users (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Username TEXT NOT NULL UNIQUE COLLATE NOCASE,
                    PasswordHash TEXT NOT NULL,
                    IsAdmin INTEGER NOT NULL DEFAULT 0,
                    CreatedAt TEXT NOT NULL DEFAULT (datetime('now'))
                );

                CREATE TABLE IF NOT EXISTS Duels (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Player1Id INTEGER NOT NULL,
                    Player2Id INTEGER NOT NULL,
                    PlayedAt TEXT NOT NULL DEFAULT (datetime('now')),
                    FOREIGN KEY (Player1Id) REFERENCES Users(Id),
                    FOREIGN KEY (Player2Id) REFERENCES Users(Id)
                );

                CREATE TABLE IF NOT EXISTS DuelHeroes (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    DuelId INTEGER NOT NULL,
                    PlayerId INTEGER NOT NULL,
                    Faction TEXT NOT NULL,
                    HeroName TEXT NOT NULL,
                    HeroLevel INTEGER NOT NULL DEFAULT 1,
                    Offence INTEGER NOT NULL DEFAULT 0,
                    Defence INTEGER NOT NULL DEFAULT 0,
                    Spellpower INTEGER NOT NULL DEFAULT 0,
                    Knowledge INTEGER NOT NULL DEFAULT 0,
                    SkillsJson TEXT NOT NULL DEFAULT '[]',
                    PerksJson TEXT NOT NULL DEFAULT '[]',
                    SpellsJson TEXT NOT NULL DEFAULT '[]',
                    ArmyJson TEXT NOT NULL DEFAULT '[]',
                    ArtifactsJson TEXT NOT NULL DEFAULT '[]',
                    FOREIGN KEY (DuelId) REFERENCES Duels(Id),
                    FOREIGN KEY (PlayerId) REFERENCES Users(Id)
                );
            ";
            cmd.ExecuteNonQuery();
        }

        private static void EnsureAdminExists()
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT COUNT(*) FROM Users WHERE IsAdmin = 1";
            long count = (long)(cmd.ExecuteScalar() ?? 0);
            if (count == 0)
            {
                using var ins = conn.CreateCommand();
                ins.CommandText = "INSERT INTO Users (Username, PasswordHash, IsAdmin) VALUES (@u, @p, 1)";
                ins.Parameters.AddWithValue("@u", "admin");
                ins.Parameters.AddWithValue("@p", HashPassword("admin"));
                ins.ExecuteNonQuery();
            }
        }

        private static string HashPassword(string password)
        {
            using var sha = SHA256.Create();
            var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(password));
            return Convert.ToBase64String(bytes);
        }

        public static UserAccount? Authenticate(string username, string password)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, IsAdmin, CreatedAt FROM Users WHERE Username = @u AND PasswordHash = @p";
            cmd.Parameters.AddWithValue("@u", username);
            cmd.Parameters.AddWithValue("@p", HashPassword(password));
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new UserAccount
            {
                Id = r.GetInt32(0),
                Username = r.GetString(1),
                IsAdmin = r.GetInt32(2) == 1,
                CreatedAt = DateTime.Parse(r.GetString(3)),
            };
        }

        public static (bool ok, string error) Register(string username, string password)
        {
            if (string.IsNullOrWhiteSpace(username) || username.Length < 2)
                return (false, "Имя пользователя: минимум 2 символа");
            if (string.IsNullOrWhiteSpace(password) || password.Length < 3)
                return (false, "Пароль: минимум 3 символа");

            try
            {
                using var conn = Open();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "INSERT INTO Users (Username, PasswordHash) VALUES (@u, @p)";
                cmd.Parameters.AddWithValue("@u", username.Trim());
                cmd.Parameters.AddWithValue("@p", HashPassword(password));
                cmd.ExecuteNonQuery();
                return (true, "");
            }
            catch (SqliteException ex) when (ex.SqliteErrorCode == 19)
            {
                return (false, "Пользователь с таким именем уже существует");
            }
        }

        public static int SaveDuel(int player1Id, int player2Id)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "INSERT INTO Duels (Player1Id, Player2Id) VALUES (@p1, @p2); SELECT last_insert_rowid();";
            cmd.Parameters.AddWithValue("@p1", player1Id);
            cmd.Parameters.AddWithValue("@p2", player2Id);
            return Convert.ToInt32(cmd.ExecuteScalar());
        }

        public static void SaveDuelHero(DuelHeroSnapshot snap)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"INSERT INTO DuelHeroes 
                (DuelId, PlayerId, Faction, HeroName, HeroLevel, Offence, Defence, Spellpower, Knowledge,
                 SkillsJson, PerksJson, SpellsJson, ArmyJson, ArtifactsJson)
                VALUES (@did, @pid, @fac, @hero, @lvl, @off, @def, @sp, @kn,
                        @skills, @perks, @spells, @army, @arts)";
            cmd.Parameters.AddWithValue("@did", snap.DuelId);
            cmd.Parameters.AddWithValue("@pid", snap.PlayerId);
            cmd.Parameters.AddWithValue("@fac", snap.Faction);
            cmd.Parameters.AddWithValue("@hero", snap.HeroName);
            cmd.Parameters.AddWithValue("@lvl", snap.HeroLevel);
            cmd.Parameters.AddWithValue("@off", snap.Offence);
            cmd.Parameters.AddWithValue("@def", snap.Defence);
            cmd.Parameters.AddWithValue("@sp", snap.Spellpower);
            cmd.Parameters.AddWithValue("@kn", snap.Knowledge);
            cmd.Parameters.AddWithValue("@skills", snap.SkillsJson);
            cmd.Parameters.AddWithValue("@perks", snap.PerksJson);
            cmd.Parameters.AddWithValue("@spells", snap.SpellsJson);
            cmd.Parameters.AddWithValue("@army", snap.ArmyJson);
            cmd.Parameters.AddWithValue("@arts", snap.ArtifactsJson);
            cmd.ExecuteNonQuery();
        }

        public static List<DuelRecord> GetDuels(int? playerId = null)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            string where = playerId.HasValue
                ? "WHERE d.Player1Id = @pid OR d.Player2Id = @pid"
                : "";
            cmd.CommandText = $@"
                SELECT d.Id, d.Player1Id, d.Player2Id, d.PlayedAt,
                       u1.Username, u2.Username,
                       h1.Faction, h2.Faction,
                       h1.HeroName, h2.HeroName,
                       h1.HeroLevel, h2.HeroLevel
                FROM Duels d
                JOIN Users u1 ON d.Player1Id = u1.Id
                JOIN Users u2 ON d.Player2Id = u2.Id
                LEFT JOIN DuelHeroes h1 ON h1.DuelId = d.Id AND h1.PlayerId = d.Player1Id
                LEFT JOIN DuelHeroes h2 ON h2.DuelId = d.Id AND h2.PlayerId = d.Player2Id
                {where}
                ORDER BY d.PlayedAt DESC";
            if (playerId.HasValue)
                cmd.Parameters.AddWithValue("@pid", playerId.Value);

            var list = new List<DuelRecord>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new DuelRecord
                {
                    Id = r.GetInt32(0),
                    Player1Id = r.GetInt32(1),
                    Player2Id = r.GetInt32(2),
                    PlayedAt = DateTime.Parse(r.GetString(3)),
                    Player1Name = r.GetString(4),
                    Player2Name = r.GetString(5),
                    Player1Faction = r.IsDBNull(6) ? "" : r.GetString(6),
                    Player2Faction = r.IsDBNull(7) ? "" : r.GetString(7),
                    Player1Hero = r.IsDBNull(8) ? "" : r.GetString(8),
                    Player2Hero = r.IsDBNull(9) ? "" : r.GetString(9),
                    Player1Level = r.IsDBNull(10) ? 0 : r.GetInt32(10),
                    Player2Level = r.IsDBNull(11) ? 0 : r.GetInt32(11),
                });
            }
            return list;
        }

        public static DuelHeroSnapshot? GetDuelHero(int duelId, int playerId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"SELECT dh.DuelId, dh.PlayerId, u.Username, dh.Faction, dh.HeroName, dh.HeroLevel,
                                       dh.Offence, dh.Defence, dh.Spellpower, dh.Knowledge,
                                       dh.SkillsJson, dh.PerksJson, dh.SpellsJson, dh.ArmyJson, dh.ArtifactsJson
                                FROM DuelHeroes dh
                                JOIN Users u ON dh.PlayerId = u.Id
                                WHERE dh.DuelId = @did AND dh.PlayerId = @pid";
            cmd.Parameters.AddWithValue("@did", duelId);
            cmd.Parameters.AddWithValue("@pid", playerId);
            using var r = cmd.ExecuteReader();
            if (!r.Read()) return null;
            return new DuelHeroSnapshot
            {
                DuelId = r.GetInt32(0),
                PlayerId = r.GetInt32(1),
                PlayerName = r.GetString(2),
                Faction = r.GetString(3),
                HeroName = r.GetString(4),
                HeroLevel = r.GetInt32(5),
                Offence = r.GetInt32(6),
                Defence = r.GetInt32(7),
                Spellpower = r.GetInt32(8),
                Knowledge = r.GetInt32(9),
                SkillsJson = r.GetString(10),
                PerksJson = r.GetString(11),
                SpellsJson = r.GetString(12),
                ArmyJson = r.GetString(13),
                ArtifactsJson = r.GetString(14),
            };
        }

        public static List<UserAccount> GetAllUsers()
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "SELECT Id, Username, IsAdmin, CreatedAt FROM Users ORDER BY Username";
            var list = new List<UserAccount>();
            using var r = cmd.ExecuteReader();
            while (r.Read())
            {
                list.Add(new UserAccount
                {
                    Id = r.GetInt32(0),
                    Username = r.GetString(1),
                    IsAdmin = r.GetInt32(2) == 1,
                    CreatedAt = DateTime.Parse(r.GetString(3)),
                });
            }
            return list;
        }

        public static void SetAdmin(int userId, bool isAdmin)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "UPDATE Users SET IsAdmin = @a WHERE Id = @id";
            cmd.Parameters.AddWithValue("@a", isAdmin ? 1 : 0);
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.ExecuteNonQuery();
        }

        public static void DeleteUser(int userId)
        {
            using var conn = Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = @"DELETE FROM DuelHeroes WHERE DuelId IN 
                (SELECT Id FROM Duels WHERE Player1Id = @id OR Player2Id = @id)";
            cmd.Parameters.AddWithValue("@id", userId);
            cmd.ExecuteNonQuery();

            using var cmd2 = conn.CreateCommand();
            cmd2.CommandText = "DELETE FROM Duels WHERE Player1Id = @id OR Player2Id = @id";
            cmd2.Parameters.AddWithValue("@id", userId);
            cmd2.ExecuteNonQuery();

            using var cmd3 = conn.CreateCommand();
            cmd3.CommandText = "DELETE FROM Users WHERE Id = @id";
            cmd3.Parameters.AddWithValue("@id", userId);
            cmd3.ExecuteNonQuery();
        }
    }
}
