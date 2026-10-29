using System.Globalization;
using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL.Repositories;

public interface IVenteRepository
{
    Vente? GetById(int id);
    Vente? GetByNumeroFacture(string numeroFacture);
    List<Vente> GetVentesRecentes(int limit = 50);
    List<Vente> GetVentesParPeriode(DateTime debut, DateTime fin);
    decimal GetChiffreAffairesJour(DateTime jour);
    int EnregistrerVenteComplete(Vente vente, List<LigneVente> lignes, List<Paiement> paiements, List<MouvementStock> mouvements);
    bool AnnulerVente(int venteId, int utilisateurId, string motif);
}

public class VenteRepository : IVenteRepository
{
    private readonly DatabaseConnection _db;

    public VenteRepository(DatabaseConnection? db = null)
    {
        _db = db ?? DatabaseConnection.Instance;
    }

    public Vente? GetById(int id)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT v.id, v.numero_facture, v.date_vente, v.utilisateur_id, 
                   v.montant_total, v.montant_paye, v.rendu_monnaie, v.statut,
                   u.nom AS nom_vendeur
            FROM ventes v
            INNER JOIN utilisateurs u ON u.id = v.utilisateur_id
            WHERE v.id = @id;
        ";
        cmd.Parameters.AddWithValue("@id", id);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var vente = MapVente(reader);
        reader.Close();

        // Charger lignes
        vente.Lignes = GetLignesByVenteId(conn, vente.Id);
        vente.Paiements = GetPaiementsByVenteId(conn, vente.Id);

