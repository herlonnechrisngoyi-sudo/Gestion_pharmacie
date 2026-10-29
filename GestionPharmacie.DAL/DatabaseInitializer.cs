using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Security;
using Microsoft.Data.Sqlite;

namespace GestionPharmacie.DAL;

/// <summary>
/// Initialisateur du schéma DDL et des données initiales DML de la base SQLite.
/// </summary>
public static class DatabaseInitializer
{
    public const string DefaultAdminUsername = "admin";
    public const string DefaultAdminPassword = "admin";
    public const string DefaultVendeurUsername = "vendeur";
    public const string DefaultVendeurPassword = "vendeur";

    /// <summary>
    /// Initialise la base de données : crée les tables, index et les comptes admin et vendeur par défaut si inexistants.
    /// </summary>
    public static void InitializeDatabase()
    {
        var db = DatabaseConnection.Instance;

        using var connection = db.CreateOpenConnection();
        using var transaction = connection.BeginTransaction();

        try
        {
            // 1. Création des tables DDL
            CreateTables(connection, transaction);

            // 2. Création des index DDL
            CreateIndexes(connection, transaction);

            // 3. Alimentation DML des données par défaut (Comptes Admin & Vendeur)
            SeedDefaultData(connection, transaction);

            transaction.Commit();
        }
        catch
        {
            transaction.Rollback();
            throw;
        }
    }

