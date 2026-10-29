using System.Drawing;
using System.Windows.Forms;
using GestionPharmacie.BLL.DTOs;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.BLL.Services;
using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;

namespace GestionPharmacie.UI.Forms;

/// <summary>
/// Tableau de bord principal optimisé pour une résolution minimale de 1366x768,
/// avec menu latéral stable, gestion des produits, fiches fournisseurs, approvisionnements / réceptions de stock,
/// indicateurs clés en temps réel (CA, Ruptures, Stocks faibles, Péremptions 90j),
/// et masquage automatique des données sensibles pour le profil Vendeur.
/// </summary>
public class DashboardForm : Form
{
    private readonly ProduitService _produitService;
    private readonly StockService _stockService;
    private readonly VenteService _venteService;
    private readonly FournisseurService _fournisseurService;
    private readonly AuditService _auditService;

    // Paramètre du délai de péremption (90 jours par défaut)
    private int _delaiPeremptionJours = 90;

    // Sidebar navigation buttons
    private readonly List<Button> _navButtons = new();
    private Button _btnNavDashboard = null!;
    private Button _btnNavProduits = null!;
    private Button _btnNavFournisseurs = null!;
    private Button _btnNavReceptions = null!;
    private Button _btnNavLots = null!;
    private Button _btnNavCaisse = null!;
    private Button _btnNavAudit = null!;
    private Button _btnNavParametres = null!;

    // Main content panels container
    private Panel _pnlContent = null!;
    private Panel _viewDashboard = null!;
    private Panel _viewProduits = null!;
    private Panel _viewFournisseurs = null!;
    private Panel _viewReceptions = null!;
    private Panel _viewLots = null!;
    private Panel _viewCaisse = null!;
    private Panel _viewAudit = null!;
    private Panel _viewParametres = null!;

    // Dashboard KPI Labels
    private Label _lblKpiCa = null!;
    private Label _lblKpiRuptures = null!;
    private Label _lblKpiStocksFaibles = null!;
    private Label _lblKpiPeremptions = null!;
    private Label _lblKpiTitrePeremption = null!;

    // Dashboard DataGridViews
    private DataGridView _gridAlertesStock = null!;
    private DataGridView _gridAlertesPeremption = null!;
    private NumericUpDown _numDelaiPeremption = null!;

    // Produits View Controls
    private DataGridView _gridCatalogueProduits = null!;
    private ComboBox _cboFiltreCategorieProduit = null!;
    private ComboBox _cboFiltreStatutProduit = null!;
    private TextBox _txtRechercheProduit = null!;

    // Fournisseurs View Controls
    private DataGridView _gridFournisseurs = null!;
    private TextBox _txtRechercheFournisseur = null!;
    private ComboBox _cboFiltreStatutFournisseur = null!;

    // Receptions View Controls & Multi-lines Basket
    private ComboBox _cboReceptionFournisseur = null!;
    private TextBox _txtReceptionBL = null!;
    private ComboBox _cboReceptionProduit = null!;
    private TextBox _txtReceptionLot = null!;
    private DateTimePicker _dtpReceptionPeremption = null!;
    private NumericUpDown _numReceptionQte = null!;
    private NumericUpDown _numReceptionCout = null!;
    private DataGridView _gridBonReceptionLignes = null!;
    private Label _lblTotalBonReception = null!;
    private readonly List<LigneReceptionDto> _lignesBonReceptionEnCours = new();

    // Lots View Controls
    private DataGridView _gridLotsStock = null!;

    // Caisse & Audit
    private DataGridView _gridJournalAudit = null!;
    private DataGridView _gridVentesRecentes = null!;
    private ComboBox _cboProduitVente = null!;
    private NumericUpDown _numQteVente = null!;
    private Label _lblTotalVenteSimulee = null!;

    public DashboardForm()
    {
        _produitService = new ProduitService();
        _stockService = new StockService();
        _venteService = new VenteService();
        _fournisseurService = new FournisseurService();
        _auditService = new AuditService();

        InitializeComponent();
        AppliquerRestrictionsRole();
        ChargerToutesLesDonnees();
        BasculerVue(_viewDashboard, _btnNavDashboard);
    }

    private void InitializeComponent()
    {
        Text = "Système de Gestion Pharmaceutique - Officine & Approvisionnements";
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(1366, 768);
        MinimumSize = new Size(1366, 768);
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // 1. Menu Latéral Stable (Sidebar - 250px)
        var pnlSidebar = CreerSidebar();

        // 2. Container de Contenu Principal (remplit le reste de l'écran)
        _pnlContent = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.FromArgb(241, 245, 249)
        };

        // 3. Création des différentes vues (écrans modulaires)
        _viewDashboard = CreerVueDashboard();
        _viewProduits = CreerVueProduits();
        _viewFournisseurs = CreerVueFournisseurs();
        _viewReceptions = CreerVueReceptions();
        _viewLots = CreerVueLots();
        _viewCaisse = CreerVueCaisse();
        _viewAudit = CreerVueAudit();
        _viewParametres = CreerVueParametres();

        _pnlContent.Controls.Add(_viewDashboard);
        _pnlContent.Controls.Add(_viewProduits);
        _pnlContent.Controls.Add(_viewFournisseurs);
        _pnlContent.Controls.Add(_viewReceptions);
        _pnlContent.Controls.Add(_viewLots);
        _pnlContent.Controls.Add(_viewCaisse);
        _pnlContent.Controls.Add(_viewAudit);
        _pnlContent.Controls.Add(_viewParametres);