        return vente;
    }

    public Vente? GetByNumeroFacture(string numeroFacture)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT v.id, v.numero_facture, v.date_vente, v.utilisateur_id, 
                   v.montant_total, v.montant_paye, v.rendu_monnaie, v.statut,
                   u.nom AS nom_vendeur
            FROM ventes v
            INNER JOIN utilisateurs u ON u.id = v.utilisateur_id
            WHERE v.numero_facture = @numFacture;
        ";
        cmd.Parameters.AddWithValue("@numFacture", numeroFacture);

        using var reader = cmd.ExecuteReader();
        if (!reader.Read()) return null;

        var vente = MapVente(reader);
        reader.Close();

        vente.Lignes = GetLignesByVenteId(conn, vente.Id);
        vente.Paiements = GetPaiementsByVenteId(conn, vente.Id);

        return vente;
    }

    public List<Vente> GetVentesRecentes(int limit = 50)
    {
        var list = new List<Vente>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT v.id, v.numero_facture, v.date_vente, v.utilisateur_id, 
                   v.montant_total, v.montant_paye, v.rendu_monnaie, v.statut,
                   u.nom AS nom_vendeur
            FROM ventes v
            INNER JOIN utilisateurs u ON u.id = v.utilisateur_id
            ORDER BY v.date_vente DESC
            LIMIT @limit;
        ";
        cmd.Parameters.AddWithValue("@limit", limit);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapVente(reader));
        }
        return list;
    }

    public List<Vente> GetVentesParPeriode(DateTime debut, DateTime fin)
    {
        var list = new List<Vente>();
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT v.id, v.numero_facture, v.date_vente, v.utilisateur_id, 
                   v.montant_total, v.montant_paye, v.rendu_monnaie, v.statut,
                   u.nom AS nom_vendeur
            FROM ventes v
            INNER JOIN utilisateurs u ON u.id = v.utilisateur_id
            WHERE date(v.date_vente) BETWEEN date(@debut) AND date(@fin)
            ORDER BY v.date_vente DESC;
        ";
        cmd.Parameters.AddWithValue("@debut", debut.ToString("yyyy-MM-dd"));
        cmd.Parameters.AddWithValue("@fin", fin.ToString("yyyy-MM-dd"));

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            list.Add(MapVente(reader));
        }
        return list;
    }

    public decimal GetChiffreAffairesJour(DateTime jour)
    {
        using var conn = _db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = @"
            SELECT COALESCE(SUM(montant_total), 0)
            FROM ventes
            WHERE date(date_vente) = date(@jour) AND statut = 'Validee';
        ";
        cmd.Parameters.AddWithValue("@jour", jour.ToString("yyyy-MM-dd"));

        var result = cmd.ExecuteScalar();
        if (result != null && decimal.TryParse(result.ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var ca))
        {
            return ca;
        }
        return 0m;
    }

    public int EnregistrerVenteComplete(Vente vente, List<LigneVente> lignes, List<Paiement> paiements, List<MouvementStock> mouvements)
    {
        return _db.ExecuteInTransaction((conn, trans) =>
        {
            // 1. Insertion de la vente
            using var cmdVente = conn.CreateCommand();
            cmdVente.Transaction = trans;
            cmdVente.CommandText = @"
                INSERT INTO ventes (numero_facture, date_vente, utilisateur_id, montant_total, montant_paye, rendu_monnaie, statut)
                VALUES (@num, @date, @userId, @total, @paye, @rendu, @statut);
                SELECT last_insert_rowid();
            ";
            cmdVente.Parameters.AddWithValue("@num", vente.NumeroFacture);
            cmdVente.Parameters.AddWithValue("@date", vente.DateVente.ToString("yyyy-MM-dd HH:mm:ss"));
            cmdVente.Parameters.AddWithValue("@userId", vente.UtilisateurId);
            cmdVente.Parameters.AddWithValue("@total", vente.MontantTotal.ToString(CultureInfo.InvariantCulture));
            cmdVente.Parameters.AddWithValue("@paye", vente.MontantPaye.ToString(CultureInfo.InvariantCulture));
            cmdVente.Parameters.AddWithValue("@rendu", vente.RenduMonnaie.ToString(CultureInfo.InvariantCulture));
            cmdVente.Parameters.AddWithValue("@statut", vente.Statut.ToString());

            int venteId = Convert.ToInt32(cmdVente.ExecuteScalar());
            vente.Id = venteId;

            // 2. Insertion des lignes de vente et décrémentation des stocks
            foreach (var ligne in lignes)
            {
                using var cmdLigne = conn.CreateCommand();
                cmdLigne.Transaction = trans;
                cmdLigne.CommandText = @"
                    INSERT INTO lignes_ventes (vente_id, lot_id, quantite, prix_unitaire, sous_total)
                    VALUES (@venteId, @lotId, @qte, @pu, @sousTotal);
                ";
                cmdLigne.Parameters.AddWithValue("@venteId", venteId);
                cmdLigne.Parameters.AddWithValue("@lotId", ligne.LotId);
                cmdLigne.Parameters.AddWithValue("@qte", ligne.Quantite);
                cmdLigne.Parameters.AddWithValue("@pu", ligne.PrixUnitaire.ToString(CultureInfo.InvariantCulture));
                cmdLigne.Parameters.AddWithValue("@sousTotal", ligne.SousTotal.ToString(CultureInfo.InvariantCulture));
                cmdLigne.ExecuteNonQuery();

                // Décrémentation du stock lot
                using var cmdDimStock = conn.CreateCommand();
                cmdDimStock.Transaction = trans;
                cmdDimStock.CommandText = @"
                    UPDATE lots_stocks 
                    SET quantite_actuelle = quantite_actuelle - @qte 
                    WHERE id = @lotId AND quantite_actuelle >= @qte;
                ";
                cmdDimStock.Parameters.AddWithValue("@qte", ligne.Quantite);
                cmdDimStock.Parameters.AddWithValue("@lotId", ligne.LotId);
                int rowsUpdated = cmdDimStock.ExecuteNonQuery();
                if (rowsUpdated == 0)
                {
                    throw new InvalidOperationException($"Stock insuffisant pour le lot #{ligne.LotId} lors de la finalisation de la vente.");
                }
            }

            // 3. Insertion des paiements
            foreach (var p in paiements)
            {
                using var cmdPmt = conn.CreateCommand();
                cmdPmt.Transaction = trans;
                cmdPmt.CommandText = @"
                    INSERT INTO paiements (vente_id, date_paiement, montant, mode_paiement, reference_transaction)
                    VALUES (@venteId, @date, @montant, @mode, @ref);
                ";
                cmdPmt.Parameters.AddWithValue("@venteId", venteId);
                cmdPmt.Parameters.AddWithValue("@date", p.DatePaiement.ToString("yyyy-MM-dd HH:mm:ss"));
                cmdPmt.Parameters.AddWithValue("@montant", p.Montant.ToString(CultureInfo.InvariantCulture));
                cmdPmt.Parameters.AddWithValue("@mode", p.Mode.ToString());
                cmdPmt.Parameters.AddWithValue("@ref", (object?)p.ReferenceTransaction ?? DBNull.Value);
                cmdPmt.ExecuteNonQuery();
            }

            // 4. Insertion des mouvements de stock
            foreach (var m in mouvements)
            {
                using var cmdMvt = conn.CreateCommand();
                cmdMvt.Transaction = trans;
                cmdMvt.CommandText = @"
                    INSERT INTO mouvements_stock (produit_id, lot_id, type_mouvement, quantite, date_mouvement, motif, utilisateur_id)
                    VALUES (@prodId, @lotId, @typeMvt, @qte, @date, @motif, @userId);
                ";
                cmdMvt.Parameters.AddWithValue("@prodId", m.ProduitId);
                cmdMvt.Parameters.AddWithValue("@lotId", (object?)m.LotId ?? DBNull.Value);
                cmdMvt.Parameters.AddWithValue("@typeMvt", m.TypeMouvement.ToString());
                cmdMvt.Parameters.AddWithValue("@qte", m.Quantite);
                cmdMvt.Parameters.AddWithValue("@date", m.DateMouvement.ToString("yyyy-MM-dd HH:mm:ss"));
                cmdMvt.Parameters.AddWithValue("@motif", m.Motif);
                cmdMvt.Parameters.AddWithValue("@userId", (object?)m.UtilisateurId ?? DBNull.Value);
                cmdMvt.ExecuteNonQuery();
            }

            // 5. Journal d'audit
            using var cmdAudit = conn.CreateCommand();
            cmdAudit.Transaction = trans;
            cmdAudit.CommandText = @"
                INSERT INTO journal_audit (utilisateur_id, action, table_concernee, entite_id, details, date_action)
                VALUES (@userId, 'VENTE', 'ventes', @venteId, @details, datetime('now'));
            ";
            cmdAudit.Parameters.AddWithValue("@userId", vente.UtilisateurId);
            cmdAudit.Parameters.AddWithValue("@venteId", venteId);
            cmdAudit.Parameters.AddWithValue("@details", $"Vente validée #{vente.NumeroFacture} - Total: {vente.MontantTotal:C} - {lignes.Count} article(s)");
            cmdAudit.ExecuteNonQuery();

            return venteId;
        });
    }

    public bool AnnulerVente(int venteId, int utilisateurId, string motif)
    {
        return _db.ExecuteInTransaction((conn, trans) =>
        {
            // Récupérer les lignes pour restituer le stock
            var lignes = GetLignesByVenteId(conn, venteId, trans);
            foreach (var ligne in lignes)
            {
                using var cmdRestit = conn.CreateCommand();
                cmdRestit.Transaction = trans;
                cmdRestit.CommandText = "UPDATE lots_stocks SET quantite_actuelle = quantite_actuelle + @qte WHERE id = @lotId;";
                cmdRestit.Parameters.AddWithValue("@qte", ligne.Quantite);
                cmdRestit.Parameters.AddWithValue("@lotId", ligne.LotId);
                cmdRestit.ExecuteNonQuery();
            }

            // Mettre la vente à Annulée
            using var cmdAnnul = conn.CreateCommand();
            cmdAnnul.Transaction = trans;
            cmdAnnul.CommandText = "UPDATE ventes SET statut = 'Annulee' WHERE id = @id;";
            cmdAnnul.Parameters.AddWithValue("@id", venteId);
            int affected = cmdAnnul.ExecuteNonQuery();

            // Audit
            using var cmdAudit = conn.CreateCommand();
            cmdAudit.Transaction = trans;
            cmdAudit.CommandText = @"
                INSERT INTO journal_audit (utilisateur_id, action, table_concernee, entite_id, details, date_action)
                VALUES (@userId, 'ANNULATION_VENTE', 'ventes', @venteId, @details, datetime('now'));
            ";
            cmdAudit.Parameters.AddWithValue("@userId", utilisateurId);
            cmdAudit.Parameters.AddWithValue("@venteId", venteId);
            cmdAudit.Parameters.AddWithValue("@details", $"Annulation de la vente #{venteId}. Motif : {motif}");
            cmdAudit.ExecuteNonQuery();

            return affected > 0;
        });
    }

    private static List<LigneVente> GetLignesByVenteId(SqliteConnection conn, int venteId, SqliteTransaction? trans = null)
    {
        var list = new List<LigneVente>();
        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = @"
            SELECT lv.id, lv.vente_id, lv.lot_id, lv.quantite, lv.prix_unitaire, lv.sous_total,
                   p.designation, l.numero_lot
            FROM lignes_ventes lv
            INNER JOIN lots_stocks l ON l.id = lv.lot_id
            INNER JOIN produits p ON p.id = l.produit_id
            WHERE lv.vente_id = @venteId;
        ";
        cmd.Parameters.AddWithValue("@venteId", venteId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            decimal pu = decimal.TryParse(reader.GetValue(4).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v1) ? v1 : 0m;
            decimal st = decimal.TryParse(reader.GetValue(5).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2) ? v2 : 0m;

            list.Add(new LigneVente
            {
                Id = reader.GetInt32(0),
                VenteId = reader.GetInt32(1),
                LotId = reader.GetInt32(2),
                Quantite = reader.GetInt32(3),
                PrixUnitaire = pu,
                SousTotal = st,
                DesignationProduit = reader.GetString(6),
                NumeroLot = reader.GetString(7)
            });
        }
        return list;
    }

    private static List<Paiement> GetPaiementsByVenteId(SqliteConnection conn, int venteId, SqliteTransaction? trans = null)
    {
        var list = new List<Paiement>();
        using var cmd = conn.CreateCommand();
        if (trans != null) cmd.Transaction = trans;
        cmd.CommandText = @"
            SELECT id, vente_id, date_paiement, montant, mode_paiement, reference_transaction
            FROM paiements
            WHERE vente_id = @venteId;
        ";
        cmd.Parameters.AddWithValue("@venteId", venteId);

        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            decimal montant = decimal.TryParse(reader.GetValue(3).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v) ? v : 0m;

            list.Add(new Paiement
            {
                Id = reader.GetInt32(0),
                VenteId = reader.GetInt32(1),
                DatePaiement = DateTime.TryParse(reader.GetString(2), out var dt) ? dt : DateTime.UtcNow,
                Montant = montant,
                Mode = Enum.TryParse<ModePaiement>(reader.GetString(4), out var mp) ? mp : ModePaiement.Especes,
                ReferenceTransaction = reader.IsDBNull(5) ? null : reader.GetString(5)
            });
        }
        return list;
    }

    private static Vente MapVente(SqliteDataReader reader)
    {
        decimal total = decimal.TryParse(reader.GetValue(4).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v1) ? v1 : 0m;
        decimal paye = decimal.TryParse(reader.GetValue(5).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v2) ? v2 : 0m;
        decimal rendu = decimal.TryParse(reader.GetValue(6).ToString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var v3) ? v3 : 0m;

        return new Vente
        {
            Id = reader.GetInt32(0),
            NumeroFacture = reader.GetString(1),
            DateVente = DateTime.TryParse(reader.GetString(2), out var dt) ? dt : DateTime.UtcNow,
            UtilisateurId = reader.GetInt32(3),
            MontantTotal = total,
            MontantPaye = paye,
            RenduMonnaie = rendu,
            Statut = Enum.TryParse<StatutVente>(reader.GetString(7), out var st) ? st : StatutVente.Validee,
            NomVendeur = reader.IsDBNull(8) ? null : reader.GetString(8)
        };
    }
}
