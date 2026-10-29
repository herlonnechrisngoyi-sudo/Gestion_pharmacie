using Microsoft.VisualStudio.TestTools.UnitTesting;
using GestionPharmacie.DAL;
using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using GestionPharmacie.DAL.Repositories;
using GestionPharmacie.BLL.Services;
using GestionPharmacie.BLL.DTOs;

namespace GestionPharmacie.Tests;

[TestClass]
[DoNotParallelize]
public class DatabaseAndBusinessTests
{
    private string _testDbPath = string.Empty;

    [TestInitialize]
    public void Setup()
    {
        _testDbPath = Path.Combine(Path.GetTempPath(), $"pharmacie_test_{Guid.NewGuid():N}.db");
        DatabaseConnection.Initialize(_testDbPath);
        DatabaseInitializer.InitializeDatabase();
    }

    [TestCleanup]
    public void Cleanup()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (File.Exists(_testDbPath))
        {
            try { File.Delete(_testDbPath); } catch { }
        }
    }

    [TestMethod]
    public void Test_DatabaseInitialization_CreatesAllTablesAndAdminAndVendeur()
    {
        var db = DatabaseConnection.Instance;
        using var conn = db.CreateOpenConnection();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' ORDER BY name;";

        var tables = new List<string>();
        using var reader = cmd.ExecuteReader();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        // Vérification des 9 tables requises
        CollectionAssert.Contains(tables, "utilisateurs", "Table utilisateurs manquante");
        CollectionAssert.Contains(tables, "produits", "Table produits manquante");
        CollectionAssert.Contains(tables, "fournisseurs", "Table fournisseurs manquante");
        CollectionAssert.Contains(tables, "lots_stocks", "Table lots_stocks manquante");
        CollectionAssert.Contains(tables, "ventes", "Table ventes manquante");
        CollectionAssert.Contains(tables, "lignes_ventes", "Table lignes_ventes manquante");
        CollectionAssert.Contains(tables, "paiements", "Table paiements manquante");
        CollectionAssert.Contains(tables, "mouvements_stock", "Table mouvements_stock manquante");
        CollectionAssert.Contains(tables, "journal_audit", "Table journal_audit manquante");

        // Vérification des comptes par défaut (Admin & Vendeur)
        var userRepo = new UtilisateurRepository();
        var admin = userRepo.GetByIdentifiant("admin");
        Assert.IsNotNull(admin, "Le compte admin par défaut doit exister");
        Assert.AreEqual(RoleUtilisateur.Admin, admin.Role);
        Assert.IsTrue(admin.Actif);

        var vendeur = userRepo.GetByIdentifiant("vendeur");
        Assert.IsNotNull(vendeur, "Le compte vendeur par défaut doit exister");
        Assert.AreEqual(RoleUtilisateur.Vendeur, vendeur.Role);
        Assert.IsTrue(vendeur.Actif);
    }

    [TestMethod]
    public void Test_Authentication_SucceedsForBothRoles()
    {
        var authService = new AuthentificationService();

        // Test connexion Administrateur
        var admin = authService.Connecter("admin", "admin");
        Assert.IsNotNull(admin);
        Assert.AreEqual("admin", admin.Identifiant);
        Assert.AreEqual(RoleUtilisateur.Admin, admin.Role);

        // Test connexion Vendeur
        var vendeur = authService.Connecter("vendeur", "vendeur");
        Assert.IsNotNull(vendeur);
        Assert.AreEqual("vendeur", vendeur.Identifiant);
        Assert.AreEqual(RoleUtilisateur.Vendeur, vendeur.Role);
    }

    [TestMethod]
    public void Test_Alertes_Rupture_StockFaible_Et_Peremption90Jours()
    {
        var produitService = new ProduitService();
        var stockService = new StockService();

        // 1. Produit en rupture (stock = 0)
        var pRupture = produitService.EnregistrerProduit(new Produit
        {
            ReferenceUnique = "MED-RUPT-01",
            Designation = "Produit En Rupture",
            Categorie = "Générique",
            UniteVente = "Boîte",
            PrixVente = 5.00m,
            SeuilStockMin = 10
        });

        // 2. Produit avec stock faible et lot proche de péremption (< 90 jours)
        var pFaible = produitService.EnregistrerProduit(new Produit
        {
            ReferenceUnique = "MED-FAIBLE-01",
            Designation = "Produit Stock Faible",
            Categorie = "Générique",
            UniteVente = "Boîte",
            PrixVente = 8.50m,
            SeuilStockMin = 10
        });

        // Lot expirant dans 45 jours (doit apparaître dans l'alerte péremption 90 jours)
        stockService.ReceptionnerLot(pFaible.Id, null, "LOT-45J", DateTime.Today.AddDays(45), 4, 3.20m);

        // Vérification des indicateurs d'alertes
        var synthese = produitService.ObtenirSyntheseStocks();
        var ruptures = synthese.Where(p => p.EstEnRupture).ToList();
        var faibles = synthese.Where(p => p.EstStockFaible).ToList();
        var lotsProches = stockService.ObtenirLotsProchesPeremption(90);

        Assert.IsTrue(ruptures.Any(p => p.ProduitId == pRupture.Id), "Le produit en rupture doit être détecté");
        Assert.IsTrue(faibles.Any(p => p.ProduitId == pFaible.Id), "Le produit en stock faible doit être détecté");
        Assert.IsTrue(lotsProches.Any(l => l.NumeroLot == "LOT-45J"), "Le lot à 45 jours doit figurer dans les alertes ≤ 90 jours");
    }

    [TestMethod]
    public void Test_Vente_WithDecimalMonetaryPrecision_DecrementsStockAndLogsAudit()
    {
        var produitService = new ProduitService();
        var stockService = new StockService();
        var venteService = new VenteService();
        var auditService = new AuditService();

        // 1. Création d'un produit avec prix décimal
        decimal prixVente = 12.75m;
        var produit = produitService.EnregistrerProduit(new Produit
        {
            ReferenceUnique = "MED-PAR-1000",
            Designation = "Paracétamol 1000mg",
            Categorie = "Antalgique",
            Dosage = "1000mg",
            Forme = "Comprimé",
            UniteVente = "Boîte",
            PrixVente = prixVente,
            SeuilStockMin = 5
        });

        Assert.AreEqual(prixVente, produit.PrixVente);

        // 2. Réception d'un lot avec coût d'achat décimal
        decimal coutAchat = 6.40m;
        int quantiteInitiale = 20;
        var lot = stockService.ReceptionnerLot(
            produit.Id,
            null,
            "LOT-2026-001",
            DateTime.Today.AddMonths(12),
            quantiteInitiale,
            coutAchat
        );

        Assert.AreEqual(quantiteInitiale, lot.QuantiteActuelle);
        Assert.AreEqual(coutAchat, lot.CoutAchat);

        // 3. Simulation d'une vente de 3 unités
        int quantiteVendue = 3;
        var panier = new List<LignePanierDto>
        {
            new LignePanierDto
            {
                ProduitId = produit.Id,
                DesignationProduit = produit.Designation,
                LotId = lot.Id,
                NumeroLot = lot.NumeroLot,
                Quantite = quantiteVendue,
                PrixUnitaire = produit.PrixVente
            }
        };

        decimal attenduTotal = quantiteVendue * prixVente; // 38.25m
        decimal montantPaye = 50.00m;
        decimal attenduRendu = 11.75m;

        var vente = venteService.ValiderVente(panier, ModePaiement.Especes, montantPaye);

        Assert.AreEqual(attenduTotal, vente.MontantTotal);
        Assert.AreEqual(montantPaye, vente.MontantPaye);
        Assert.AreEqual(attenduRendu, vente.RenduMonnaie);

        // 4. Vérification que le stock du lot a diminué exactement
        var lotApres = stockService.ObtenirLotsParProduit(produit.Id).First(l => l.Id == lot.Id);
        Assert.AreEqual(quantiteInitiale - quantiteVendue, lotApres.QuantiteActuelle);

        // 5. Vérification du journal d'audit
        var audits = auditService.ObtenirDernieresActions();
        Assert.IsTrue(audits.Any(a => a.Action == "VENTE" && a.EntiteId == vente.Id));
    }

    [TestMethod]
    public void Test_Produit_Validation_Et_GestionDoublons_Et_Archivage()
    {
        var produitService = new ProduitService();

        // 1. Création initiale d'un produit
        var p1 = produitService.EnregistrerProduit(new Produit
        {
            ReferenceUnique = "MED-DOUBLON-TEST",
            Designation = "Produit Original",
            Categorie = "Test",
            UniteVente = "Boîte",
            PrixVente = 10.00m
        });

        // 2. Tentative de création d'un doublon avec la même référence
        bool doublonLeve = false;
        try
        {
            produitService.EnregistrerProduit(new Produit
            {
                ReferenceUnique = "med-doublon-test", // Test insensibilité à la casse (COLLATE NOCASE)
                Designation = "Produit Doublon",
                Categorie = "Test",
                UniteVente = "Boîte",
                PrixVente = 15.00m
            });
        }
        catch (GestionPharmacie.BLL.Exceptions.MetierException)
        {
            doublonLeve = true;
        }
        Assert.IsTrue(doublonLeve, "La création d'un doublon de référence doit lever une MetierException");

        // 3. Archivage sans mouvements
        bool ok = produitService.ArchiverProduit(p1.Id, out string msg);
        Assert.IsTrue(ok);
        var pArchive = produitService.ObtenirParId(p1.Id);
        Assert.IsFalse(pArchive.Actif, "Le produit doit être archivé (Actif = false)");
    }

    [TestMethod]
    public void Test_Approvisionnement_BonDeReception_Transactionnel_IncrementeStock()
    {
        var fournisseurService = new FournisseurService();
        var produitService = new ProduitService();
        var stockService = new StockService();

        var four = fournisseurService.EnregistrerFournisseur(new Fournisseur
        {
            Nom = "Fournisseur Test BL",
            Telephone = "0102030405",
            Email = "bl@fournisseur.com"
        });

        var p = produitService.EnregistrerProduit(new Produit
        {
            ReferenceUnique = "MED-RECEPT-01",
            Designation = "Produit Entrée Stock",
            Categorie = "Générique",
            UniteVente = "Boîte",
            PrixVente = 20.00m,
            SeuilStockMin = 5
        });

        int quantiteEntree = 100;
        decimal coutAchat = 11.50m;
        string numLot = "LOT-RECEPT-2026";

        var lignes = new List<LigneReceptionDto>
        {
            new LigneReceptionDto
            {
                ProduitId = p.Id,
                ReferenceProduit = p.ReferenceUnique,
                DesignationProduit = p.Designation,
                NumeroLot = numLot,
                DatePeremption = DateTime.Today.AddMonths(24),
                Quantite = quantiteEntree,
                CoutAchat = coutAchat
            }
        };

        // Exécution de la réception transactionnelle
        stockService.ValiderBonDeReception(four.Id, "BL-TEST-999", lignes);

        // Vérification de l'incrémentation du stock disponible
        var lotCree = stockService.ObtenirLotsParProduit(p.Id).First(l => l.NumeroLot == numLot);
        Assert.AreEqual(quantiteEntree, lotCree.QuantiteActuelle);
        Assert.AreEqual(coutAchat, lotCree.CoutAchat);

        var synthese = produitService.ObtenirSyntheseStocks().First(s => s.ProduitId == p.Id);
        Assert.AreEqual(quantiteEntree, synthese.StockDisponible);
        Assert.IsFalse(synthese.EstEnRupture);
    }
}