    private static void CreateTables(SqliteConnection connection, SqliteTransaction transaction)
    {
        const string sqlDdl = @"
            -- 1. Table Utilisateurs
            CREATE TABLE IF NOT EXISTS utilisateurs (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                nom                 TEXT NOT NULL,
                identifiant         TEXT NOT NULL UNIQUE COLLATE NOCASE,
                mot_de_passe_hash   TEXT NOT NULL,
                role                TEXT NOT NULL CHECK(role IN ('Admin', 'Vendeur')),
                actif               INTEGER NOT NULL DEFAULT 1 CHECK(actif IN (0, 1)),
                date_creation       TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- 2. Table Produits (prix_vente en NUMERIC/DECIMAL)
            CREATE TABLE IF NOT EXISTS produits (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                reference_unique    TEXT NOT NULL UNIQUE COLLATE NOCASE,
                designation         TEXT NOT NULL,
                categorie           TEXT NOT NULL,
                dosage              TEXT,
                forme               TEXT,
                unite_vente         TEXT NOT NULL,
                prix_vente          NUMERIC(18, 2) NOT NULL CHECK(prix_vente >= 0),
                seuil_stock_min     INTEGER NOT NULL DEFAULT 5 CHECK(seuil_stock_min >= 0),
                actif               INTEGER NOT NULL DEFAULT 1 CHECK(actif IN (0, 1)),
                date_creation       TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- 3. Table Fournisseurs
            CREATE TABLE IF NOT EXISTS fournisseurs (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                nom                 TEXT NOT NULL,
                telephone           TEXT,
                adresse             TEXT,
                email               TEXT,
                actif               INTEGER NOT NULL DEFAULT 1 CHECK(actif IN (0, 1)),
                date_creation       TEXT NOT NULL DEFAULT (datetime('now'))
            );

            -- 4. Table Lots_Stocks (cout_achat en NUMERIC/DECIMAL)
            CREATE TABLE IF NOT EXISTS lots_stocks (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                produit_id          INTEGER NOT NULL REFERENCES produits(id) ON DELETE RESTRICT,
                fournisseur_id      INTEGER REFERENCES fournisseurs(id) ON DELETE SET NULL,
                numero_lot          TEXT NOT NULL,
                date_peremption     TEXT NOT NULL,
                quantite_initiale   INTEGER NOT NULL CHECK(quantite_initiale >= 0),
                quantite_actuelle   INTEGER NOT NULL CHECK(quantite_actuelle >= 0),
                cout_achat          NUMERIC(18, 2) NOT NULL CHECK(cout_achat >= 0),
                date_reception      TEXT NOT NULL DEFAULT (datetime('now')),
                statut              TEXT NOT NULL DEFAULT 'Normal' CHECK(statut IN ('Normal', 'Bloque')),
                CONSTRAINT uq_produit_lot UNIQUE(produit_id, numero_lot)
            );

            -- 5. Table Ventes (montants en NUMERIC/DECIMAL)
            CREATE TABLE IF NOT EXISTS ventes (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                numero_facture      TEXT NOT NULL UNIQUE,
                date_vente          TEXT NOT NULL DEFAULT (datetime('now')),
                utilisateur_id      INTEGER NOT NULL REFERENCES utilisateurs(id) ON DELETE RESTRICT,
                montant_total       NUMERIC(18, 2) NOT NULL CHECK(montant_total >= 0),
                montant_paye        NUMERIC(18, 2) NOT NULL CHECK(montant_paye >= 0),
                rendu_monnaie       NUMERIC(18, 2) NOT NULL DEFAULT 0.00 CHECK(rendu_monnaie >= 0),
                statut              TEXT NOT NULL DEFAULT 'Validee' CHECK(statut IN ('Validee', 'Annulee'))
            );

            -- 6. Table Lignes_Ventes (prix et sous-total en NUMERIC/DECIMAL)
            CREATE TABLE IF NOT EXISTS lignes_ventes (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                vente_id            INTEGER NOT NULL REFERENCES ventes(id) ON DELETE CASCADE,
                lot_id              INTEGER NOT NULL REFERENCES lots_stocks(id) ON DELETE RESTRICT,
                quantite            INTEGER NOT NULL CHECK(quantite > 0),
                prix_unitaire       NUMERIC(18, 2) NOT NULL CHECK(prix_unitaire >= 0),
                sous_total          NUMERIC(18, 2) NOT NULL CHECK(sous_total >= 0)
            );

            -- 7. Table Paiements (montant en NUMERIC/DECIMAL)
            CREATE TABLE IF NOT EXISTS paiements (
                id                      INTEGER PRIMARY KEY AUTOINCREMENT,
                vente_id                INTEGER NOT NULL REFERENCES ventes(id) ON DELETE CASCADE,
                date_paiement           TEXT NOT NULL DEFAULT (datetime('now')),
                montant                 NUMERIC(18, 2) NOT NULL CHECK(montant > 0),
                mode_paiement           TEXT NOT NULL CHECK(mode_paiement IN ('Especes', 'CarteBancaire', 'Virement', 'Cheque', 'MobileMoney')),
                reference_transaction   TEXT
            );

            -- 8. Table Mouvements_Stock
            CREATE TABLE IF NOT EXISTS mouvements_stock (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                produit_id          INTEGER NOT NULL REFERENCES produits(id) ON DELETE RESTRICT,
                lot_id              INTEGER REFERENCES lots_stocks(id) ON DELETE SET NULL,
                type_mouvement      TEXT NOT NULL CHECK(type_mouvement IN ('Entree', 'Sortie_Vente', 'Sortie_Perime', 'Ajustement', 'Retour')),
                quantite            INTEGER NOT NULL,
                date_mouvement      TEXT NOT NULL DEFAULT (datetime('now')),
                motif               TEXT NOT NULL,
                utilisateur_id      INTEGER REFERENCES utilisateurs(id) ON DELETE SET NULL
            );

            -- 9. Table Journal_Audit
            CREATE TABLE IF NOT EXISTS journal_audit (
                id                  INTEGER PRIMARY KEY AUTOINCREMENT,
                utilisateur_id      INTEGER REFERENCES utilisateurs(id) ON DELETE SET NULL,
                action              TEXT NOT NULL,
                table_concernee     TEXT NOT NULL,
                entite_id           INTEGER,
                details             TEXT NOT NULL,
                date_action         TEXT NOT NULL DEFAULT (datetime('now'))
            );
        ";

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sqlDdl;
        cmd.ExecuteNonQuery();
    }

    private static void CreateIndexes(SqliteConnection connection, SqliteTransaction transaction)
    {
        const string sqlIndexes = @"
            CREATE INDEX IF NOT EXISTS idx_utilisateurs_identifiant ON utilisateurs(identifiant);
            CREATE INDEX IF NOT EXISTS idx_produits_ref ON produits(reference_unique);
            CREATE INDEX IF NOT EXISTS idx_produits_designation ON produits(designation);
            CREATE INDEX IF NOT EXISTS idx_produits_categorie ON produits(categorie);
            CREATE INDEX IF NOT EXISTS idx_lots_produit ON lots_stocks(produit_id);
            CREATE INDEX IF NOT EXISTS idx_lots_peremption ON lots_stocks(date_peremption);
            CREATE INDEX IF NOT EXISTS idx_lots_statut ON lots_stocks(statut);
            CREATE INDEX IF NOT EXISTS idx_ventes_date ON ventes(date_vente);
            CREATE INDEX IF NOT EXISTS idx_ventes_facture ON ventes(numero_facture);
            CREATE INDEX IF NOT EXISTS idx_ventes_utilisateur ON ventes(utilisateur_id);
            CREATE INDEX IF NOT EXISTS idx_lignes_vente ON lignes_ventes(vente_id);
            CREATE INDEX IF NOT EXISTS idx_lignes_lot ON lignes_ventes(lot_id);
            CREATE INDEX IF NOT EXISTS idx_paiements_vente ON paiements(vente_id);
            CREATE INDEX IF NOT EXISTS idx_mouvements_produit ON mouvements_stock(produit_id);
            CREATE INDEX IF NOT EXISTS idx_mouvements_date ON mouvements_stock(date_mouvement);
            CREATE INDEX IF NOT EXISTS idx_audit_date ON journal_audit(date_action);
        ";

        using var cmd = connection.CreateCommand();
        cmd.Transaction = transaction;
        cmd.CommandText = sqlIndexes;
        cmd.ExecuteNonQuery();
    }

    private static void SeedDefaultData(SqliteConnection connection, SqliteTransaction transaction)
    {
        // 1. Compte Administrateur par défaut
        using var checkAdminCmd = connection.CreateCommand();
        checkAdminCmd.Transaction = transaction;
        checkAdminCmd.CommandText = "SELECT COUNT(*) FROM utilisateurs WHERE identifiant = @login;";
        checkAdminCmd.Parameters.AddWithValue("@login", DefaultAdminUsername);
        long countAdmin = Convert.ToInt64(checkAdminCmd.ExecuteScalar() ?? 0);

        if (countAdmin == 0)
        {
            string hashAdmin = PasswordHasher.HashPassword(DefaultAdminPassword);

            using var insertAdminCmd = connection.CreateCommand();
            insertAdminCmd.Transaction = transaction;
            insertAdminCmd.CommandText = @"
                INSERT INTO utilisateurs (nom, identifiant, mot_de_passe_hash, role, actif, date_creation)
                VALUES (@nom, @login, @hash, 'Admin', 1, datetime('now'));
                SELECT last_insert_rowid();
            ";
            insertAdminCmd.Parameters.AddWithValue("@nom", "Administrateur Principal");
            insertAdminCmd.Parameters.AddWithValue("@login", DefaultAdminUsername);
            insertAdminCmd.Parameters.AddWithValue("@hash", hashAdmin);
            long adminId = Convert.ToInt64(insertAdminCmd.ExecuteScalar() ?? 1);

            using var auditAdminCmd = connection.CreateCommand();
            auditAdminCmd.Transaction = transaction;
            auditAdminCmd.CommandText = @"
                INSERT INTO journal_audit (utilisateur_id, action, table_concernee, entite_id, details, date_action)
                VALUES (@userId, 'INITIALISATION', 'utilisateurs', @entiteId, @details, datetime('now'));
            ";
            auditAdminCmd.Parameters.AddWithValue("@userId", adminId);
            auditAdminCmd.Parameters.AddWithValue("@entiteId", adminId);
            auditAdminCmd.Parameters.AddWithValue("@details", "Création automatique du compte administrateur par défaut.");
            auditAdminCmd.ExecuteNonQuery();
        }

        // 2. Compte Vendeur par défaut (pour test immédiat de la séparation des rôles)
        using var checkVendeurCmd = connection.CreateCommand();
        checkVendeurCmd.Transaction = transaction;
        checkVendeurCmd.CommandText = "SELECT COUNT(*) FROM utilisateurs WHERE identifiant = @login;";
        checkVendeurCmd.Parameters.AddWithValue("@login", DefaultVendeurUsername);
        long countVendeur = Convert.ToInt64(checkVendeurCmd.ExecuteScalar() ?? 0);

        if (countVendeur == 0)
        {
            string hashVendeur = PasswordHasher.HashPassword(DefaultVendeurPassword);

            using var insertVendeurCmd = connection.CreateCommand();
            insertVendeurCmd.Transaction = transaction;
            insertVendeurCmd.CommandText = @"
                INSERT INTO utilisateurs (nom, identifiant, mot_de_passe_hash, role, actif, date_creation)
                VALUES (@nom, @login, @hash, 'Vendeur', 1, datetime('now'));
                SELECT last_insert_rowid();
            ";
            insertVendeurCmd.Parameters.AddWithValue("@nom", "Vendeur Comptoir");
            insertVendeurCmd.Parameters.AddWithValue("@login", DefaultVendeurUsername);
            insertVendeurCmd.Parameters.AddWithValue("@hash", hashVendeur);
            long vendeurId = Convert.ToInt64(insertVendeurCmd.ExecuteScalar() ?? 2);

            using var auditVendeurCmd = connection.CreateCommand();
            auditVendeurCmd.Transaction = transaction;
            auditVendeurCmd.CommandText = @"
                INSERT INTO journal_audit (utilisateur_id, action, table_concernee, entite_id, details, date_action)
                VALUES (@userId, 'INITIALISATION', 'utilisateurs', @entiteId, @details, datetime('now'));
            ";
            auditVendeurCmd.Parameters.AddWithValue("@userId", vendeurId);
            auditVendeurCmd.Parameters.AddWithValue("@entiteId", vendeurId);
            auditVendeurCmd.Parameters.AddWithValue("@details", "Création automatique du compte vendeur par défaut pour validation des accès restreints.");
            auditVendeurCmd.ExecuteNonQuery();
        }
    }
}
