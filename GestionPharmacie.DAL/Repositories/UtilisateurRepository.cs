using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Repositories;

public interface IUtilisateurRepository
{
    Utilisateur? GetById(int id);
    Utilisateur? GetByIdentifiant(string identifiant);
    List<Utilisateur> GetAll();
    int Add(Utilisateur utilisateur);
    bool Update(Utilisateur utilisateur);
    bool UpdateMotDePasse(int id, string motDePasseHash);
    bool SetActif(int id, bool actif);
}

public class UtilisateurRepository : IUtilisateurRepository
{
    private readonly DatabaseConnection _db;

    public UtilisateurRepository(DatabaseConnection? db = null)
    {
        _db = db ?? DatabaseConnection.Instance;
    }

    public Utilisateur? GetById(int id)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nom, identifiant, mot_de_passe_hash, role, actif, date_creation FROM utilisateurs WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapUtilisateur(reader) : null;
    }

    public Utilisateur? GetByIdentifiant(string identifiant)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nom, identifiant, mot_de_passe_hash, role, actif, date_creation FROM utilisateurs WHERE identifiant = @identifiant COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@identifiant", identifiant);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapUtilisateur(reader) : null;
    }

    public List<Utilisateur> GetAll()
    {
        var list = new List<Utilisateur>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nom, identifiant, mot_de_passe_hash, role, actif, date_creation FROM utilisateurs ORDER BY nom ASC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapUtilisateur(reader));
        }
        return list;
    }

    public int Add(Utilisateur utilisateur)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO utilisateurs (nom, identifiant, mot_de_passe_hash, role, actif, date_creation)
            VALUES (@nom, @identifiant, @motDePasseHash, @role, @actif, @dateCreation);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@nom", utilisateur.Nom);
        cmd.Parameters.AddWithValue("@identifiant", utilisateur.Identifiant);
        cmd.Parameters.AddWithValue("@motDePasseHash", utilisateur.MotDePasseHash);
        cmd.Parameters.AddWithValue("@role", utilisateur.Role.ToString());
        cmd.Parameters.AddWithValue("@actif", utilisateur.Actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@dateCreation", utilisateur.DateCreation.ToString("yyyy-MM-dd HH:mm:ss"));

        long id = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        utilisateur.Id = (int)id;
        return utilisateur.Id;
    }

    public bool Update(Utilisateur utilisateur)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE utilisateurs 
            SET nom = @nom, role = @role, actif = @actif 
            WHERE id = @id;
        ";
        cmd.Parameters.AddWithValue("@nom", utilisateur.Nom);
        cmd.Parameters.AddWithValue("@role", utilisateur.Role.ToString());
        cmd.Parameters.AddWithValue("@actif", utilisateur.Actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", utilisateur.Id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool UpdateMotDePasse(int id, string motDePasseHash)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE utilisateurs SET mot_de_passe_hash = @hash WHERE id = @id;";
        cmd.Parameters.AddWithValue("@hash", motDePasseHash);
        cmd.Parameters.AddWithValue("@id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public bool SetActif(int id, bool actif)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE utilisateurs SET actif = @actif WHERE id = @id;";
        cmd.Parameters.AddWithValue("@actif", actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);

        return cmd.ExecuteNonQuery() > 0;
    }

    private static Utilisateur MapUtilisateur(SqliteDataReader reader)
    {
        return new Utilisateur
        {
            Id = reader.GetInt32(0),
            Nom = reader.GetString(1),
            Identifiant = reader.GetString(2),
            MotDePasseHash = reader.GetString(3),
            Role = Enum.TryParse<RoleUtilisateur>(reader.GetString(4), out var role) ? role : RoleUtilisateur.Vendeur,
            Actif = reader.GetInt32(5) == 1,
            DateCreation = DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.UtcNow
        };
    }
}
