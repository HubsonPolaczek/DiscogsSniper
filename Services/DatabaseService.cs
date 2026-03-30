using System.Collections.Generic;
using System.Data;
using System.Linq;
using Dapper;
using Microsoft.Data.Sqlite;
using DiscogsSniper.Models;

namespace DiscogsSniper.Services
{
    public class DatabaseService
    {
        // Nazwa pliku naszej bazy danych (stworzy się w folderze z programem)
        private const string DbName = "discogs_sniper.db";
        private readonly string _connectionString = $"Data Source={DbName}";

        public DatabaseService()
        {
            // Przy każdym utworzeniu tej usługi, upewniamy się, że tabele istnieją
            InitializeDatabase();
        }

        private void InitializeDatabase()
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                // Tabela wytwórni (Labels)
                string createLabelsTable = @"
                    CREATE TABLE IF NOT EXISTS Labels (
                        Id INTEGER PRIMARY KEY,
                        Name TEXT NOT NULL,
                        IsActive INTEGER DEFAULT 1
                    );";
                db.Execute(createLabelsTable);

                // Tabela zapamiętanych ofert (żeby nie przetwarzać 2 razy tego samego ogłoszenia)
                string createSeenOffersTable = @"
                    CREATE TABLE IF NOT EXISTS SeenOffers (
                        ListingId INTEGER PRIMARY KEY,
                        DateSeen TEXT NOT NULL
                    );";
                db.Execute(createSeenOffersTable);
            }
        }

        // ==========================================
        // METODY DO OBSŁUGI WYTWÓRNI (LABELS)
        // ==========================================

        public List<Label> GetLabels()
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                // Dapper sam dopasuje kolumny z bazy do właściwości w naszej klasie Label!
                return db.Query<Label>("SELECT * FROM Labels").ToList();
            }
        }

        public void AddLabel(Label label)
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                string sql = "INSERT OR IGNORE INTO Labels (Id, Name, IsActive) VALUES (@Id, @Name, @IsActive)";
                db.Execute(sql, label);
            }
        }

        public void DeleteLabel(int id)
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                db.Execute("DELETE FROM Labels WHERE Id = @Id", new { Id = id });
            }
        }

        public void UpdateLabelActiveState(int id, bool isActive)
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                // Zapisuje 1 (ON) lub 0 (OFF) w bazie danych
                db.Execute("UPDATE Labels SET IsActive = @IsActive WHERE Id = @Id", new { IsActive = isActive ? 1 : 0, Id = id });
            }
        }

        // ==========================================
        // METODY DO OBSŁUGI SKANERA
        // ==========================================

        public bool IsOfferAlreadySeen(long listingId)
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                int count = db.QuerySingle<int>("SELECT COUNT(*) FROM SeenOffers WHERE ListingId = @ListingId", new { ListingId = listingId });
                return count > 0;
            }
        }

        public void MarkOfferAsSeen(long listingId)
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                string sql = "INSERT OR IGNORE INTO SeenOffers (ListingId, DateSeen) VALUES (@ListingId, datetime('now'))";
                db.Execute(sql, new { ListingId = listingId });
            }
        }
        public void ClearSeenOffers()
        {
            using (IDbConnection db = new SqliteConnection(_connectionString))
            {
                db.Execute("DELETE FROM SeenOffers");
            }
        }
    }

}