        Controls.Add(_pnlContent);
        Controls.Add(pnlSidebar);
    }

    #region Construction de la Sidebar & Navigation

    private Panel CreerSidebar()
    {
        var sidebar = new Panel
        {
            Dock = DockStyle.Left,
            Width = 260,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(0)
        };

        // Header : Logo & Titre
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(10, 15, 30),
            Padding = new Padding(18, 15, 18, 10)
        };

        var lblBrand = new Label
        {
            Text = "⚕️ PHARMA-GEST",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 13.5f, FontStyle.Bold),
            Location = new Point(16, 16),
            AutoSize = true
        };

        var lblSubBrand = new Label
        {
            Text = "Officine & Traçabilité FEFO",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(18, 44),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblBrand);
        pnlHeader.Controls.Add(lblSubBrand);

        // Carte Utilisateur & Badge Rôle
        var pnlUserBadge = new Panel
        {
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(15, 12, 15, 12)
        };

        var lblUserName = new Label
        {
            Text = SessionUtilisateur.NomUtilisateur,
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Location = new Point(14, 12),
            AutoSize = true
        };

        bool estAdmin = SessionUtilisateur.EstAdmin;
        var lblRoleBadge = new Label
        {
            Text = estAdmin ? "👑 ADMINISTRATEUR" : "🛒 VENDEUR",
            ForeColor = estAdmin ? Color.FromArgb(192, 132, 252) : Color.FromArgb(52, 211, 153),
            BackColor = estAdmin ? Color.FromArgb(88, 28, 135) : Color.FromArgb(6, 78, 59),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Padding = new Padding(6, 3, 6, 3),
            Location = new Point(14, 38),
            AutoSize = true
        };

        pnlUserBadge.Controls.Add(lblUserName);
        pnlUserBadge.Controls.Add(lblRoleBadge);

        // Conteneur des Boutons de Navigation
        var pnlNav = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(0, 10, 0, 10)
        };

        _btnNavDashboard = CreerBoutonNav("  📊  Tableau de Bord", () => BasculerVue(_viewDashboard, _btnNavDashboard));
        _btnNavProduits = CreerBoutonNav("  💊  Gestion Produits", () => BasculerVue(_viewProduits, _btnNavProduits));
        _btnNavFournisseurs = CreerBoutonNav("  🏢  Fournisseurs", () => BasculerVue(_viewFournisseurs, _btnNavFournisseurs));
        _btnNavReceptions = CreerBoutonNav("  📥  Approvisionnements", () => BasculerVue(_viewReceptions, _btnNavReceptions));
        _btnNavLots = CreerBoutonNav("  📦  Lots & Péremptions", () => BasculerVue(_viewLots, _btnNavLots));
        _btnNavCaisse = CreerBoutonNav("  🛒  Caisse & Ventes", () => BasculerVue(_viewCaisse, _btnNavCaisse));
        _btnNavAudit = CreerBoutonNav("  🛡️  Journal d'Audit", () => BasculerVue(_viewAudit, _btnNavAudit));
        _btnNavParametres = CreerBoutonNav("  ⚙️  Configuration", () => BasculerVue(_viewParametres, _btnNavParametres));

        pnlNav.Controls.Add(_btnNavParametres);
        pnlNav.Controls.Add(_btnNavAudit);
        pnlNav.Controls.Add(_btnNavCaisse);
        pnlNav.Controls.Add(_btnNavLots);
        pnlNav.Controls.Add(_btnNavReceptions);
        pnlNav.Controls.Add(_btnNavFournisseurs);
        pnlNav.Controls.Add(_btnNavProduits);
        pnlNav.Controls.Add(_btnNavDashboard);

        // Footer avec bouton de Déconnexion
        var pnlFooter = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 65,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(15, 12, 15, 12)
        };

        var btnDeconnexion = new Button
        {
            Dock = DockStyle.Fill,
            Text = "🚪 Déconnexion",
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnDeconnexion.FlatAppearance.BorderSize = 0;
        btnDeconnexion.Click += (s, e) =>
        {
            if (MessageBox.Show("Voulez-vous fermer votre session ?", "Déconnexion", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                SessionUtilisateur.FermerSession();
                Close();
            }
        };
        pnlFooter.Controls.Add(btnDeconnexion);

        sidebar.Controls.Add(pnlNav);
        sidebar.Controls.Add(pnlFooter);
        sidebar.Controls.Add(pnlUserBadge);
        sidebar.Controls.Add(pnlHeader);

        return sidebar;
    }

    private Button CreerBoutonNav(string titre, Action onAction)
    {
        var btn = new Button
        {
            Dock = DockStyle.Top,
            Height = 46,
            Text = titre,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(18, 0, 0, 0),
            BackColor = Color.FromArgb(15, 23, 42),
            ForeColor = Color.FromArgb(203, 213, 225),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.Click += (s, e) => onAction();
        _navButtons.Add(btn);
        return btn;
    }

    private void BasculerVue(Panel vueCible, Button boutonActif)
    {
        _viewDashboard.Visible = false;
        _viewProduits.Visible = false;
        _viewFournisseurs.Visible = false;
        _viewReceptions.Visible = false;
        _viewLots.Visible = false;
        _viewCaisse.Visible = false;
        _viewAudit.Visible = false;
        _viewParametres.Visible = false;

        vueCible.Visible = true;
        vueCible.BringToFront();

        foreach (var btn in _navButtons)
        {
            if (btn == boutonActif)
            {
                btn.BackColor = Color.FromArgb(30, 41, 59);
                btn.ForeColor = Color.White;
                btn.Font = new Font("Segoe UI", 10f, FontStyle.Bold);
            }
            else
            {
                btn.BackColor = Color.FromArgb(15, 23, 42);
                btn.ForeColor = Color.FromArgb(203, 213, 225);
                btn.Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);
            }
        }
    }

    #endregion

    #region Masquage et Restrictions de Rôle

    private void AppliquerRestrictionsRole()
    {
        bool estAdmin = SessionUtilisateur.EstAdmin;

        if (!estAdmin)
        {
            _btnNavAudit.Visible = false;
            _btnNavParametres.Visible = false;

            var colLot = _gridLotsStock.Columns["CoutAchat"];
            if (colLot != null) colLot.Visible = false;

            var colAlerte = _gridAlertesPeremption.Columns["CoutAchat"];
            if (colAlerte != null) colAlerte.Visible = false;

            if (_numReceptionCout != null)
            {
                _numReceptionCout.Visible = false;
            }
        }
    }

    #endregion

    #region Vue 1 : Tableau de Bord Principal

    private Panel CreerVueDashboard()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 20, 25, 20)
        };

        var pnlTopBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 65,
            BackColor = Color.White,
            Padding = new Padding(20, 10, 20, 10)
        };

        var lblTitreDash = new Label
        {
            Text = "TABLEAU DE BORD OPÉRATIONNEL",
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 13f, FontStyle.Bold),
            Location = new Point(15, 12),
            AutoSize = true
        };

        var lblSousTitreDash = new Label
        {
            Text = $"Dernière mise à jour : {DateTime.Now:dd/MM/yyyy HH:mm} | Clés étrangères SQLite : Activées | Mode WAL : Actif",
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(17, 38),
            AutoSize = true
        };

        var lblParamPeremption = new Label
        {
            Text = "Délai alerte péremption :",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(710, 22),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        _numDelaiPeremption = new NumericUpDown
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(875, 19),
            Width = 65,
            Minimum = 15,
            Maximum = 365,
            Value = _delaiPeremptionJours,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };
        _numDelaiPeremption.ValueChanged += (s, e) =>
        {
            _delaiPeremptionJours = (int)_numDelaiPeremption.Value;
            _lblKpiTitrePeremption.Text = $"PÉREMPTIONS (≤ {_delaiPeremptionJours}J)";
            RafraichirKpisEtAlertes();
        };

        var lblJours = new Label
        {
            Text = "jours",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(945, 22),
            AutoSize = true,
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        var btnRefresh = new Button
        {
            Text = "🔄 Actualiser",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(995, 15),
            Width = 105,
            Height = 34,
            BackColor = Color.FromArgb(226, 232, 240),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnRefresh.FlatAppearance.BorderSize = 0;
        btnRefresh.Click += (s, e) => ChargerToutesLesDonnees();

        pnlTopBar.Controls.Add(lblTitreDash);
        pnlTopBar.Controls.Add(lblSousTitreDash);
        pnlTopBar.Controls.Add(lblParamPeremption);
        pnlTopBar.Controls.Add(_numDelaiPeremption);
        pnlTopBar.Controls.Add(lblJours);
        pnlTopBar.Controls.Add(btnRefresh);

        // Section KPIs (4 Indicateurs clés)
        var tblKpis = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 115,
            ColumnCount = 4,
            RowCount = 1,
            Padding = new Padding(0, 15, 0, 15)
        };
        tblKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));
        tblKpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));

        var cardCa = CreerKpiCard("CHIFFRE D'AFFAIRES (JOUR)", "0,00 €", "Encaissements du jour", Color.FromArgb(16, 185, 129), out _lblKpiCa, out _);
        var cardRuptures = CreerKpiCard("RUPTURES DE STOCK", "0", "Articles à 0 unité", Color.FromArgb(239, 68, 68), out _lblKpiRuptures, out _);
        var cardStocksFaibles = CreerKpiCard("STOCKS FAIBLES", "0", "Stock ≤ seuil min", Color.FromArgb(249, 115, 22), out _lblKpiStocksFaibles, out _);
        var cardPeremptions = CreerKpiCard($"PÉREMPTIONS (≤ {_delaiPeremptionJours}J)", "0", "Traçabilité FEFO", Color.FromArgb(245, 158, 11), out _lblKpiPeremptions, out _lblKpiTitrePeremption);

        tblKpis.Controls.Add(cardCa, 0, 0);
        tblKpis.Controls.Add(cardRuptures, 1, 0);
        tblKpis.Controls.Add(cardStocksFaibles, 2, 0);
        tblKpis.Controls.Add(cardPeremptions, 3, 0);

        // Section Grilles d'Alertes
        var tblAlertes = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            Padding = new Padding(0, 10, 0, 0)
        };
        tblAlertes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
        tblAlertes.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));

        var pnlAlertesStock = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(0, 0, 10, 0)
        };
        var lblTitreAlerteStock = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "⚠️ Alertes de Stocks (Ruptures & Niveaux Critiques)",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        _gridAlertesStock = CreerDataGridViewStandard();
        pnlAlertesStock.Controls.Add(_gridAlertesStock);
        pnlAlertesStock.Controls.Add(lblTitreAlerteStock);

        var pnlAlertesPeremption = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(10, 0, 0, 0)
        };
        var lblTitreAlertePeremption = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "🚨 Lots Proches de la Péremption (FEFO)",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };
        _gridAlertesPeremption = CreerDataGridViewStandard();
        pnlAlertesPeremption.Controls.Add(_gridAlertesPeremption);
        pnlAlertesPeremption.Controls.Add(lblTitreAlertePeremption);

        tblAlertes.Controls.Add(pnlAlertesStock, 0, 0);
        tblAlertes.Controls.Add(pnlAlertesPeremption, 1, 0);

        panel.Controls.Add(tblAlertes);
        panel.Controls.Add(tblKpis);
        panel.Controls.Add(pnlTopBar);

        return panel;
    }

    private static Panel CreerKpiCard(string titre, string valeur, string sousTitre, Color couleurAccent, out Label lblValeur, out Label lblTitreOut)
    {
        var card = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Margin = new Padding(6),
            Padding = new Padding(15, 12, 15, 10)
        };

        var pnlBordure = new Panel
        {
            Dock = DockStyle.Left,
            Width = 5,
            BackColor = couleurAccent
        };

        var lblT = new Label
        {
            Text = titre,
            ForeColor = Color.FromArgb(100, 116, 139),
            Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
            Location = new Point(18, 12),
            AutoSize = true
        };
        lblTitreOut = lblT;

        var lblV = new Label
        {
            Text = valeur,
            ForeColor = Color.FromArgb(15, 23, 42),
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            Location = new Point(16, 32),
            AutoSize = true
        };
        lblValeur = lblV;

        var lblSub = new Label
        {
            Text = sousTitre,
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8f),
            Location = new Point(18, 62),
            AutoSize = true
        };

        card.Controls.Add(pnlBordure);
        card.Controls.Add(lblT);
        card.Controls.Add(lblV);
        card.Controls.Add(lblSub);

        return card;
    }

    #endregion

    #region Vue 2 : Gestion des Produits (Filtres Catégorie/Statut, Recherche, CRUD & Archivage)

    private Panel CreerVueProduits()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 20, 25, 20)
        };

        // Barre d'outils, filtres et actions
        var pnlBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.White,
            Padding = new Padding(15, 10, 15, 10)
        };

        var lblRech = new Label { Text = "Recherche rapide :", Location = new Point(15, 14), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtRechercheProduit = new TextBox { Location = new Point(15, 34), Width = 220 };
        _txtRechercheProduit.TextChanged += (s, e) => FiltrerCatalogueProduits();

        var lblCat = new Label { Text = "Catégorie :", Location = new Point(250, 14), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cboFiltreCategorieProduit = new ComboBox { Location = new Point(250, 34), Width = 190, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboFiltreCategorieProduit.SelectedIndexChanged += (s, e) => FiltrerCatalogueProduits();

        var lblStatut = new Label { Text = "Statut :", Location = new Point(455, 14), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cboFiltreStatutProduit = new ComboBox { Location = new Point(455, 34), Width = 140, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboFiltreStatutProduit.Items.AddRange(new object[] { "Actifs uniquement", "Archivés uniquement", "Tous les produits" });
        _cboFiltreStatutProduit.SelectedIndex = 0;
        _cboFiltreStatutProduit.SelectedIndexChanged += (s, e) => FiltrerCatalogueProduits();

        // Boutons CRUD
        var btnAjouter = new Button
        {
            Text = "➕ Nouveau Produit",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(680, 28),
            Width = 145,
            Height = 36,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAjouter.FlatAppearance.BorderSize = 0;
        btnAjouter.Click += BtnAjouterProduit_Click;

        var btnModifier = new Button
        {
            Text = "✏️ Modifier",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(835, 28),
            Width = 110,
            Height = 36,
            BackColor = Color.FromArgb(24, 119, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnModifier.FlatAppearance.BorderSize = 0;
        btnModifier.Click += BtnModifierProduit_Click;

        var btnArchiver = new Button
        {
            Text = "🗄️ Archiver / Activer",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(955, 28),
            Width = 140,
            Height = 36,
            BackColor = Color.FromArgb(239, 68, 68),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnArchiver.FlatAppearance.BorderSize = 0;
        btnArchiver.Click += BtnArchiverProduit_Click;

        pnlBar.Controls.Add(lblRech);
        pnlBar.Controls.Add(_txtRechercheProduit);
        pnlBar.Controls.Add(lblCat);
        pnlBar.Controls.Add(_cboFiltreCategorieProduit);
        pnlBar.Controls.Add(lblStatut);
        pnlBar.Controls.Add(_cboFiltreStatutProduit);
        pnlBar.Controls.Add(btnAjouter);
        pnlBar.Controls.Add(btnModifier);
        pnlBar.Controls.Add(btnArchiver);

        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0)
        };

        _gridCatalogueProduits = CreerDataGridViewStandard();
        _gridCatalogueProduits.CellDoubleClick += (s, e) => BtnModifierProduit_Click(s, e);
        pnlGrid.Controls.Add(_gridCatalogueProduits);

        panel.Controls.Add(pnlGrid);
        panel.Controls.Add(pnlBar);

        return panel;
    }

    private void FiltrerCatalogueProduits()
    {
        string recherche = _txtRechercheProduit.Text.Trim().ToLowerInvariant();
        string catSelectionnee = _cboFiltreCategorieProduit.SelectedItem?.ToString() ?? "Toutes les catégories";
        int statutIdx = _cboFiltreStatutProduit.SelectedIndex;

        // Charger tous les produits
        var tous = _produitService.ObtenirTousLesProduits(false);

        var filtre = tous.Where(p =>
        {
            // Statut : 0 = Actifs, 1 = Archivés, 2 = Tous
            if (statutIdx == 0 && !p.Actif) return false;
            if (statutIdx == 1 && p.Actif) return false;

            // Catégorie
            if (catSelectionnee != "Toutes les catégories" && p.Categorie != catSelectionnee) return false;

            // Recherche
            if (!string.IsNullOrEmpty(recherche))
            {
                bool match = p.ReferenceUnique.ToLowerInvariant().Contains(recherche) ||
                             p.Designation.ToLowerInvariant().Contains(recherche) ||
                             p.Categorie.ToLowerInvariant().Contains(recherche) ||
                             p.Dosage.ToLowerInvariant().Contains(recherche);
                if (!match) return false;
            }

            return true;
        }).Select(p => new
        {
            p.Id,
            Référence = p.ReferenceUnique,
            Désignation = p.Designation,
            Catégorie = p.Categorie,
            Dosage = p.Dosage,
            Forme = p.Forme,
            Unité = p.UniteVente,
            PrixVente = p.PrixVente.ToString("C2"),
            SeuilMin = p.SeuilStockMin,
            Statut = p.Actif ? "Actif" : "Archivé"
        }).ToList();

        _gridCatalogueProduits.DataSource = filtre;
    }

    private Produit? ObtenirProduitSelectionne()
    {
        if (_gridCatalogueProduits.SelectedRows.Count == 0) return null;
        var row = _gridCatalogueProduits.SelectedRows[0];
        if (row.Cells["Id"].Value is int id)
        {
            return _produitService.ObtenirParId(id);
        }
        return null;
    }

    private void BtnAjouterProduit_Click(object? sender, EventArgs e)
    {
        using var form = new ProduitEditForm(null, _produitService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            ChargerCategoriesComboProduit();
            ChargerCatalogue();
            RafraichirKpisEtAlertes();
            MessageBox.Show($"Produit '{form.ProduitEnregistre?.Designation}' créé avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnModifierProduit_Click(object? sender, EventArgs e)
    {
        var prod = ObtenirProduitSelectionne();
        if (prod == null)
        {
            MessageBox.Show("Veuillez sélectionner un produit à modifier.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new ProduitEditForm(prod, _produitService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            ChargerCategoriesComboProduit();
            ChargerCatalogue();
            RafraichirKpisEtAlertes();
            MessageBox.Show($"Produit '{prod.Designation}' mis à jour avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnArchiverProduit_Click(object? sender, EventArgs e)
    {
        var prod = ObtenirProduitSelectionne();
        if (prod == null)
        {
            MessageBox.Show("Veuillez sélectionner un produit.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (prod.Actif)
        {
            var res = MessageBox.Show(
                $"Êtes-vous sûr de vouloir archiver le produit '{prod.Designation}' (Réf: {prod.ReferenceUnique}) ?\n" +
                $"Si des mouvements de stock ou ventes existent, une suppression logique sera appliquée.",
                "Confirmation d'Archivage",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (res == DialogResult.Yes)
            {
                _produitService.ArchiverProduit(prod.Id, out string msg);
                ChargerCatalogue();
                RafraichirKpisEtAlertes();
                MessageBox.Show(msg, "Produit Archivé", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
        else
        {
            var res = MessageBox.Show(
                $"Le produit '{prod.Designation}' est actuellement archivé. Souhaitez-vous le réactiver ?",
                "Réactivation Produit",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question
            );

            if (res == DialogResult.Yes)
            {
                _produitService.ActiverProduit(prod.Id);
                ChargerCatalogue();
                RafraichirKpisEtAlertes();
                MessageBox.Show($"Le produit '{prod.Designation}' est de nouveau actif et disponible à la vente.", "Produit Réactivé", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
        }
    }

    #endregion

    #region Vue 3 : Gestion des Fournisseurs (Fiches signalétiques complètes)

    private Panel CreerVueFournisseurs()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 20, 25, 20)
        };

        var pnlBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 75,
            BackColor = Color.White,
            Padding = new Padding(15, 12, 15, 12)
        };

        var lblRech = new Label { Text = "Recherche fournisseur :", Location = new Point(15, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _txtRechercheFournisseur = new TextBox { Location = new Point(15, 32), Width = 260 };
        _txtRechercheFournisseur.TextChanged += (s, e) => FiltrerFournisseurs();

        var lblStatut = new Label { Text = "Statut :", Location = new Point(290, 12), AutoSize = true, Font = new Font("Segoe UI", 8.5f, FontStyle.Bold) };
        _cboFiltreStatutFournisseur = new ComboBox { Location = new Point(290, 32), Width = 150, DropDownStyle = ComboBoxStyle.DropDownList };
        _cboFiltreStatutFournisseur.Items.AddRange(new object[] { "Actifs uniquement", "Tous les fournisseurs" });
        _cboFiltreStatutFournisseur.SelectedIndex = 0;
        _cboFiltreStatutFournisseur.SelectedIndexChanged += (s, e) => FiltrerFournisseurs();

        var btnAjouter = new Button
        {
            Text = "➕ Nouveau Fournisseur",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(680, 24),
            Width = 160,
            Height = 36,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAjouter.FlatAppearance.BorderSize = 0;
        btnAjouter.Click += BtnAjouterFournisseur_Click;

        var btnModifier = new Button
        {
            Text = "✏️ Modifier Fiche",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(850, 24),
            Width = 125,
            Height = 36,
            BackColor = Color.FromArgb(24, 119, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnModifier.FlatAppearance.BorderSize = 0;
        btnModifier.Click += BtnModifierFournisseur_Click;

        var btnChangerStatut = new Button
        {
            Text = "🔄 Activer / Désactiver",
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
            Location = new Point(985, 24),
            Width = 145,
            Height = 36,
            BackColor = Color.FromArgb(245, 158, 11),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnChangerStatut.FlatAppearance.BorderSize = 0;
        btnChangerStatut.Click += BtnChangerStatutFournisseur_Click;

        pnlBar.Controls.Add(lblRech);
        pnlBar.Controls.Add(_txtRechercheFournisseur);
        pnlBar.Controls.Add(lblStatut);
        pnlBar.Controls.Add(_cboFiltreStatutFournisseur);
        pnlBar.Controls.Add(btnAjouter);
        pnlBar.Controls.Add(btnModifier);
        pnlBar.Controls.Add(btnChangerStatut);

        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0)
        };

        _gridFournisseurs = CreerDataGridViewStandard();
        _gridFournisseurs.CellDoubleClick += (s, e) => BtnModifierFournisseur_Click(s, e);
        pnlGrid.Controls.Add(_gridFournisseurs);

        panel.Controls.Add(pnlGrid);
        panel.Controls.Add(pnlBar);

        return panel;
    }

    private void FiltrerFournisseurs()
    {
        string q = _txtRechercheFournisseur.Text.Trim();
        bool seulementActifs = _cboFiltreStatutFournisseur.SelectedIndex == 0;

        var list = string.IsNullOrEmpty(q)
            ? _fournisseurService.ObtenirTous(!seulementActifs ? false : true)
            : _fournisseurService.Rechercher(q);

        if (seulementActifs)
        {
            list = list.Where(f => f.Actif).ToList();
        }

        _gridFournisseurs.DataSource = list.Select(f => new
        {
            f.Id,
            RaisonSociale = f.Nom,
            Téléphone = f.Telephone,
            Email = f.Email,
            Adresse = f.Adresse,
            Statut = f.Actif ? "Actif" : "Inactif"
        }).ToList();
    }

    private Fournisseur? ObtenirFournisseurSelectionne()
    {
        if (_gridFournisseurs.SelectedRows.Count == 0) return null;
        var row = _gridFournisseurs.SelectedRows[0];
        if (row.Cells["Id"].Value is int id)
        {
            return _fournisseurService.ObtenirParId(id);
        }
        return null;
    }

    private void BtnAjouterFournisseur_Click(object? sender, EventArgs e)
    {
        using var form = new FournisseurEditForm(null, _fournisseurService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            FiltrerFournisseurs();
            ChargerFournisseursComboReception();
            MessageBox.Show($"Fournisseur '{form.FournisseurEnregistre?.Nom}' créé avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnModifierFournisseur_Click(object? sender, EventArgs e)
    {
        var f = ObtenirFournisseurSelectionne();
        if (f == null)
        {
            MessageBox.Show("Veuillez sélectionner un fournisseur à modifier.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var form = new FournisseurEditForm(f, _fournisseurService);
        if (form.ShowDialog(this) == DialogResult.OK)
        {
            FiltrerFournisseurs();
            ChargerFournisseursComboReception();
            MessageBox.Show($"Fournisseur '{f.Nom}' mis à jour avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
    }

    private void BtnChangerStatutFournisseur_Click(object? sender, EventArgs e)
    {
        var f = ObtenirFournisseurSelectionne();
        if (f == null)
        {
            MessageBox.Show("Veuillez sélectionner un fournisseur.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        bool nouveauStatut = !f.Actif;
        string action = nouveauStatut ? "activer" : "désactiver";

        if (MessageBox.Show($"Voulez-vous {action} le fournisseur '{f.Nom}' ?", "Confirmation", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            _fournisseurService.ChangerStatut(f.Id, nouveauStatut);
            FiltrerFournisseurs();
            ChargerFournisseursComboReception();
        }
    }

    #endregion

    #region Vue 4 : Approvisionnements & Réceptions de Stock (Transactionnel)

    private Panel CreerVueReceptions()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 15, 25, 15)
        };

        // Panneau supérieur : Saisie du bon de réception et ajout d'articles
        var grpSaisie = new GroupBox
        {
            Text = "📥 Enregistrement d'une Réception Fournisseur (Entrée en Stock)",
            Dock = DockStyle.Top,
            Height = 220,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(15, 10, 15, 10),
            BackColor = Color.White
        };

        // Ligne 1 : Fournisseur & N° Bon de livraison
        var lblF = new Label { Text = "Fournisseur Partenaire :", Location = new Point(15, 28), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cboReceptionFournisseur = new ComboBox { Location = new Point(15, 50), Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };

        var lblBL = new Label { Text = "N° Bon Livraison / Facture :", Location = new Point(340, 28), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtReceptionBL = new TextBox { Location = new Point(340, 50), Width = 220, Font = new Font("Segoe UI", 9.5f) };

        // Ligne 2 : Produit, N° Lot, Péremption, Quantité, Coût
        var lblProd = new Label { Text = "Médicament / Produit * :", Location = new Point(15, 88), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _cboReceptionProduit = new ComboBox { Location = new Point(15, 110), Width = 310, DropDownStyle = ComboBoxStyle.DropDownList, Font = new Font("Segoe UI", 9.5f) };

        var lblLot = new Label { Text = "N° de Lot * :", Location = new Point(340, 88), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _txtReceptionLot = new TextBox { Location = new Point(340, 110), Width = 150, Font = new Font("Segoe UI", 9.5f) };

        var lblPeremption = new Label { Text = "Date Péremption * :", Location = new Point(505, 88), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _dtpReceptionPeremption = new DateTimePicker
        {
            Location = new Point(505, 110),
            Width = 140,
            Format = DateTimePickerFormat.Short,
            Value = DateTime.Today.AddYears(1),
            Font = new Font("Segoe UI", 9.5f)
        };

        var lblQte = new Label { Text = "Quantité Reçue * :", Location = new Point(660, 88), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _numReceptionQte = new NumericUpDown
        {
            Location = new Point(660, 110),
            Width = 100,
            Minimum = 1,
            Maximum = 100000,
            Value = 50,
            Font = new Font("Segoe UI", 9.5f)
        };

        var lblCout = new Label { Text = "Coût Achat Unitaire (€) :", Location = new Point(775, 88), AutoSize = true, Font = new Font("Segoe UI", 9f, FontStyle.Bold) };
        _numReceptionCout = new NumericUpDown
        {
            Location = new Point(775, 110),
            Width = 110,
            DecimalPlaces = 2,
            Increment = 0.50m,
            Minimum = 0.00m,
            Maximum = 99999.99m,
            Value = 2.00m,
            Font = new Font("Segoe UI", 9.5f)
        };

        // Boutons d'ajout au bon ou réception rapide
        var btnAjouterLigne = new Button
        {
            Text = "➕ Ajouter au Bon de Réception",
            Location = new Point(15, 155),
            Width = 270,
            Height = 38,
            BackColor = Color.FromArgb(79, 70, 229),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnAjouterLigne.FlatAppearance.BorderSize = 0;
        btnAjouterLigne.Click += BtnAjouterLigneReception_Click;

        var btnReceptionRapide = new Button
        {
            Text = "⚡ Réceptionner Directement Cet Article",
            Location = new Point(300, 155),
            Width = 310,
            Height = 38,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnReceptionRapide.FlatAppearance.BorderSize = 0;
        btnReceptionRapide.Click += BtnReceptionRapide_Click;

        grpSaisie.Controls.Add(lblF);
        grpSaisie.Controls.Add(_cboReceptionFournisseur);
        grpSaisie.Controls.Add(lblBL);
        grpSaisie.Controls.Add(_txtReceptionBL);
        grpSaisie.Controls.Add(lblProd);
        grpSaisie.Controls.Add(_cboReceptionProduit);
        grpSaisie.Controls.Add(lblLot);
        grpSaisie.Controls.Add(_txtReceptionLot);
        grpSaisie.Controls.Add(lblPeremption);
        grpSaisie.Controls.Add(_dtpReceptionPeremption);
        grpSaisie.Controls.Add(lblQte);
        grpSaisie.Controls.Add(_numReceptionQte);
        grpSaisie.Controls.Add(lblCout);
        grpSaisie.Controls.Add(_numReceptionCout);
        grpSaisie.Controls.Add(btnAjouterLigne);
        grpSaisie.Controls.Add(btnReceptionRapide);

        // Section intermédiaire : Grille du bon de réception multi-lignes
        var pnlBonEnCours = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0)
        };

        var pnlBonBar = new Panel { Dock = DockStyle.Bottom, Height = 55, Padding = new Padding(10) };
        _lblTotalBonReception = new Label
        {
            Text = "Articles dans le bon : 0  |  Montant Total : 0,00 €",
            Dock = DockStyle.Left,
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            AutoSize = true
        };

        var btnValiderBon = new Button
        {
            Text = "✅ Valider et Entrer en Stock (Transactionnel)",
            Dock = DockStyle.Right,
            Width = 350,
            Height = 40,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnValiderBon.FlatAppearance.BorderSize = 0;
        btnValiderBon.Click += BtnValiderBonReception_Click;

        var btnViderBon = new Button
        {
            Text = "Vider le bon",
            Dock = DockStyle.Right,
            Width = 110,
            Height = 40,
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9f),
            Margin = new Padding(0, 0, 10, 0)
        };
        btnViderBon.FlatAppearance.BorderSize = 0;
        btnViderBon.Click += (s, e) =>
        {
            _lignesBonReceptionEnCours.Clear();
            ActualiserGrilleBonReception();
        };

        pnlBonBar.Controls.Add(_lblTotalBonReception);
        pnlBonBar.Controls.Add(btnViderBon);
        pnlBonBar.Controls.Add(btnValiderBon);

        var lblTitreBon = new Label
        {
            Dock = DockStyle.Top,
            Height = 28,
            Text = "📋 Bon de Réception en Cours de Validation",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        _gridBonReceptionLignes = CreerDataGridViewStandard();
        pnlBonEnCours.Controls.Add(_gridBonReceptionLignes);
        pnlBonEnCours.Controls.Add(pnlBonBar);
        pnlBonEnCours.Controls.Add(lblTitreBon);

        panel.Controls.Add(pnlBonEnCours);
        panel.Controls.Add(grpSaisie);

        return panel;
    }

    private void BtnAjouterLigneReception_Click(object? sender, EventArgs e)
    {
        if (_cboReceptionProduit.SelectedItem is not Produit prod)
        {
            MessageBox.Show("Veuillez sélectionner un médicament / produit.", "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string numLot = _txtReceptionLot.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(numLot))
        {
            MessageBox.Show("Le numéro de lot est obligatoire pour la traçabilité.", "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtReceptionLot.Focus();
            return;
        }

        if (_dtpReceptionPeremption.Value.Date <= DateTime.Today)
        {
            MessageBox.Show("La date de péremption du lot doit être ultérieure à aujourd'hui.", "Date Invalide", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int qte = (int)_numReceptionQte.Value;
        decimal cout = _numReceptionCout.Value;

        _lignesBonReceptionEnCours.Add(new LigneReceptionDto
        {
            ProduitId = prod.Id,
            ReferenceProduit = prod.ReferenceUnique,
            DesignationProduit = prod.Designation,
            NumeroLot = numLot,
            DatePeremption = _dtpReceptionPeremption.Value.Date,
            Quantite = qte,
            CoutAchat = cout
        });

        _txtReceptionLot.Clear();
        ActualiserGrilleBonReception();
    }

    private void ActualiserGrilleBonReception()
    {
        _gridBonReceptionLignes.DataSource = null;
        _gridBonReceptionLignes.DataSource = _lignesBonReceptionEnCours.Select(l => new
        {
            Produit = l.DesignationProduit,
            Référence = l.ReferenceProduit,
            Lot = l.NumeroLot,
            Péremption = l.DatePeremption.ToString("dd/MM/yyyy"),
            Quantité = l.Quantite,
            CoûtUnitaire = l.CoutAchat.ToString("C2"),
            SousTotal = l.MontantTotal.ToString("C2")
        }).ToList();

        decimal montantTotal = _lignesBonReceptionEnCours.Sum(x => x.MontantTotal);
        _lblTotalBonReception.Text = $"Articles dans le bon : {_lignesBonReceptionEnCours.Count}  |  Montant Total : {montantTotal:C2}";
    }

    private void BtnValiderBonReception_Click(object? sender, EventArgs e)
    {
        if (_lignesBonReceptionEnCours.Count == 0)
        {
            MessageBox.Show("Le bon de réception est vide. Veuillez d'abord ajouter au moins un article.", "Information", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int? fournisseurId = _cboReceptionFournisseur.SelectedValue as int?;
        string? blRef = _txtReceptionBL.Text.Trim();

        try
        {
            // Validation transactionnelle ACID
            _stockService.ValiderBonDeReception(fournisseurId, blRef, _lignesBonReceptionEnCours);

            int nbArticles = _lignesBonReceptionEnCours.Count;
            _lignesBonReceptionEnCours.Clear();
            ActualiserGrilleBonReception();
            ChargerToutesLesDonnees();

            MessageBox.Show(
                $"Bon de réception validé avec succès !\n" +
                $"• {nbArticles} article(s) réceptionné(s)\n" +
                $"• Stock physique et vendable incrémenté automatiquement\n" +
                $"• Mouvements d'entrée et traçabilité tracés dans SQLite.",
                "Entrée en Stock Validée",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur lors de la validation du bon : " + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private void BtnReceptionRapide_Click(object? sender, EventArgs e)
    {
        if (_cboReceptionProduit.SelectedItem is not Produit prod)
        {
            MessageBox.Show("Veuillez sélectionner un médicament / produit.", "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string numLot = _txtReceptionLot.Text.Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(numLot))
        {
            MessageBox.Show("Le numéro de lot est obligatoire.", "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtReceptionLot.Focus();
            return;
        }

        if (_dtpReceptionPeremption.Value.Date <= DateTime.Today)
        {
            MessageBox.Show("La date de péremption doit être ultérieure à aujourd'hui.", "Date Invalide", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        int qte = (int)_numReceptionQte.Value;
        decimal cout = _numReceptionCout.Value;
        int? fournisseurId = _cboReceptionFournisseur.SelectedValue as int?;

        try
        {
            var lot = _stockService.ReceptionnerLot(prod.Id, fournisseurId, numLot, _dtpReceptionPeremption.Value.Date, qte, cout);
            ChargerToutesLesDonnees();

            MessageBox.Show(
                $"Réception de '{prod.Designation}' (Lot: {lot.NumeroLot}) validée avec succès !\n" +
                $"• Quantité ajoutée au stock : {qte} unité(s)\n" +
                $"• Stock disponible mis à jour immédiatement.",
                "Stock Incrémenté",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            _txtReceptionLot.Clear();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur : " + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region Vue 5 : Lots & Traçabilité FEFO

    private Panel CreerVueLots()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 20, 25, 20)
        };

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 60,
            BackColor = Color.White,
            Padding = new Padding(15, 12, 15, 12)
        };

        var lblTitre = new Label
        {
            Text = "📦 Traçabilité des Lots (Règle FEFO - First Expired, First Out)",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            Location = new Point(15, 16),
            AutoSize = true
        };
        pnlHeader.Controls.Add(lblTitre);

        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0)
        };

        _gridLotsStock = CreerDataGridViewStandard();
        pnlGrid.Controls.Add(_gridLotsStock);

        panel.Controls.Add(pnlGrid);
        panel.Controls.Add(pnlHeader);

        return panel;
    }

    #endregion

    #region Vue 6 : Caisse & Encaissements

    private Panel CreerVueCaisse()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 20, 25, 20)
        };

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 110,
            BackColor = Color.White,
            Padding = new Padding(20, 15, 20, 15)
        };

        var lblTitre = new Label
        {
            Text = "🛒 MODULE DE CAISSE & ENCAISSEMENTS",
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            ForeColor = Color.FromArgb(15, 23, 42),
            Location = new Point(15, 12),
            AutoSize = true
        };

        var lblProd = new Label { Text = "Produit :", Location = new Point(15, 52), AutoSize = true };
        _cboProduitVente = new ComboBox
        {
            Location = new Point(80, 48),
            Width = 280,
            DropDownStyle = ComboBoxStyle.DropDownList
        };

        var lblQte = new Label { Text = "Quantité :", Location = new Point(375, 52), AutoSize = true };
        _numQteVente = new NumericUpDown
        {
            Location = new Point(445, 48),
            Width = 70,
            Minimum = 1,
            Maximum = 100,
            Value = 1
        };

        _lblTotalVenteSimulee = new Label
        {
            Text = "Total : 0,00 €",
            Location = new Point(530, 52),
            AutoSize = true,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 185, 129)
        };

        var btnValiderVente = new Button
        {
            Text = "💳 Valider Vente & Sortie Stock",
            Location = new Point(680, 44),
            Width = 240,
            Height = 36,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnValiderVente.FlatAppearance.BorderSize = 0;
        btnValiderVente.Click += BtnValiderVente_Click;

        pnlHeader.Controls.Add(lblTitre);
        pnlHeader.Controls.Add(lblProd);
        pnlHeader.Controls.Add(_cboProduitVente);
        pnlHeader.Controls.Add(lblQte);
        pnlHeader.Controls.Add(_numQteVente);
        pnlHeader.Controls.Add(_lblTotalVenteSimulee);
        pnlHeader.Controls.Add(btnValiderVente);

        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(0, 15, 0, 0)
        };

        var lblGridTitre = new Label
        {
            Dock = DockStyle.Top,
            Height = 30,
            Text = "Historique des Ventes du Jour",
            Font = new Font("Segoe UI", 10.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59)
        };

        _gridVentesRecentes = CreerDataGridViewStandard();
        pnlGrid.Controls.Add(_gridVentesRecentes);
        pnlGrid.Controls.Add(lblGridTitre);

        panel.Controls.Add(pnlGrid);
        panel.Controls.Add(pnlHeader);

        return panel;
    }

    private void BtnValiderVente_Click(object? sender, EventArgs e)
    {
        try
        {
            if (_cboProduitVente.SelectedItem is not Produit produit)
            {
                MessageBox.Show("Veuillez sélectionner un produit.", "Attention", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            int qte = (int)_numQteVente.Value;
            var lotsFEFO = _stockService.ObtenirLotsFEFO(produit.Id);
            var lot = lotsFEFO.FirstOrDefault(l => l.QuantiteActuelle >= qte);

            if (lot == null)
            {
                MessageBox.Show($"Aucun lot disponible avec une quantité suffisante ({qte} unité(s)) pour '{produit.Designation}'.", "Stock Insuffisant", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            var panier = new List<LignePanierDto>
            {
                new LignePanierDto
                {
                    ProduitId = produit.Id,
                    DesignationProduit = produit.Designation,
                    LotId = lot.Id,
                    NumeroLot = lot.NumeroLot,
                    Quantite = qte,
                    PrixUnitaire = produit.PrixVente
                }
            };

            decimal total = panier.Sum(x => x.SousTotal);
            var vente = _venteService.ValiderVente(panier, ModePaiement.Especes, total);

            MessageBox.Show(
                $"Vente #{vente.NumeroFacture} enregistrée avec succès !\n" +
                $"• Article : {produit.Designation} (x{qte})\n" +
                $"• Montant payé : {vente.MontantPaye:C2}\n" +
                $"• Décrémentation automatique du lot {lot.NumeroLot} (FEFO)",
                "Vente Validée",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information
            );

            ChargerToutesLesDonnees();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur lors de la vente : " + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    #endregion

    #region Vue 7 : Journal d'Audit (Admin Uniquement)

    private Panel CreerVueAudit()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 20, 25, 20)
        };

        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 55,
            BackColor = Color.White,
            Padding = new Padding(15)
        };

        var lblTitre = new Label
        {
            Text = "🛡️ Piste d'Audit & Sécurité (Accès Réservé à l'Administration)",
            Font = new Font("Segoe UI", 11f, FontStyle.Bold),
            ForeColor = Color.FromArgb(88, 28, 135),
            Location = new Point(15, 14),
            AutoSize = true
        };
        pnlHeader.Controls.Add(lblTitre);

        var pnlGrid = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = Color.White,
            Padding = new Padding(15),
            Margin = new Padding(0, 10, 0, 0)
        };

        _gridJournalAudit = CreerDataGridViewStandard();
        pnlGrid.Controls.Add(_gridJournalAudit);

        panel.Controls.Add(pnlGrid);
        panel.Controls.Add(pnlHeader);

        return panel;
    }

    #endregion

    #region Vue 8 : Configuration & Paramètres (Admin Uniquement)

    private Panel CreerVueParametres()
    {
        var panel = new Panel
        {
            Dock = DockStyle.Fill,
            AutoScroll = true,
            Padding = new Padding(25, 20, 25, 20)
        };

        var grpInfo = new GroupBox
        {
            Text = "⚙️ Paramètres du Système & Base de Données SQLite",
            Dock = DockStyle.Top,
            Height = 220,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            ForeColor = Color.FromArgb(30, 41, 59),
            Padding = new Padding(20)
        };

        var lblDbPath = new Label
        {
            Text = $"Emplacement du fichier SQLite :\n{DatabaseConnection.Instance.DatabaseFilePath}",
            Font = new Font("Segoe UI", 9f),
            Location = new Point(20, 35),
            Size = new Size(800, 40)
        };

        var btnSeedDemo = new Button
        {
            Text = "🌱 Insérer un Jeu de Données Pharmaceutiques de Test (DML)",
            Location = new Point(20, 85),
            Width = 420,
            Height = 40,
            BackColor = Color.FromArgb(16, 185, 129),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        btnSeedDemo.FlatAppearance.BorderSize = 0;
        btnSeedDemo.Click += (s, e) => InsererJeuDeDonneesTest();

        grpInfo.Controls.Add(lblDbPath);
        grpInfo.Controls.Add(btnSeedDemo);

        panel.Controls.Add(grpInfo);
        return panel;
    }

    #endregion

    #region Chargement & Synchronisation Globale

    private void ChargerToutesLesDonnees()
    {
        RafraichirKpisEtAlertes();
        ChargerCategoriesComboProduit();
        ChargerCatalogue();
        ChargerFournisseurs();
        ChargerFournisseursComboReception();
        ChargerProduitsComboReception();
        ChargerLotsEtTraçabilite();
        ChargerHistoriqueVentes();

        if (SessionUtilisateur.EstAdmin)
        {
            ChargerAudit();
        }
    }

    private void RafraichirKpisEtAlertes()
    {
        decimal caJour = _venteService.ObtenirChiffreAffairesAujourdhui();
        _lblKpiCa.Text = caJour.ToString("C2");

        var syntheseStocks = _produitService.ObtenirSyntheseStocks();
        int nbRuptures = syntheseStocks.Count(p => p.EstEnRupture);
        int nbFaibles = syntheseStocks.Count(p => p.EstStockFaible);

        _lblKpiRuptures.Text = nbRuptures.ToString();
        _lblKpiStocksFaibles.Text = nbFaibles.ToString();

        var alertesStock = syntheseStocks.Where(p => p.EstEnRupture || p.EstStockFaible).Select(s => new
        {
            Référence = s.ReferenceUnique,
            Désignation = s.Designation,
            Catégorie = s.Categorie,
            Prix = s.PrixVente.ToString("C2"),
            SeuilMin = s.SeuilStockMin,
            StockDisponible = s.StockDisponible,
            Alerte = s.StatutStock
        }).ToList();
        _gridAlertesStock.DataSource = alertesStock;

        var lotsPeremption = _stockService.ObtenirLotsProchesPeremption(_delaiPeremptionJours);
        _lblKpiPeremptions.Text = lotsPeremption.Count.ToString();

        _gridAlertesPeremption.DataSource = lotsPeremption.Select(l => new
        {
            Produit = l.DesignationProduit,
            Lot = l.NumeroLot,
            Péremption = l.DatePeremption.ToString("dd/MM/yyyy"),
            JoursRestants = l.JoursRestantsAvantPeremption,
            Stock = l.QuantiteActuelle,
            CoutAchat = l.CoutAchat.ToString("C2"),
            Statut = l.Statut.ToString()
        }).ToList();

        var colAlert = _gridAlertesPeremption.Columns["CoutAchat"];
        if (!SessionUtilisateur.EstAdmin && colAlert != null)
        {
            colAlert.Visible = false;
        }

        var produitsDispos = _produitService.ObtenirTousLesProduits();
        _cboProduitVente.DataSource = produitsDispos;
        _cboProduitVente.DisplayMember = "Designation";
        _cboProduitVente.ValueMember = "Id";
    }

    private void ChargerCategoriesComboProduit()
    {
        _cboFiltreCategorieProduit.Items.Clear();
        _cboFiltreCategorieProduit.Items.Add("Toutes les catégories");
        foreach (var c in _produitService.ObtenirCategories())
        {
            _cboFiltreCategorieProduit.Items.Add(c);
        }
        _cboFiltreCategorieProduit.SelectedIndex = 0;
    }

    private void ChargerCatalogue()
    {
        FiltrerCatalogueProduits();
    }

    private void ChargerFournisseurs()
    {
        FiltrerFournisseurs();
    }

    private void ChargerFournisseursComboReception()
    {
        var fournisseurs = _fournisseurService.ObtenirTous(true);
        _cboReceptionFournisseur.DataSource = fournisseurs;
        _cboReceptionFournisseur.DisplayMember = "Nom";
        _cboReceptionFournisseur.ValueMember = "Id";
    }

    private void ChargerProduitsComboReception()
    {
        var produits = _produitService.ObtenirTousLesProduits(true);
        _cboReceptionProduit.DataSource = produits;
        _cboReceptionProduit.DisplayMember = "Designation";
        _cboReceptionProduit.ValueMember = "Id";
    }

    private void ChargerLotsEtTraçabilite()
    {
        var lots = _stockService.ObtenirTousLesLots();
        _gridLotsStock.DataSource = lots.Select(l => new
        {
            l.Id,
            Produit = l.DesignationProduit,
            Lot = l.NumeroLot,
            Péremption = l.DatePeremption.ToString("dd/MM/yyyy"),
            JoursRestants = l.JoursRestantsAvantPeremption,
            Initial = l.QuantiteInitiale,
            Actuel = l.QuantiteActuelle,
            CoutAchat = l.CoutAchat.ToString("C2"),
            Fournisseur = l.NomFournisseur,
            Statut = l.Statut.ToString()
        }).ToList();

        var colLots = _gridLotsStock.Columns["CoutAchat"];
        if (!SessionUtilisateur.EstAdmin && colLots != null)
        {
            colLots.Visible = false;
        }
    }

    private void ChargerHistoriqueVentes()
    {
        var ventes = _venteService.ObtenirVentesRecentes(50);
        _gridVentesRecentes.DataSource = ventes.Select(v => new
        {
            Facture = v.NumeroFacture,
            Date = v.DateVente.ToString("dd/MM/yyyy HH:mm"),
            Total = v.MontantTotal.ToString("C2"),
            Payé = v.MontantPaye.ToString("C2"),
            Rendu = v.RenduMonnaie.ToString("C2"),
            Vendeur = v.NomVendeur,
            Statut = v.Statut.ToString()
        }).ToList();
    }

    private void ChargerAudit()
    {
        if (SessionUtilisateur.EstAdmin)
        {
            var audits = _auditService.ObtenirDernieresActions(100);
            _gridJournalAudit.DataSource = audits.Select(a => new
            {
                a.Id,
                Date = a.DateAction.ToString("dd/MM/yyyy HH:mm:ss"),
                Utilisateur = a.NomUtilisateur,
                Action = a.Action,
                Table = a.TableConcernee,
                Détails = a.Details
            }).ToList();
        }
    }

    private void InsererJeuDeDonneesTest()
    {
        try
        {
            var f1 = _fournisseurService.EnregistrerFournisseur(new Fournisseur
            {
                Nom = "Laboratoires Sanofi France",
                Telephone = "+33 1 53 77 40 00",
                Email = "contact@sanofi.com",
                Adresse = "54 Rue La Boétie, 75008 Paris"
            });

            var f2 = _fournisseurService.EnregistrerFournisseur(new Fournisseur
            {
                Nom = "Biogaran Génériques",
                Telephone = "+33 1 55 72 45 00",
                Email = "info@biogaran.fr",
                Adresse = "15 Boulevard Charles de Gaulle, 92700 Colombes"
            });

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
                Designation = "Amoxicilline 500mg",
                Categorie = "Antibiotique",
                Dosage = "500mg",
                Forme = "Gélule",
                UniteVente = "Boîte de 12",
                PrixVente = 4.80m,
                SeuilStockMin = 8
            });

            var p3 = _produitService.EnregistrerProduit(new Produit
            {
                ReferenceUnique = "MED-IBU-400",
                Designation = "Ibuprofène 400mg",
                Categorie = "Anti-inflammatoire",
                Dosage = "400mg",
                Forme = "Comprimé",
                UniteVente = "Boîte de 10",
                PrixVente = 3.65m,
                SeuilStockMin = 5
            });

            var pRupture = _produitService.EnregistrerProduit(new Produit
            {
                ReferenceUnique = "MED-AZI-250",
                Designation = "Azithromycine 250mg",
                Categorie = "Antibiotique",
                Dosage = "250mg",
                Forme = "Comprimé",
                UniteVente = "Boîte de 6",
                PrixVente = 7.20m,
                SeuilStockMin = 5
            });

            _stockService.ReceptionnerLot(p1.Id, f1.Id, "LOT-DOL-NORMAL", DateTime.Today.AddMonths(18), 60, 1.20m);
            _stockService.ReceptionnerLot(p2.Id, f2.Id, "LOT-AMX-45J", DateTime.Today.AddDays(45), 12, 2.50m);
            _stockService.ReceptionnerLot(p3.Id, f1.Id, "LOT-IBU-75J", DateTime.Today.AddDays(75), 4, 1.90m);

            ChargerToutesLesDonnees();
            MessageBox.Show("Données de test insérées avec succès !", "Succès", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show("Erreur : " + ex.Message, "Erreur", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static DataGridView CreerDataGridViewStandard()
    {
        var grid = new DataGridView
        {
            Dock = DockStyle.Fill,
            ReadOnly = true,
            AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
            SelectionMode = DataGridViewSelectionMode.FullRowSelect,
            BackgroundColor = Color.White,
            BorderStyle = BorderStyle.None,
            RowHeadersVisible = false,
            EnableHeadersVisualStyles = false,
            GridColor = Color.FromArgb(241, 245, 249)
        };
        grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(241, 245, 249);
        grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(51, 65, 85);
        grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9f, FontStyle.Bold);
        grid.ColumnHeadersHeight = 32;
        grid.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(248, 250, 252);
        return grid;
    }

    #endregion
}
