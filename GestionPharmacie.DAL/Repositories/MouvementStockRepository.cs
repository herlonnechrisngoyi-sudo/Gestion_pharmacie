using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Repositories;

public interface IMouvementStockRepository
{
    List<MouvementStock> GetAll(int limit = 100);
    List<MouvementStock> GetByProduit(int produitId);
    int Add(MouvementStock mvt, SqliteTransaction? transaction = null);
}

public class MouvementStockRepository : IMouvementStockRepository
{
    private readonly DatabaseConnection _db;

    public MouvementStockRepository(DatabaseConnection? db = null)
    {
        _db = db ?? DatabaseConnection.Instance;
    }

    public List<MouvementStock> GetAll(int limit = 100)
    {
        var list = new List<MouvementStock>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT m.id, m.produit_id, m.lot_id, m.type_mouvement, m.quantite, m.date_mouvement, m.motif, m.utilisateur_id,
                   p.designation, l.numero_lot, u.nom
            FROM mouvements_stock m
            INNER JOIN produits p ON p.id = m.produit_id
            LEFT JOIN lots_stocks l ON l.id = m.lot_id
            LEFT JOIN utilisateurs u ON u.id = m.utilisateur_id
            ORDER BY m.date_mouvement DESC
            LIMIT @limit;
        ";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapMouvement(reader));
        }
        return list;
    }

    public List<MouvementStock> GetByProduit(int produitId)
    {
        var list = new List<MouvementStock>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT m.id, m.produit_id, m.lot_id, m.type_mouvement, m.quantite, m.date_mouvement, m.motif, m.utilisateur_id,
                   p.designation, l.numero_lot, u.nom
            FROM mouvements_stock m
            INNER JOIN produits p ON p.id = m.produit_id
            LEFT JOIN lots_stocks l ON l.id = m.lot_id
            LEFT JOIN utilisateurs u ON u.id = m.utilisateur_id
            WHERE m.produit_id = @produitId
            ORDER BY m.date_mouvement DESC;
        ";
        cmd.Parameters.AddWithValue("@produitId", produitId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapMouvement(reader));
        }
        return list;
    }

    public int Add(MouvementStock mvt, SqliteTransaction? transaction = null)
    {
        const string sql = @"
            INSERT INTO mouvements_stock (produit_id, lot_id, type_mouvement, quantite, date_mouvement, motif, utilisateur_id)
            VALUES (@prodId, @lotId, @typeMvt, @qte, @date, @motif, @userId);
            SELECT last_insert_rowid();
        ";

        var parameters = new Dictionary<string, object?>
        {
            { "prodId", mvt.ProduitId },
            { "lotId", mvt.LotId },
            { "typeMvt", mvt.TypeMouvement.ToString() },
            { "qte", mvt.Quantite },
            { "date", mvt.DateMouvement.ToString("yyyy-MM-dd HH:mm:ss") },
            { "motif", mvt.Motif },
            { "userId", mvt.UtilisateurId }
        };

        long id = Convert.ToInt64(_db.ExecuteScalar(sql, parameters, transaction) ?? 0);
        mvt.Id = (int)id;
        return mvt.Id;
    }

    private static MouvementStock MapMouvement(SqliteDataReader reader)
    {
        return new MouvementStock
        {
            Id = reader.GetInt32(0),
            ProduitId = reader.GetInt32(1),
            LotId = reader.IsDBNull(2) ? null : reader.GetInt32(2),
            TypeMouvement = Enum.TryParse<TypeMouvementStock>(reader.GetString(3), out var tm) ? tm : TypeMouvementStock.Entree,
            Quantite = reader.GetInt32(4),
            DateMouvement = DateTime.TryParse(reader.GetString(5), out var dt) ? dt : DateTime.UtcNow,
            Motif = reader.GetString(6),
            UtilisateurId = reader.IsDBNull(7) ? null : reader.GetInt32(7),
            DesignationProduit = reader.IsDBNull(8) ? null : reader.GetString(8),
            NumeroLot = reader.IsDBNull(9) ? null : reader.GetString(9),
            NomUtilisateur = reader.IsDBNull(10) ? null : reader.GetString(10)
        };
    }
}
