using System.Globalization;
using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Repositories;

public interface ILotStockRepository
{
    LotStock? GetById(int id);
    List<LotStock> GetAll();
    List<LotStock> GetLotsByProduit(int produitId);
    List<LotStock> GetLotsDisponiblesParFEFO(int produitId);
    List<LotStock> GetLotsProchesPeremption(int jours = 30);
    LotStock? GetByProduitEtNumeroLot(int produitId, string numeroLot, SqliteTransaction? transaction = null);
    int Add(LotStock lot, SqliteTransaction? transaction = null);
    bool Update(LotStock lot);
    bool DiminuerStock(int lotId, int quantite, SqliteTransaction? transaction = null);
    bool IncrementerStock(int lotId, int quantite, decimal? nouveauCoutAchat = null, SqliteTransaction? transaction = null);
    bool ChangerStatut(int lotId, StatutLot statut);
}

public class LotStockRepository : ILotStockRepository
{
    private readonly DatabaseConnection _db;

    public LotStockRepository(DatabaseConnection? db = null)
    {
        _db = db ?? DatabaseConnection.Instance;
    }

    public LotStock? GetById(int id)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT l.id, l.produit_id, l.fournisseur_id, l.numero_lot, l.date_peremption, 
                   l.quantite_initiale, l.quantite_actuelle, l.cout_achat, l.date_reception, l.statut,
                   p.designation AS nom_produit, f.nom AS nom_fournisseur
            FROM lots_stocks l
            INNER JOIN produits p ON p.id = l.produit_id
            LEFT JOIN fournisseurs f ON f.id = l.fournisseur_id
            WHERE l.id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        return reader.Read() ? MapLotStock(reader) : null;
    }

    public List<LotStock> GetAll()
    {
        var list = new List<LotStock>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT l.id, l.produit_id, l.fournisseur_id, l.numero_lot, l.date_peremption, 
                   l.quantite_initiale, l.quantite_actuelle, l.cout_achat, l.date_reception, l.statut,
                   p.designation AS nom_produit, f.nom AS nom_fournisseur
            FROM lots_stocks l
            INNER JOIN produits p ON p.id = l.produit_id
            LEFT JOIN fournisseurs f ON f.id = l.fournisseur_id
            ORDER BY l.date_peremption ASC;
        ";

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapLotStock(reader));
        }
        return list;
    }

    public List<LotStock> GetLotsByProduit(int produitId)
    {
        var list = new List<LotStock>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT l.id, l.produit_id, l.fournisseur_id, l.numero_lot, l.date_peremption, 
                   l.quantite_initiale, l.quantite_actuelle, l.cout_achat, l.date_reception, l.statut,
                   p.designation AS nom_produit, f.nom AS nom_fournisseur
            FROM lots_stocks l
            INNER JOIN produits p ON p.id = l.produit_id
            LEFT JOIN fournisseurs f ON f.id = l.fournisseur_id
            WHERE l.produit_id = @produitId
            ORDER BY l.date_peremption ASC;
        ";
        cmd.Parameters.AddWithValue("@produitId", produitId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapLotStock(reader));
        }
        return list;
    }

    public List<LotStock> GetLotsDisponiblesParFEFO(int produitId)
    {
        // Règle FEFO (First Expired First Out) : Lots actifs, non expirés, quantité > 0, triés par date de péremption
        var list = new List<LotStock>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT l.id, l.produit_id, l.fournisseur_id, l.numero_lot, l.date_peremption, 
                   l.quantite_initiale, l.quantite_actuelle, l.cout_achat, l.date_reception, l.statut,
                   p.designation AS nom_produit, f.nom AS nom_fournisseur
            FROM lots_stocks l
            INNER JOIN produits p ON p.id = l.produit_id
            LEFT JOIN fournisseurs f ON f.id = l.fournisseur_id
            WHERE l.produit_id = @produitId 
              AND l.statut = 'Normal' 
              AND l.quantite_actuelle > 0
              AND date(l.date_peremption) >= date('now')
            ORDER BY l.date_peremption ASC;
        ";
        cmd.Parameters.AddWithValue("@produitId", produitId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapLotStock(reader));
        }
        return list;
    }

    public List<LotStock> GetLotsProchesPeremption(int jours = 30)
    {
        var list = new List<LotStock>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT l.id, l.produit_id, l.fournisseur_id, l.numero_lot, l.date_peremption, 
                   l.quantite_initiale, l.quantite_actuelle, l.cout_achat, l.date_reception, l.statut,
                   p.designation AS nom_produit, f.nom AS nom_fournisseur
            FROM lots_stocks l
            INNER JOIN produits p ON p.id = l.produit_id
            LEFT JOIN fournisseurs f ON f.id = l.fournisseur_id
            WHERE l.quantite_actuelle > 0 
              AND date(l.date_peremption) <= date('now', '+' || @jours || ' days')
            ORDER BY l.date_peremption ASC;
        ";
        cmd.Parameters.AddWithValue("@jours", jours);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapLotStock(reader));
        }
        return list;
    }

    public int Add(LotStock lot, SqliteTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO lots_stocks (produit_id, fournisseur_id, numero_lot, date_peremption, 
                                     quantite_initiale, quantite_actuelle, cout_achat, date_reception, statut)
            VALUES (@produitId, @fournisseurId, @numLot, @peremption, 
                    @qteInit, @qteAct, @cout, @reception, @statut);
            SELECT last_insert_rowid();
        ";

        var parameters = new Dictionary<string, object?>
        {
            { "produitId", lot.ProduitId },
            { "fournisseurId", lot.FournisseurId },
            { "numLot", lot.NumeroLot },
            { "peremption", lot.DatePeremption.ToString("yyyy-MM-dd") },
            { "qteInit", lot.QuantiteInitiale },
            { "qteAct", lot.QuantiteActuelle },
            { "cout", lot.CoutAchat },
            { "reception", lot.DateReception.ToString("yyyy-MM-dd HH:mm:ss") },
            { "statut", lot.Statut.ToString() }
        };

        long id = Convert.ToInt64(_db.ExecuteScalar(sql, parameters, transaction) ?? 0);
        lot.Id = (int)id;
        return lot.Id;
    }

    public bool Update(LotStock lot)
    {
        const string sql = @"
            UPDATE lots_stocks 
            SET numero_lot = @numLot, date_peremption = @peremption, 
                quantite_actuelle = @qteAct, cout_achat = @cout, statut = @statut
            WHERE id = @id;
        ";

        var parameters = new Dictionary<string, object?>
        {
            { "numLot", lot.NumeroLot },
            { "peremption", lot.DatePeremption.ToString("yyyy-MM-dd") },
            { "qteAct", lot.QuantiteActuelle },
            { "cout", lot.CoutAchat },
            { "statut", lot.Statut.ToString() },
            { "id", lot.Id }
        };

        return _db.ExecuteNonQuery(sql, parameters) > 0;
    }

    public LotStock? GetByProduitEtNumeroLot(int produitId, string numeroLot, SqliteTransaction? transaction = null)
    {
        const string sql = @"
            SELECT l.id, l.produit_id, l.fournisseur_id, l.numero_lot, l.date_peremption, 
                   l.quantite_initiale, l.quantite_actuelle, l.cout_achat, l.date_reception, l.statut,
                   p.designation AS nom_produit, f.nom AS nom_fournisseur
            FROM lots_stocks l
            INNER JOIN produits p ON p.id = l.produit_id
            LEFT JOIN fournisseurs f ON f.id = l.fournisseur_id
            WHERE l.produit_id = @produitId AND l.numero_lot = @numLot COLLATE NOCASE;
        ";

        if (transaction != null)
        {
            using var cmd = transaction.Connection!.CreateCommand();
            cmd.Transaction = transaction;
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@produitId", produitId);
            cmd.Parameters.AddWithValue("@numLot", numeroLot);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapLotStock(reader) : null;
        }
        else
        {
            using var conn = _db.CreateOpenConnection();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@produitId", produitId);
            cmd.Parameters.AddWithValue("@numLot", numeroLot);
            using var reader = cmd.ExecuteReader();
            return reader.Read() ? MapLotStock(reader) : null;
        }
    }

    public bool DiminuerStock(int lotId, int quantite, SqliteTransaction? transaction = null)
    {
        const string sql = @"
            UPDATE lots_stocks 
            SET quantite_actuelle = quantite_actuelle - @qte 
            WHERE id = @id AND quantite_actuelle >= @qte;
        ";

        var parameters = new Dictionary<string, object?>
        {
            { "qte", quantite },
            { "id", lotId }
        };

        return _db.ExecuteNonQuery(sql, parameters, transaction) > 0;
    }

    public bool IncrementerStock(int lotId, int quantite, decimal? nouveauCoutAchat = null, SqliteTransaction? transaction = null)
    {
        string sql = nouveauCoutAchat.HasValue
            ? @"
                UPDATE lots_stocks 
                SET quantite_actuelle = quantite_actuelle + @qte,
                    quantite_initiale = quantite_initiale + @qte,
                    cout_achat = @cout
                WHERE id = @id;
              "
            : @"
                UPDATE lots_stocks 
                SET quantite_actuelle = quantite_actuelle + @qte,
                    quantite_initiale = quantite_initiale + @qte
                WHERE id = @id;
              ";

        var parameters = new Dictionary<string, object?>
        {
            { "qte", quantite },
            { "id", lotId }
        };

        if (nouveauCoutAchat.HasValue)
        {
            parameters.Add("cout", nouveauCoutAchat.Value.ToString(CultureInfo.InvariantCulture));
        }

        return _db.ExecuteNonQuery(sql, parameters, transaction) > 0;
    }

    public bool ChangerStatut(int lotId, StatutLot statut)
    {
        const string sql = "UPDATE lots_stocks SET statut = @statut WHERE id = @id;";
        var parameters = new Dictionary<string, object?>
        {
            { "statut", statut.ToString() },
            { "id", lotId }
        };

        return _db.ExecuteNonQuery(sql, parameters) > 0;
    }

    private static LotStock MapLotStock(SqliteDataReader reader)
    {
        decimal cout = 0m;
        if (!reader.IsDBNull(7))
        {
            var raw = reader.GetValue(7).ToString();
            decimal.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out cout);
        }

        return new LotStock
        {
            Id = reader.GetInt32(0),
            ProduitId = reader.GetInt32(1),
            FournisseurId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            NumeroLot = reader.GetString(3),
            DatePeremption = DateTime.TryParse(reader.GetString(4), out var dtP) ? dtP : DateTime.MinValue,
            QuantiteInitiale = reader.GetInt32(5),
            QuantiteActuelle = reader.GetInt32(6),
            CoutAchat = cout,
            DateReception = DateTime.TryParse(reader.GetString(8), out var dtR) ? dtR : DateTime.UtcNow,
            Statut = Enum.TryParse<StatutLot>(reader.GetString(9), out var st) ? st : StatutLot.Normal,
            DesignationProduit = reader.IsDBNull(10) ? null : reader.GetString(10),
            NomFournisseur = reader.IsDBNull(11) ? null : reader.GetString(11)
        };
    }
}
