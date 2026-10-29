using System.Drawing;
using System.Windows.Forms;
using GestionPharmacie.BLL.DTOs;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.BLL.Services;
using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;

namespace GestionPharmacie.UI.Forms;

public class MainForm : Form
{
    private readonly ProduitService _produitService;
    private readonly StockService _stockService;
    private readonly VenteService _venteService;
    private readonly FournisseurService _fournisseurService;
    private readonly AuditService _auditService;

    // Controls
    private Label _lblStatUtilisateurs = null!;
    private Label _lblStatProduits = null!;
    private Label _lblStatLots = null!;
    private Label _lblStatAlertes = null!;
    private Label _lblStatCa = null!;
    private Label _lblDbInfo = null!;
    private DataGridView _gridProduits = null!;
    private DataGridView _gridLots = null!;
    private DataGridView _gridAudit = null!;
    private TextBox _txtRechercheProduit = null!;

    public MainForm()
    {
        _produitService = new ProduitService();
        _stockService = new StockService();
        _venteService = new VenteService();
        _fournisseurService = new FournisseurService();
        _auditService = new AuditService();

        InitializeComponent();
        ChargerDonnees();
    }

    private void InitializeComponent()
    {
        Text = "Système de Gestion de Pharmacie - Architecture 3-Tiers (.NET / SQLite)";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1100, 720);
        MinimumSize = new Size(950, 600);
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Header
        var pnlTop = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(15, 23, 42)
        };

        var lblAppTitle = new Label
        {
            Text = "🏥 GESTION PHARMACEUTIQUE",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Location = new Point(20, 15),
            AutoSize = true
        };

        var lblArchitectureBadge = new Label
        {
            Text = "Architecture 3-Tiers : UI (WinForms) ➔ BLL (Métier) ➔ DAL (SQLite .NET)",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 9f),
            Location = new Point(22, 42),
            AutoSize = true
        };

        var lblUserInfo = new Label
        {
            Text = $"Connecté : {SessionUtilisateur.NomUtilisateur} ({SessionUtilisateur.UtilisateurCourant?.Role})",
            ForeColor = Color.FromArgb(56, 189, 248),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(720, 25),
            AutoSize = true
        };

        var btnDeconnexion = new Button
        {
            Text = "Déconnexion",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(960, 18),
            Width = 110,
            Height = 34,
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold)
        };
        btnDeconnexion.FlatAppearance.BorderSize = 0;
        btnDeconnexion.Click += (s, e) => { Close(); };

        pnlTop.Controls.Add(lblAppTitle);
        pnlTop.Controls.Add(lblArchitectureBadge);
        pnlTop.Controls.Add(lblUserInfo);
        pnlTop.Controls.Add(btnDeconnexion);

        // Status bar
        var pnlStatus = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            BackColor = Color.FromArgb(241, 245, 249),
            BorderStyle = BorderStyle.FixedSingle
        };

        _lblDbInfo = new Label
        {
            Text = $"Base SQLite : {DatabaseConnection.Instance.DatabaseFilePath} | Mode WAL : Actif | Clés étrangères : Activées",
            ForeColor = Color.FromArgb(71, 85, 105),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(10, 7),
            AutoSize = true
        };
        pnlStatus.Controls.Add(_lblDbInfo);

        // Main TabControl
        var tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(14, 8),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular)
        };

        // Tabs
        tabControl.TabPages.Add(CreerTabTableauDeBord());
        tabControl.TabPages.Add(CreerTabProduits());
        tabControl.TabPages.Add(CreerTabLots());
        tabControl.TabPages.Add(CreerTabAudit());

        Controls.Add(tabControl);
        Controls.Add(pnlStatus);
        Controls.Add(pnlTop);
    }

    private TabPage CreerTabTableauDeBord()
    {
        var tab = new TabPage("📊 Tableau de bord & KPIs");
        tab.BackColor = Color.White;

        var pnlKpis = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 115,
            Padding = new Padding(15, 15, 15, 5),
            WrapContents = false,
            AutoScroll = true
        };

        _lblStatUtilisateurs = CreerCarteKpi(pnlKpis, "UTILISATEURS", "1", Color.FromArgb(79, 70, 229));
        _lblStatProduits = CreerCarteKpi(pnlKpis, "PRODUITS", "0", Color.FromArgb(16, 185, 129));
        _lblStatLots = CreerCarteKpi(pnlKpis, "LOTS EN STOCK", "0", Color.FromArgb(14, 165, 233));
        _lblStatAlertes = CreerCarteKpi(pnlKpis, "ALERTES STOCK BAS", "0", Color.FromArgb(239, 68, 68));
        _lblStatCa = CreerCarteKpi(pnlKpis, "CHIFFRE D'AFFAIRES (JOUR)", "0,00 €", Color.FromArgb(245, 158, 11));

        var pnlActions = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(25)
        };

        var grpDemo = new GroupBox
        {
            Text = "Validation & Démonstration de l'Architecture 3-Tiers",
            Dock = DockStyle.Top,
            Height = 240,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(20)
        };

        var lblDescription = new Label
        {
            Text = "Ce module démontre l'intégration complète de la chaîne 3-Tiers :\n" +
                   "• DAL : SQLite local, scripts DDL/DML, tables indexées, intégrité référentielle, types monétaires en 'decimal'.\n" +
                   "• BLL : Règles de gestion, hachage PBKDF2-SHA256, gestion FEFO (First Expired First Out), calculs de caisse atomiques.\n" +
                   "• UI : Interface Windows Forms moderne avec synchronisation des données.",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.FromArgb(71, 85, 105),
            Location = new Point(20, 30),
            Size = new Size(800, 80)
        };

        var btnSeedDemo = new Button
        {
            Text = "🌱 Insérer un jeu de données médicales d'exemple (DML)",
            Location = new Point(20, 120),
            Width = 400,
            Height = 42,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSeedDemo.FlatAppearance.BorderSize = 0;
        btnSeedDemo.Click += BtnSeedDemo_Click;

        var btnSimulerVente = new Button
        {
            Text = "🛒 Simuler une vente avec sortie de stock & paiement (Transaction ACID)",
            Location = new Point(20, 172),
            Width = 530,
            Height = 42,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSimulerVente.FlatAppearance.BorderSize = 0;
        btnSimulerVente.Click += BtnSimulerVente_Click;

        grpDemo.Controls.Add(lblDescription);
        grpDemo.Controls.Add(btnSeedDemo);
        grpDemo.Controls.Add(btnSimulerVente);

        pnlActions.Controls.Add(grpDemo);

        tab.Controls.Add(pnlActions);
        tab.Controls.Add(pnlKpis);

        return tab;
    }

    private TabPage CreerTabProduits()
    {
        var tab = new TabPage("💊 Catalogue des Produits");
        tab.BackColor = Color.White;

        var pnlBar = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(15, 10, 15, 10) };

        var lblRecherche = new Label { Text = "Rechercher :", Location = new Point(15, 16), AutoSize = true };
        _txtRechercheProduit = new TextBox { Location = new Point(105, 14), Width = 300 };
        _txtRechercheProduit.TextChanged += (s, e) => FiltrerProduits();

        var btnRefresh = new Button
        {
            Text = "Actualiser",
            Location = new Point(415, 12),
            Width = 100,
            Height = 30,
            BackColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (s, e) => ChargerProduits();

        var btnAlertes = new Button
        {
            Text = "⚠️ Voir stocks bas",
            Location = new Point(525, 12),
            Width = 140,
            Height = 30,
            BackColor = Color.FromArgb(254, 226, 226),
            ForeColor = Color.FromArgb(185, 28, 28),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnAlertes.FlatAppearance.BorderSize = 0;
        btnAlertes.Click += (s, e) =>
        {
            var alertes = _produitService.ObtenirAlertesStockBas();
            _gridProduits.DataSource = alertes;
        };

        pnlBar.Controls.Add(lblRecherche);
        pnlBar.Controls.Add(_txtRechercheProduit);
        pnlBar.Controls.Add(btnRefresh);
        pnlBar.Controls.Add(btnAlertes);

        _gridProduits = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false
        };

        tab.Controls.Add(_gridProduits);
        tab.Controls.Add(pnlBar);
        return tab;
    }

    private TabPage CreerTabLots()
    {
        var tab = new TabPage("📦 Lots & Péremptions (FEFO)");
        tab.BackColor = Color.White;

        var pnlBar = new Panel { Dock = DockStyle.Top, Height = 55, Padding = new Padding(15, 10, 15, 10) };

        var btnRefreshLots = new Button
        {
            Text = "Tous les lots",
            Location = new Point(15, 12),
            Width = 120,
            Height = 30,
            BackColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat
        };
        btnRefreshLots.FlatAppearance.BorderSize = 0;
        btnRefreshLots.Click += (s, e) => ChargerLots();

        var btnPeremptions = new Button
        {
            Text = "🚨 Péremption < 60 jours",
            Location = new Point(145, 12),
            Width = 190,
            Height = 30,
            BackColor = Color.FromArgb(254, 243, 199),
            ForeColor = Color.FromArgb(180, 83, 9),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold)
        };
        btnPeremptions.FlatAppearance.BorderSize = 0;
        btnPeremptions.Click += (s, e) =>
        {
            _gridLots.DataSource = _stockService.ObtenirLotsProchesPeremption(60);
        };

        pnlBar.Controls.Add(btnRefreshLots);
        pnlBar.Controls.Add(btnPeremptions);

        _gridLots = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false
        };

        tab.Controls.Add(_gridLots);
        tab.Controls.Add(pnlBar);
        return tab;
    }

    private TabPage CreerTabAudit()
    {
        var tab = new TabPage("🛡️ Journal d'Audit & Sécurité");
        tab.BackColor = Color.White;

        var pnlBar = new Panel { Dock = DockStyle.Top, Height = 50, Padding = new Padding(15, 10, 15, 10) };
        var btnRefreshAudit = new Button
        {
            Text = "Actualiser l'audit",
            Location = new Point(15, 10),
            Width = 140,
            Height = 30,
            BackColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat
        };
        btnRefreshAudit.FlatAppearance.BorderSize = 0;
        btnRefreshAudit.Click += (s, e) => ChargerAudit();
        pnlBar.Controls.Add(btnRefreshAudit);

        _gridAudit = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false
        };

        tab.Controls.Add(_gridAudit);
        tab.Controls.Add(pnlBar);
        return tab;
    }

    private static Label CreerCarteKpi(FlowLayoutPanel parent, string titre, string valeurInitiale, Color couleurAccent)
    {
        var panel = new Panel
        {
            Width = 195,
            Height = 85,
            BackColor = Color.FromArgb(248, 250, 252),
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(0, 0, 15, 0)
        };

        var pnlBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 4,
            BackColor = couleurAccent
        };

        var lblTitre = new Label
        {
            Text = titre,
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Location = new Point(10, 12),
            AutoSize = true
        };

        var lblVal = new Label
        {
            Text = valeurInitiale,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 14f, FontStyle.Bold),
            Location = new Point(10, 34),
            AutoSize = true
        };

        panel.Controls.Add(pnlBar);
        panel.Controls.Add(lblTitre);
        panel.Controls.Add(lblVal);

        parent.Controls.Add(panel);
        return lblVal;
    }

    private void ChargerDonnees()
    {
        ChargerProduits();
        ChargerLots();
        ChargerAudit();
        RafraichirKpis();
    }

    private void ChargerProduits()
    {
        var produits = _produitService.ObtenirTousLesProduits(false);
        _gridProduits.DataSource = produits;
    }

    private void FiltrerProduits()
    {
        string q = _txtRechercheProduit.Text.Trim();
        _gridProduits.DataSource = _produitService.Rechercher(q);
    }

    private void ChargerLots()
    {
        var lots = _stockService.ObtenirTousLesLots();
        _gridLots.DataSource = lots;
    }

    private void ChargerAudit()
    {
        var audits = _auditService.ObtenirDernieresActions();
        _gridAudit.DataSource = audits;
    }

    private void RafraichirKpis()
    {
        var userRepo = new DAL.Repositories.UtilisateurRepository();
        int nbUsers = userRepo.GetAll().Count;
        int nbProduits = _produitService.ObtenirTousLesProduits().Count;
        int nbLots = _stockService.ObtenirTousLesLots().Count;
        int nbAlertes = _produitService.ObtenirAlertesStockBas().Count;
        decimal caJour = _venteService.ObtenirChiffreAffairesAujourdhui();

        _lblStatUtilisateurs.Text = nbUsers.ToString();
        _lblStatProduits.Text = nbProduits.ToString();
        _lblStatLots.Text = nbLots.ToString();
        _lblStatAlertes.Text = nbAlertes.ToString();
        _lblStatCa.Text = caJour.ToString("C2");
    }

    private void BtnSeedDemo_Click(object? sender, EventArgs e)
    {
        try
        {
            // 1. Fournisseur
            var f1 = _fournisseurService.EnregistrerFournisseur(new Fournisseur
            {
                Nom = "Laboratoires Sanofi France",
                Telephone = "+33 1 53 77 40 00",
                Email = "contact@sanofi.com",
                Adresse = "Paris, France"
            });

            var f2 = _fournisseurService.EnregistrerFournisseur(new Fournisseur
            {
                Nom = "GSK Distribution Pharmaceutique",
                Telephone = "+33 1 39 17 80 00",
                Email = "info@gsk.com",
                Adresse = "Rueil-Malmaison, France"
            });

            // 2. Produits avec décimaux
            var p1 = _produitService.EnregistrerProduit(new Produit
            {
                ReferenceUnique = "MED-DOL-1000",
                Designation = "Doliprane 1000mg",
                Categorie = "Antalgique / Antipyrétique",
                Dosage = "1000mg",
                Forme = "Comprimé",
                UniteVente = "Boîte de 8",
                PrixVente = 2.45m,
                SeuilStockMin = 10
            });

            var p2 = _produitService.EnregistrerProduit(new Produit
            {
                ReferenceUnique = "MED-AMX-500",
                Designation = "Amoxicilline Biogaran 500mg",
                Categorie = "Antibiotique",
                Dosage = "500mg",
                Forme = "Gélule",
                UniteVente = "Boîte de 12",
                PrixVente = 4.80m,
                SeuilStockMin = 5
            });

            var p3 = _produitService.EnregistrerProduit(new Produit
            {
                ReferenceUnique = "MED-IBU-400",
                Designation = "Ibuprofène Mylan 400mg",
                Categorie = "Anti-inflammatoire",
                Dosage = "400mg",
                Forme = "Comprimé",
                UniteVente = "Boîte de 10",
                PrixVente = 3.65m,
                SeuilStockMin = 8
            });

            // 3. Lots avec règles de gestion FEFO et coûts décimaux
            _stockService.ReceptionnerLot(p1.Id, f1.Id, "LOT-DOL-2026A", DateTime.Today.AddMonths(18), 50, 1.20m);
            _stockService.ReceptionnerLot(p1.Id, f1.Id, "LOT-DOL-2026B", DateTime.Today.AddMonths(24), 30, 1.25m);
            _stockService.ReceptionnerLot(p2.Id, f2.Id, "LOT-AMX-2026-X", DateTime.Today.AddMonths(12), 40, 2.50m);
            _stockService.ReceptionnerLot(p3.Id, f1.Id, "LOT-IBU-ALERT", DateTime.Today.AddDays(45), 15, 1.90m);

            ChargerDonnees();
            MessageBox.Show("Données médicales d'exemple insérées avec succès !", "Succès DML", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur lors de l'insertion : " + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnSimulerVente_Click(object? sender, EventArgs e)
    {
        try
        {
            var lots = _stockService.ObtenirTousLesLots();
            var lotDisponible = lots.FirstOrDefault(l => l.QuantiteActuelle >= 2 && l.Statut == StatutLot.Normal);

            if (lotDisponible == null)
            {
                MessageBox.Show("Veuillez d'abord insérer les données d'exemple pour disposer de stock.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var produit = _produitService.ObtenirParId(lotDisponible.ProduitId);

            var panier = new List<LignePanierDto>
            {
                new LignePanierDto
                {
                    ProduitId = produit.Id,
                    DesignationProduit = produit.Designation,
                    LotId = lotDisponible.Id,
                    NumeroLot = lotDisponible.NumeroLot,
                    Quantite = 2,
                    PrixUnitaire = produit.PrixVente
                }
            };

            decimal totalVente = panier.Sum(x => x.SousTotal);
            decimal paye = totalVente + 5.00m; // Montant supérieur pour tester le rendu de monnaie

            var vente = _venteService.ValiderVente(panier, ModePaiement.Especes, paye);

            ChargerDonnees();

            MessageBox.Show(
                $"Vente validée avec succès !\n" +
                $"• N° Facture : {vente.NumeroFacture}\n" +
                $"• Article : {produit.Designation} x 2\n" +
                $"• Total : {vente.MontantTotal:C2}\n" +
                $"• Payé : {vente.MontantPaye:C2}\n" +
                $"• Rendu monnaie : {vente.RenduMonnaie:C2}\n" +
                $"• Stock décrémenté et audit tracé dans SQLite.",
                "Transaction Vente Réussie",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur de vente : " + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
