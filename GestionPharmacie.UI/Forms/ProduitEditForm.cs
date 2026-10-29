using System.Drawing;
using System.Windows.Forms;
using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Services;
using GestionPharmacie.DAL.Entities;

namespace GestionPharmacie.UI.Forms;

/// <summary>
/// Formulaire de création et modification d'un produit avec validation stricte des champs et gestion des doublons.
/// </summary>
public class ProduitEditForm : Form
{
    private readonly ProduitService _produitService;
    private readonly Produit? _produitOriginal;

    private TextBox _txtReference = null!;
    private TextBox _txtDesignation = null!;
    private ComboBox _cboCategorie = null!;
    private TextBox _txtDosage = null!;
    private ComboBox _cboForme = null!;
    private ComboBox _cboUniteVente = null!;
    private NumericUpDown _numPrixVente = null!;
    private NumericUpDown _numSeuilStock = null!;
    private CheckBox _chkActif = null!;
    private Label _lblErreur = null!;
    private Button _btnEnregistrer = null!;
    private Button _btnAnnuler = null!;

    public Produit? ProduitEnregistre { get; private set; }

    public ProduitEditForm(Produit? produit = null, ProduitService? produitService = null)
    {
        _produitOriginal = produit;
        _produitService = produitService ?? new ProduitService();
        InitializeComponent();
        ChargerCategories();
        RemplirChamps();
    }

    private void InitializeComponent()
    {
        Text = _produitOriginal == null ? "Nouveau Produit Pharmaceutique" : $"Modification du Produit - {_produitOriginal.Designation}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(540, 560);
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Header
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 70,
            BackColor = Color.FromArgb(15, 23, 42),
            Padding = new Padding(20, 12, 20, 12)
        };

        var lblTitre = new Label
        {
            Text = _produitOriginal == null ? "➕ NOUVEAU PRODUIT" : "✏️ MODIFICATION PRODUIT",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Location = new Point(16, 12),
            AutoSize = true
        };

        var lblSousTitre = new Label
        {
            Text = "Fiche signalétique et paramètres de dispensation",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(18, 38),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblTitre);
        pnlHeader.Controls.Add(lblSousTitre);

        // Form controls
        int leftCol = 35;
        int rightCol = 275;
        int fieldWidth = 225;

        // Ligne 1 : Référence unique & Catégorie
        var lblRef = CreerLabel("Référence Unique * (ex: MED-DOL-1000) :", leftCol, 85);
        _txtReference = new TextBox { Location = new Point(leftCol, 110), Width = fieldWidth };

        var lblCat = CreerLabel("Catégorie * :", rightCol, 85);
        _cboCategorie = new ComboBox { Location = new Point(rightCol, 110), Width = fieldWidth, DropDownStyle = ComboBoxStyle.DropDown };

        // Ligne 2 : Désignation (pleine largeur)
        var lblDesig = CreerLabel("Désignation / Nom Commercial * :", leftCol, 150);
        _txtDesignation = new TextBox { Location = new Point(leftCol, 175), Width = 465 };

        // Ligne 3 : Dosage & Forme
        var lblDosage = CreerLabel("Dosage (ex: 500mg, 1g) :", leftCol, 215);
        _txtDosage = new TextBox { Location = new Point(leftCol, 240), Width = fieldWidth };

        var lblForme = CreerLabel("Forme Galénique :", rightCol, 215);
        _cboForme = new ComboBox { Location = new Point(rightCol, 240), Width = fieldWidth, DropDownStyle = ComboBoxStyle.DropDown };
        _cboForme.Items.AddRange(new object[] { "Comprimé", "Gélule", "Sirop", "Pommade / Crème", "Injection", "Sachet", "Collyre", "Suppositoire", "Solution buvable", "Dispositif" });
        _cboForme.SelectedIndex = 0;

        // Ligne 4 : Unité de vente & Prix de vente (decimal)
        var lblUnite = CreerLabel("Unité de Vente * :", leftCol, 280);
        _cboUniteVente = new ComboBox { Location = new Point(leftCol, 305), Width = fieldWidth, DropDownStyle = ComboBoxStyle.DropDown };
        _cboUniteVente.Items.AddRange(new object[] { "Boîte", "Flacon", "Plaquette", "Tube", "Ampoule", "Sachet", "Unité" });
        _cboUniteVente.SelectedIndex = 0;

        var lblPrix = CreerLabel("Prix de Vente Public (€) * :", rightCol, 280);
        _numPrixVente = new NumericUpDown
        {
            Location = new Point(rightCol, 305),
            Width = fieldWidth,
            DecimalPlaces = 2,
            Increment = 0.50m,
            Minimum = 0.01m,
            Maximum = 99999.99m,
            Value = 5.00m
        };

        // Ligne 5 : Seuil stock min & Actif
        var lblSeuil = CreerLabel("Seuil de Stock Minimum d'Alerte :", leftCol, 345);
        _numSeuilStock = new NumericUpDown
        {
            Location = new Point(leftCol, 370),
            Width = fieldWidth,
            Minimum = 0,
            Maximum = 10000,
            Value = 5
        };

