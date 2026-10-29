using System.Drawing;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Services;
using GestionPharmacie.DAL.Entities;

namespace GestionPharmacie.UI.Forms;

/// <summary>
/// Formulaire de création et modification d'une fiche signalétique fournisseur.
/// </summary>
public class FournisseurEditForm : Form
{
    private readonly FournisseurService _fournisseurService;
    private readonly Fournisseur? _fournisseurOriginal;

    private TextBox _txtNom = null!;
    private TextBox _txtTelephone = null!;
    private TextBox _txtEmail = null!;
    private TextBox _txtAdresse = null!;
    private CheckBox _chkActif = null!;
    private Label _lblErreur = null!;
    private Button _btnEnregistrer = null!;
    private Button _btnAnnuler = null!;

    public Fournisseur? FournisseurEnregistre { get; private set; }

    public FournisseurEditForm(Fournisseur? fournisseur = null, FournisseurService? fournisseurService = null)
    {
        _fournisseurOriginal = fournisseur;
        _fournisseurService = fournisseurService ?? new FournisseurService();
        InitializeComponent();
        RemplirChamps();
    }

    private void InitializeComponent()
    {
        Text = _fournisseurOriginal == null ? "Nouveau Fournisseur Pharmaceutique" : $"Fiche Fournisseur - {_fournisseurOriginal.Nom}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(500, 480);
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
            Text = _fournisseurOriginal == null ? "🏢 NOUVEAU FOURNISSEUR" : "✏️ MODIFICATION FOURNISSEUR",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 12f, FontStyle.Bold),
            Location = new Point(16, 12),
            AutoSize = true
        };

        var lblSousTitre = new Label
        {
            Text = "Fiche signalétique et coordonnées de contact",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5f),
            Location = new Point(18, 38),
            AutoSize = true
        };

        pnlHeader.Controls.Add(lblTitre);
        pnlHeader.Controls.Add(lblSousTitre);

        // Champs
        int startX = 35;
        int fieldWidth = 430;

        var lblNom = CreerLabel("Raison Sociale / Nom du Fournisseur * :", startX, 85);
        _txtNom = new TextBox { Location = new Point(startX, 110), Width = fieldWidth };

        var lblTel = CreerLabel("Numéro de Téléphone :", startX, 150);
        _txtTelephone = new TextBox { Location = new Point(startX, 175), Width = fieldWidth };

        var lblEmail = CreerLabel("Adresse Email :", startX, 215);
        _txtEmail = new TextBox { Location = new Point(startX, 240), Width = fieldWidth };

        var lblAdr = CreerLabel("Adresse Postale & Ville :", startX, 280);
        _txtAdresse = new TextBox { Location = new Point(startX, 305), Width = fieldWidth };

        _chkActif = new CheckBox
        {
            Text = "Fournisseur Actif (Partenaire référencé)",
            Location = new Point(startX, 345),
            Width = fieldWidth,
            Checked = true,
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold),
            ForeColor = Color.FromArgb(16, 185, 129)
        };

        _lblErreur = new Label
        {
            Location = new Point(startX, 375),
            Width = fieldWidth,
            Height = 35,
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 9f, FontStyle.Bold),
            Text = string.Empty
        };

        _btnEnregistrer = new Button
        {
            Text = "💾 Enregistrer la Fiche",
            Location = new Point(startX, 415),
            Width = 270,
            Height = 40,
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
            Location = new Point(315, 415),
            Width = 150,
            Height = 40,
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        _btnAnnuler.FlatAppearance.BorderSize = 0;
        _btnAnnuler.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(pnlHeader);
        Controls.Add(lblNom);
        Controls.Add(_txtNom);
        Controls.Add(lblTel);
        Controls.Add(_txtTelephone);
        Controls.Add(lblEmail);
        Controls.Add(_txtEmail);
        Controls.Add(lblAdr);
        Controls.Add(_txtAdresse);
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

    private void RemplirChamps()
    {
        if (_fournisseurOriginal != null)
        {
            _txtNom.Text = _fournisseurOriginal.Nom;
            _txtTelephone.Text = _fournisseurOriginal.Telephone;
            _txtEmail.Text = _fournisseurOriginal.Email;
            _txtAdresse.Text = _fournisseurOriginal.Adresse;
            _chkActif.Checked = _fournisseurOriginal.Actif;
        }
    }

    private void BtnEnregistrer_Click(object? sender, EventArgs e)
    {
        _lblErreur.Text = string.Empty;

        string nom = _txtNom.Text.Trim();
        string email = _txtEmail.Text.Trim();

        if (string.IsNullOrWhiteSpace(nom))
        {
            _lblErreur.Text = "⚠️ La raison sociale ou nom du fournisseur est obligatoire.";
            _txtNom.Focus();
            return;
        }

        if (!string.IsNullOrWhiteSpace(email) && !Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
        {
            _lblErreur.Text = "⚠️ L'adresse email saisie n'a pas un format valide.";
            _txtEmail.Focus();
            return;
        }

        try
        {
            var fournisseur = _fournisseurOriginal ?? new Fournisseur();
            fournisseur.Nom = nom;
            fournisseur.Telephone = _txtTelephone.Text.Trim();
            fournisseur.Email = email;
            fournisseur.Adresse = _txtAdresse.Text.Trim();
            fournisseur.Actif = _chkActif.Checked;

            FournisseurEnregistre = _fournisseurService.EnregistrerFournisseur(fournisseur);

            DialogResult = DialogResult.OK;
            Close();
        }
        catch (MetierException ex)
        {
            _lblErreur.Text = "⚠️ " + ex.Message;
        }
        catch (Exception ex)
        {
            _lblErreur.Text = "Erreur système : " + ex.Message;
        }
    }
}
