using System.Globalization;
using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Repositories;

public interface IProduitRepository
{
    Produit? GetById(int id);
    Produit? GetByReference(string reference);
    List<Produit> GetAll(bool seulementActifs = true);
    List<Produit> Search(string termeRecherche);
    List<Produit> GetProduitsStockBas();
    List<ProduitStockResume> GetSyntheseStocks();
    List<string> GetCategories();
    bool HasMouvements(int produitId);
    int Add(Produit produit);
    bool Update(Produit produit);
    bool SetActif(int id, bool actif);
    bool Delete(int id);
}

public class ProduitRepository : IProduitRepository
{
    private readonly DatabaseConnection _db;

    public ProduitRepository(DatabaseConnection? db = null)
    {
        _db = db ?? DatabaseConnection.Instance;
    }

    public Produit? GetById(int id)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, reference_unique, designation, categorie, dosage, forme, unite_vente, prix_vente, seuil_stock_min, actif, date_creation FROM produits WHERE id = @id;";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapProduit(reader) : null;
    }

    public Produit? GetByReference(string reference)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT id, reference_unique, designation, categorie, dosage, forme, unite_vente, prix_vente, seuil_stock_min, actif, date_creation FROM produits WHERE reference_unique = @ref COLLATE NOCASE;";
        cmd.Parameters.AddWithValue("@ref", reference);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapProduit(reader) : null;
    }

    public List<Produit> GetAll(bool seulementActifs = true)
    {
        var list = new List<Produit>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = seulementActifs
            ? "SELECT id, reference_unique, designation, categorie, dosage, forme, unite_vente, prix_vente, seuil_stock_min, actif, date_creation FROM produits WHERE actif = 1 ORDER BY designation ASC;"
            : "SELECT id, reference_unique, designation, categorie, dosage, forme, unite_vente, prix_vente, seuil_stock_min, actif, date_creation FROM produits ORDER BY designation ASC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapProduit(reader));
        }
        return list;
    }

    public List<Produit> Search(string termeRecherche)
    {
        var list = new List<Produit>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT id, reference_unique, designation, categorie, dosage, forme, unite_vente, prix_vente, seuil_stock_min, actif, date_creation 
            FROM produits 
            WHERE actif = 1 AND (designation LIKE @q OR reference_unique LIKE @q OR categorie LIKE @q)
            ORDER BY designation ASC;
        ";
        cmd.Parameters.AddWithValue("@q", $"%{termeRecherche}%");

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapProduit(reader));
        }
        return list;
    }

    public List<Produit> GetProduitsStockBas()
    {
        var list = new List<Produit>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.id, p.reference_unique, p.designation, p.categorie, p.dosage, p.forme, p.unite_vente, p.prix_vente, p.seuil_stock_min, p.actif, p.date_creation
            FROM produits p
            LEFT JOIN (
                SELECT produit_id, SUM(quantite_actuelle) AS total_stock
                FROM lots_stocks
                WHERE statut = 'Normal' AND date_peremption > date('now')
                GROUP BY produit_id
            ) s ON s.produit_id = p.id
            WHERE p.actif = 1 AND COALESCE(s.total_stock, 0) <= p.seuil_stock_min
            ORDER BY p.designation ASC;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapProduit(reader));
        }
        return list;
    }

    public List<ProduitStockResume> GetSyntheseStocks()
    {
        var list = new List<ProduitStockResume>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT p.id, p.reference_unique, p.designation, p.categorie, p.prix_vente, p.seuil_stock_min, p.actif,
                   COALESCE(s.total_stock, 0) AS stock_disponible
            FROM produits p
            LEFT JOIN (
                SELECT produit_id, SUM(quantite_actuelle) AS total_stock
                FROM lots_stocks
                WHERE statut = 'Normal' AND date(date_peremption) >= date('now')
                GROUP BY produit_id
            ) s ON s.produit_id = p.id
            WHERE p.actif = 1
            ORDER BY stock_disponible ASC, p.designation ASC;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            decimal prix = 0m;
            if (!reader.IsDBNull(4))
            {
                var raw = reader.GetValue(4).ToString();
                decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out prix);
            }

            list.Add(new ProduitStockResume
            {
                ProduitId = reader.GetInt32(0),
                ReferenceUnique = reader.GetString(1),
                Designation = reader.GetString(2),
                Categorie = reader.GetString(3),
                PrixVente = prix,
                SeuilStockMin = reader.GetInt32(5),
                Actif = reader.GetInt32(6) == 1,
                StockDisponible = Convert.ToInt32(reader.GetInt64(7))
            });
        }
        return list;
    }

    public int Add(Produit produit)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            INSERT INTO produits (reference_unique, designation, categorie, dosage, forme, unite_vente, prix_vente, seuil_stock_min, actif, date_creation)
            VALUES (@ref, @desig, @cat, @dosage, @forme, @unite, @prix, @seuil, @actif, @dateCreation);
            SELECT last_insert_rowid();
        ";
        cmd.Parameters.AddWithValue("@ref", produit.ReferenceUnique);
        cmd.Parameters.AddWithValue("@desig", produit.Designation);
        cmd.Parameters.AddWithValue("@cat", produit.Categorie);
        cmd.Parameters.AddWithValue("@dosage", (object?)produit.Dosage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@forme", (object?)produit.Forme ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@unite", produit.UniteVente);
        cmd.Parameters.AddWithValue("@prix", produit.PrixVente.ToString(CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@seuil", produit.SeuilStockMin);
        cmd.Parameters.AddWithValue("@actif", produit.Actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@dateCreation", produit.DateCreation.ToString("yyyy-MM-dd HH:mm:ss"));

        long id = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        produit.Id = (int)id;
        return produit.Id;
    }

    public bool Update(Produit produit)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            UPDATE produits 
            SET reference_unique = @ref, designation = @desig, categorie = @cat, 
                dosage = @dosage, forme = @forme, unite_vente = @unite, 
                prix_vente = @prix, seuil_stock_min = @seuil, actif = @actif
            WHERE id = @id;
        ";
        cmd.Parameters.AddWithValue("@ref", produit.ReferenceUnique);
        cmd.Parameters.AddWithValue("@desig", produit.Designation);
        cmd.Parameters.AddWithValue("@cat", produit.Categorie);
        cmd.Parameters.AddWithValue("@dosage", (object?)produit.Dosage ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@forme", (object?)produit.Forme ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@unite", produit.UniteVente);
        cmd.Parameters.AddWithValue("@prix", produit.PrixVente.ToString(CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("@seuil", produit.SeuilStockMin);
        cmd.Parameters.AddWithValue("@actif", produit.Actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", produit.Id);

        return cmd.ExecuteNonQuery() > 0;
    }

    public List<string> GetCategories()
    {
        var list = new List<string>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT DISTINCT categorie FROM produits WHERE categorie IS NOT NULL AND categorie != '' ORDER BY categorie ASC;";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(reader.GetString(0));
        }
        return list;
    }

    public bool HasMouvements(int produitId)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT (
                (SELECT COUNT(*) FROM mouvements_stock WHERE produit_id = @id) +
                (SELECT COUNT(*) FROM lots_stocks WHERE produit_id = @id)
            );
        ";
        cmd.Parameters.AddWithValue("@id", produitId);

        long count = Convert.ToInt64(cmd.ExecuteScalar() ?? 0);
        return count > 0;
    }

    public bool SetActif(int id, bool actif)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE produits SET actif = @actif WHERE id = @id;";
        cmd.Parameters.AddWithValue("@actif", actif ? 1 : 0);
        cmd.Parameters.AddWithValue("@id", id);
        return cmd.ExecuteNonQuery() > 0;
    }

    public bool Delete(int id)
    {
        return SetActif(id, false);
    }

    private static Produit MapProduit(SqliteDataReader reader)
    {
        decimal prix = 0m;
        if (!reader.IsDBNull(7))
        {
            var raw = reader.GetValue(7).ToString();
            decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out prix);
        }

        return new Produit
        {
            Id = reader.GetInt32(0),
            ReferenceUnique = reader.GetString(1),
            Designation = reader.GetString(2),
            Categorie = reader.GetString(3),
            Dosage = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
            Forme = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
            UniteVente = reader.GetString(6),
            PrixVente = prix,
            SeuilStockMin = reader.GetInt32(8),
            Actif = reader.GetInt32(9) == 1,
            DateCreation = DateTime.TryParse(reader.GetString(10), out var dt) ? dt : DateTime.UtcNow
        };
    }
}
