using Microsoft.Data.Sqlite;

namespace Claims;

public record Claim(long Id, string Provider, string Description, double Amount, string Status, string SubmittedAt);

// An in-memory copy of the claims database with sample data. In production this is SQL Server.
public static class Db
{
    public static SqliteConnection CreateSeeded()
    {
        var db = new SqliteConnection("Data Source=:memory:");
        db.Open();
        using var cmd = db.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE members (id INTEGER PRIMARY KEY, name TEXT);
            CREATE TABLE claims (id INTEGER PRIMARY KEY, member_id INTEGER, provider TEXT, description TEXT,
                                 amount REAL, status TEXT, submitted_at TEXT);
            INSERT INTO members VALUES (1, 'Ana Torres'), (2, 'Ben Carter');
            INSERT INTO claims VALUES
              (101, 1, 'Bright Smiles Dental', 'Dental cleaning',     120.00, 'paid',    '2026-09-02'),
              (102, 1, 'O''Brien Eye Clinic',  'Eye exam',             95.50, 'paid',    '2026-09-15'),
              (103, 1, 'Bright Smiles Dental', 'Dental X-ray',          80.00, 'pending', '2026-10-01'),
              (104, 1, 'City Pharmacy',        'Copay 100% covered',     0.00, 'paid',    '2026-10-05'),
              (105, 1, 'City Pharmacy',        'Copay 100 dollars',    100.00, 'denied',  '2026-10-20'),
              (201, 2, 'Bright Smiles Dental', 'Dental crown',         900.00, 'paid',    '2026-09-20');
            """;
        cmd.ExecuteNonQuery();
        return db;
    }
}
