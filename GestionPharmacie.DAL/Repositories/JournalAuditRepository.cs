using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Repositories;

public interface IJournalAuditRepository
{
    List<JournalAudit> GetAll(int limit = 100);
    List<JournalAudit> GetByUtilisateur(int utilisateurId);
    int EnregistrerAction(int? utilisateurId, string action, string tableConcernee, int? entiteId, string details, SqliteTransaction? transaction = null);
}

public class JournalAuditRepository : IJournalAuditRepository
{
    private readonly DatabaseConnection _db;

    public JournalAuditRepository(DatabaseConnection? db = null)
    {
        _db = db ?? DatabaseConnection.Instance;
    }

    public List<JournalAudit> GetAll(int limit = 100)
    {
        var list = new List<JournalAudit>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.id, a.utilisateur_id, a.action, a.table_concernee, a.entite_id, a.details, a.date_action,
                   u.nom AS nom_utilisateur
            FROM journal_audit a
            LEFT JOIN utilisateurs u ON u.id = a.utilisateur_id
            ORDER BY a.date_action DESC
            LIMIT @limit;
        ";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapAudit(reader));
        }
        return list;
    }

    public List<JournalAudit> GetByUtilisateur(int utilisateurId)
    {
        var list = new List<JournalAudit>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT a.id, a.utilisateur_id, a.action, a.table_concernee, a.entite_id, a.details, a.date_action,
                   u.nom AS nom_utilisateur
            FROM journal_audit a
            LEFT JOIN utilisateurs u ON u.id = a.utilisateur_id
            WHERE a.utilisateur_id = @userId
            ORDER BY a.date_action DESC;
        ";
        cmd.Parameters.AddWithValue("@userId", utilisateurId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapAudit(reader));
        }
        return list;
    }

    public int EnregistrerAction(int? utilisateurId, string action, string tableConcernee, int? entiteId, string details, SqliteTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO journal_audit (utilisateur_id, action, table_concernee, entite_id, details, date_action)
            VALUES (@userId, @action, @table, @entiteId, @details, datetime('now'));
            SELECT last_insert_rowid();
        ";

        var parameters = new Dictionary<string, object?>
        {
            { "userId", utilisateurId },
            { "action", action },
            { "table", tableConcernee },
            { "entiteId", entiteId },
            { "details", details }
        };

        long id = Convert.ToInt64(_db.ExecuteScalar(sql, parameters, transaction) ?? 0);
        return (int)id;
    }

    private static JournalAudit MapAudit(SqliteDataReader reader)
    {
        return new JournalAudit
        {
            Id = reader.GetInt32(0),
            UtilisateurId = reader.IsDBNull(1) ? null : reader.GetInt32(1),
            Action = reader.GetString(2),
            TableConcernee = reader.GetString(3),
            EntiteId = reader.IsDBNull(4) ? null : reader.GetInt32(4),
            Details = reader.GetString(5),
            DateAction = DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.UtcNow,
            NomUtilisateur = reader.IsDBNull(7) ? null : reader.GetString(7)
        };
    }
}