        _chkActif = new CheckBox
        {
            Text = "Produit Actif (disponible à la vente)",
            Location = new Point(rightCol, 370),
            Width = fieldWidth,
            Checked = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 185, 129)
        };

        // Erreur feedback
        _lblErreur = new Label
        {
            Location = new Point(leftCol, 415),
            Width = 465,
            Height = 45,
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Text = string.Empty
        };

        // Boutons
        _btnEnregistrer = new Button
        {
            Text = "💾 Enregistrer le Produit",
            Location = new Point(leftCol, 470),
            Width = 300,
            Height = 42,
            BackColor = Color.FromArgb(24, 119, 242),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnEnregistrer.FlatAppearance.BorderSize = 0;
        _btnEnregistrer.Click += BtnEnregistrer_Click;

        _btnAnnuler = new Button
        {
            Text = "Annuler",
            Location = new Point(345, 470),
            Width = 155,
            Height = 42,
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        _btnAnnuler.FlatAppearance.BorderSize = 0;
        _btnAnnuler.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(pnlHeader);
        Controls.Add(lblRef);
        Controls.Add(_txtReference);
        Controls.Add(lblCat);
        Controls.Add(_cboCategorie);
        Controls.Add(lblDesig);
        Controls.Add(_txtDesignation);
        Controls.Add(lblDosage);
        Controls.Add(_txtDosage);
        Controls.Add(lblForme);
        Controls.Add(_cboForme);
        Controls.Add(lblUnite);
        Controls.Add(_cboUniteVente);
        Controls.Add(lblPrix);
        Controls.Add(_numPrixVente);
        Controls.Add(lblSeuil);
        Controls.Add(_numSeuilStock);
        Controls.Add(_chkActif);
        Controls.Add(_lblErreur);
        Controls.Add(_btnEnregistrer);
        Controls.Add(_btnAnnuler);

        AcceptButton = _btnEnregistrer;
        CancelButton = _btnAnnuler;
    }

    private static Label CreerLabel(string texte, int x, int y)
    {
        return new Label
        {
            Text = texte,
            Location = new Point(x, y),
            AutoSize = true,
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            ForeColor = Color.FromArgb(51, 65, 85)
        };
    }

    private void ChargerCategories()
    {
        _cboCategorie.Items.Clear();
        var categoriesDb = _produitService.ObtenirCategories();
        foreach (var c in categoriesDb)
        {
            _cboCategorie.Items.Add(c);
        }

        // Catégories par défaut si la base est encore jeune
        string[] defauts = { "Antalgique / Antipyrétique", "Antibiotique", "Anti-inflammatoire", "Cardiologie", "Dermatologie", "Gastro-entérologie", "Vitamines & Compléments", "Dispositifs Médicaux" };
        foreach (var d in defauts)
        {
            if (!_cboCategorie.Items.Contains(d))
            {
                _cboCategorie.Items.Add(d);
            }
        }

        if (_cboCategorie.Items.Count > 0)
        {
            _cboCategorie.SelectedIndex = 0;
        }
    }

    private void RemplirChamps()
    {
        if (_produitOriginal != null)
        {
            _txtReference.Text = _produitOriginal.ReferenceUnique;
            _txtDesignation.Text = _produitOriginal.Designation;
            _cboCategorie.Text = _produitOriginal.Categorie;
            _txtDosage.Text = _produitOriginal.Dosage;
            _cboForme.Text = _produitOriginal.Forme;
            _cboUniteVente.Text = _produitOriginal.UniteVente;
            _numPrixVente.Value = _produitOriginal.PrixVente > 0 ? _produitOriginal.PrixVente : 0.01m;
            _numSeuilStock.Value = _produitOriginal.SeuilStockMin;
            _chkActif.Checked = _produitOriginal.Actif;
        }
    }

    private void BtnEnregistrer_Click(object? sender, EventArgs e)
    {
        _lblErreur.Text = string.Empty;

        // Validation stricte des champs obligatoires
        string reference = _txtReference.Text.Trim().ToUpperInvariant();
        string designation = _txtDesignation.Text.Trim();
        string categorie = _cboCategorie.Text.Trim();
        string unite = _cboUniteVente.Text.Trim();

        if (string.IsNullOrWhiteSpace(reference))
        {
            _lblErreur.Text = "⚠️ La référence unique du produit est obligatoire.";
            _txtReference.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(designation))
        {
            _lblErreur.Text = "⚠️ La désignation du produit est obligatoire.";
            _txtDesignation.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(categorie))
        {
            _lblErreur.Text = "⚠️ La catégorie du produit est obligatoire.";
            _cboCategorie.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(unite))
        {
            _lblErreur.Text = "⚠️ L'unité de vente est obligatoire.";
            _cboUniteVente.Focus();
            return;
        }

        if (_numPrixVente.Value <= 0m)
        {
            _lblErreur.Text = "⚠️ Le prix de vente doit être strictement supérieur à zéro.";
            _numPrixVente.Focus();
            return;
        }

        try
        {
            var produit = _produitOriginal ?? new Produit();
            produit.ReferenceUnique = reference;
            produit.Designation = designation;
            produit.Categorie = categorie;
            produit.Dosage = _txtDosage.Text.Trim();
            produit.Forme = _cboForme.Text.Trim();
            produit.UniteVente = unite;
            produit.PrixVente = _numPrixVente.Value;
            produit.SeuilStockMin = (int)_numSeuilStock.Value;
            produit.Actif = _chkActif.Checked;

            ProduitEnregistre = _produitService.EnregistrerProduit(produit);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (MetierException ex)
        {
            // Affiche notamment les erreurs de doublons de référence !
            _lblErreur.Text = "⚠️ " + ex.Message;
        }
        catch (Exception ex)
        {
            _lblErreur.Text = "Erreur système : " + ex.Message;
        }
    }
}
