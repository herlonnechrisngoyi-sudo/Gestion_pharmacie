-- ============================================================================
-- BASE DE DONNÉES PHARMACEUTIQUE - SCRIPT DDL & DML (SQLite)
-- Architecture 3-Tiers - Système de Gestion d'Officine
-- Monnaie : Haute précision (DECIMAL / NUMERIC 18,2)
-- Sécurité : Intégrité référentielle, contraintes de validation, traçabilité
-- ============================================================================

PRAGMA foreign_keys = ON;
PRAGMA encoding = "UTF-8";

-- ----------------------------------------------------------------------------
-- 1. TABLE : utilisateurs
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS utilisateurs (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    nom                 TEXT NOT NULL,
    identifiant         TEXT NOT NULL UNIQUE COLLATE NOCASE,
    mot_de_passe_hash   TEXT NOT NULL,
    role                TEXT NOT NULL CHECK(role IN ('Admin', 'Vendeur')),
    actif               INTEGER NOT NULL DEFAULT 1 CHECK(actif IN (0, 1)),
    date_creation       TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_utilisateurs_identifiant ON utilisateurs(identifiant);

-- ----------------------------------------------------------------------------
-- 2. TABLE : produits
-- ----------------------------------------------------------------------------
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

CREATE INDEX IF NOT EXISTS idx_produits_ref ON produits(reference_unique);
CREATE INDEX IF NOT EXISTS idx_produits_designation ON produits(designation);
CREATE INDEX IF NOT EXISTS idx_produits_categorie ON produits(categorie);

-- ----------------------------------------------------------------------------
-- 3. TABLE : fournisseurs
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS fournisseurs (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    nom                 TEXT NOT NULL,
    telephone           TEXT,
    adresse             TEXT,
    email               TEXT,
    actif               INTEGER NOT NULL DEFAULT 1 CHECK(actif IN (0, 1)),
    date_creation       TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_fournisseurs_nom ON fournisseurs(nom);

-- ----------------------------------------------------------------------------
-- 4. TABLE : lots_stocks
-- ----------------------------------------------------------------------------
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

CREATE INDEX IF NOT EXISTS idx_lots_produit ON lots_stocks(produit_id);
CREATE INDEX IF NOT EXISTS idx_lots_peremption ON lots_stocks(date_peremption);
CREATE INDEX IF NOT EXISTS idx_lots_statut ON lots_stocks(statut);

-- ----------------------------------------------------------------------------
-- 5. TABLE : ventes
-- ----------------------------------------------------------------------------
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

CREATE INDEX IF NOT EXISTS idx_ventes_date ON ventes(date_vente);
CREATE INDEX IF NOT EXISTS idx_ventes_facture ON ventes(numero_facture);
CREATE INDEX IF NOT EXISTS idx_ventes_utilisateur ON ventes(utilisateur_id);

-- ----------------------------------------------------------------------------
-- 6. TABLE : lignes_ventes
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS lignes_ventes (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    vente_id            INTEGER NOT NULL REFERENCES ventes(id) ON DELETE CASCADE,
    lot_id              INTEGER NOT NULL REFERENCES lots_stocks(id) ON DELETE RESTRICT,
    quantite            INTEGER NOT NULL CHECK(quantite > 0),
    prix_unitaire       NUMERIC(18, 2) NOT NULL CHECK(prix_unitaire >= 0),
    sous_total          NUMERIC(18, 2) NOT NULL CHECK(sous_total >= 0)
);

CREATE INDEX IF NOT EXISTS idx_lignes_vente ON lignes_ventes(vente_id);
CREATE INDEX IF NOT EXISTS idx_lignes_lot ON lignes_ventes(lot_id);

-- ----------------------------------------------------------------------------
-- 7. TABLE : paiements
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS paiements (
    id                      INTEGER PRIMARY KEY AUTOINCREMENT,
    vente_id                INTEGER NOT NULL REFERENCES ventes(id) ON DELETE CASCADE,
    date_paiement           TEXT NOT NULL DEFAULT (datetime('now')),
    montant                 NUMERIC(18, 2) NOT NULL CHECK(montant > 0),
    mode_paiement           TEXT NOT NULL CHECK(mode_paiement IN ('Especes', 'CarteBancaire', 'Virement', 'Cheque', 'MobileMoney')),
    reference_transaction   TEXT
);

CREATE INDEX IF NOT EXISTS idx_paiements_vente ON paiements(vente_id);

-- ----------------------------------------------------------------------------
-- 8. TABLE : mouvements_stock
-- ----------------------------------------------------------------------------
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

CREATE INDEX IF NOT EXISTS idx_mouvements_produit ON mouvements_stock(produit_id);
CREATE INDEX IF NOT EXISTS idx_mouvements_date ON mouvements_stock(date_mouvement);
CREATE INDEX IF NOT EXISTS idx_mouvements_type ON mouvements_stock(type_mouvement);

-- ----------------------------------------------------------------------------
-- 9. TABLE : journal_audit
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS journal_audit (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    utilisateur_id      INTEGER REFERENCES utilisateurs(id) ON DELETE SET NULL,
    action              TEXT NOT NULL,
    table_concernee     TEXT NOT NULL,
    entite_id           INTEGER,
    details             TEXT NOT NULL,
    date_action         TEXT NOT NULL DEFAULT (datetime('now'))
);

CREATE INDEX IF NOT EXISTS idx_audit_date ON journal_audit(date_action);
CREATE INDEX IF NOT EXISTS idx_audit_utilisateur ON journal_audit(utilisateur_id);

-- ============================================================================
-- SCRIPT DML : INITIALISATION DES DONNÉES PAR DÉFAUT
-- Compte Administrateur initial (identifiant: 'admin', mot de passe: 'Admin@1234')
-- Hash PBKDF2-SHA256 sécurisé avec sel inclus
-- ============================================================================

INSERT OR IGNORE INTO utilisateurs (id, nom, identifiant, mot_de_passe_hash, role, actif, date_creation)
VALUES (
    1,
    'Administrateur Système',
    'admin',
    '100000.vA2iV90mN+G4B8yq7b8eWg==.YgLzV9zY4aJ8qLqLp2sB7d0L+7L5bX4qQ9o1eZ3mK8s=',
    'Admin',
    1,
    datetime('now')
);

INSERT OR IGNORE INTO journal_audit (utilisateur_id, action, table_concernee, entite_id, details, date_action)
VALUES (
    1,
    'INITIALISATION',
    'SYSTEME',
    1,
    'Création initiale de la structure de base de données et du compte administrateur par défaut.',
    datetime('now')
);
