using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Repositories;

public interface IFournisseurRepository
{
    Fournisseur? GetById(int id);
    List<Fournisseur> GetAll(bool seulementActifs = true);
    List<Fournisseur> Search(string query);
    int Add(Fournisseur fournisseur);
    bool Update(Fournisseur fournisseur);
    bool SetActif(int id, bool actif);
    bool Delete(int id);
}

public class FournisseurRepository : IFournisseurRepository
{
    private readonly DatabaseConnection _db;

    public FournisseurRepository(DatabaseConnection? db = null)
    {
        _db = db ?? DatabaseConnection.Instance;
    }

    public Fournisseur? GetById(int id)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, nom, telephone, adresse, email, actif, date_creation FROM fournisseurs WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapFournisseur(reader) : null;
    }

    public List<Fournisseur> GetAll(bool seulementActifs = true)
    {
        var list = new List<Fournisseur>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = seulementActifs
            ? "SELECT id, nom, telephone, adresse, email, actif, date_creation FROM fournisseurs WHERE actif = 1 ORDER BY nom ASC;"
            : "SELECT id, nom, telephone, adresse, email, actif, date_creation FROM fournisseurs ORDER BY nom ASC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapFournisseur(reader));
        }
        return list;
    }

    public int Add(Fournisseur fournisseur)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO fournisseurs (nom, telephone, adresse, email, actif, date_creation)
            VALUES (@nom, @tel, @adr, @email, @actif, @dateCreation);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@nom", fournisseur.Nom);
        cmd.Parameters.AddWithValue("@tel", (object?)fournisseur.Telephone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@adr", (object?)fournisseur.Adresse ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email", (object?)fournisseur.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@actif", fournisseur.Actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@dateCreation", fournisseur.DateCreation.ToString("yyyy-MM-dd HH:mm:ss"));

        long id = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        fournisseur.Id = (int)id;
        return fournisseur.Id;
    }

    public bool Update(Fournisseur fournisseur)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE fournisseurs 
            SET nom = @nom, telephone = @tel, adresse = @adr, email = @email, actif = @actif 
            WHERE id = @id;
        ";
        cmd.Parameters.AddWithValue("@nom", fournisseur.Nom);
        cmd.Parameters.AddWithValue("@tel", (object?)fournisseur.Telephone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@adr", (object?)fournisseur.Adresse ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@email", (object?)fournisseur.Email ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@actif", fournisseur.Actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", fournisseur.Id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public List<Fournisseur> Search(string query)
    {
        var list = new List<Fournisseur>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, nom, telephone, adresse, email, actif, date_creation 
            FROM fournisseurs 
            WHERE (nom LIKE @q OR email LIKE @q OR telephone LIKE @q OR adresse LIKE @q)
            ORDER BY nom ASC;
        ";
        cmd.Parameters.AddWithValue("@q", $"%{query}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapFournisseur(reader));
        }
        return list;
    }

    public bool SetActif(int id, bool actif)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE fournisseurs SET actif = @actif WHERE id = @id;";
        cmd.Parameters.AddWithValue("@actif", actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool Delete(int id)
    {
        return SetActif(id, false);
    }

    private static Fournisseur MapFournisseur(SqliteDataReader reader)
    {
        return new Fournisseur
        {
            Id = reader.GetInt32(0),
            Nom = reader.GetString(1),
            Telephone = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
            Adresse = reader.IsDBNull(3) ? string.Empty : reader.GetString(3),
            Email = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            Actif = reader.GetInt32(5) == 1,
            DateCreation = DateTime.TryParse(reader.GetString(6), out var dt) ? dt : DateTime.UtcNow
        };
    }
}
