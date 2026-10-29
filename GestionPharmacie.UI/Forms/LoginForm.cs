using System.Drawing;
using System.Windows.Forms;
using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Services;
using GestionPharmacie.DAL.Entities;

namespace GestionPharmacie.UI.Forms;

/// <summary>
/// Écran d'authentification sécurisé avec hachage PBKDF2/SHA-256 et redirection dynamique par rôle.
/// </summary>
public class LoginForm : Form
{
    private readonly AuthentificationService _authService;
    private TextBox _txtIdentifiant = null!;
    private TextBox _txtMotDePasse = null!;
    private CheckBox _chkAfficherMdp = null!;
    private Label _lblErreur = null!;
    private Button _btnConnexion = null!;
    private Button _btnQuitter = null!;

    /// <summary>
    /// Utilisateur authentifié avec succès.
    /// </summary>
    public Utilisateur? UtilisateurConnecte { get; private set; }

    public LoginForm()
    {
        _authService = new AuthentificationService();
        InitializeComponent();
    }

    private void InitializeComponent()
    {
        Text = "Connexion Sécurisée - Gestion de Pharmacie";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(480, 430);
        BackColor = Color.FromArgb(248, 250, 252);
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Header Panel
        var pnlHeader = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(15, 23, 42) // Slate 900
        };

        var lblTitre = new Label
        {
            Text = "⚕️ PHARMA-GEST",
            ForeColor = Color.White,
            Font = new Font("Segoe UI", 15f, FontStyle.Bold),
            AutoSize = true,
            Location = new Point(25, 18)
        };

        var lblSousTitre = new Label
        {
            Text = "Authentification sécurisée (SHA-256 / PBKDF2 avec sel)",
            ForeColor = Color.FromArgb(148, 163, 184),
            Font = new Font("Segoe UI", 8.5f),
            AutoSize = true,
            Location = new Point(27, 48)
        };

        pnlHeader.Controls.Add(lblTitre);
        pnlHeader.Controls.Add(lblSousTitre);

        // Form controls
        var lblId = new Label
        {
            Text = "Identifiant utilisateur :",
            Location = new Point(40, 105),
            AutoSize = true,
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _txtIdentifiant = new TextBox
        {
            Location = new Point(40, 130),
            Width = 400,
            Height = 28,
            Text = "admin"
        };

        var lblMdp = new Label
        {
            Text = "Mot de passe :",
            Location = new Point(40, 170),
            AutoSize = true,
            ForeColor = Color.FromArgb(51, 65, 85),
            Font = new Font("Segoe UI", 9.5f, FontStyle.Bold)
        };

        _txtMotDePasse = new TextBox
        {
            Location = new Point(40, 195),
            Width = 400,
            Height = 28,
            PasswordChar = '•',
            Text = "admin"
        };

        _chkAfficherMdp = new CheckBox
        {
            Text = "Afficher le mot de passe",
            Location = new Point(40, 228),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            ForeColor = Color.FromArgb(100, 116, 139)
        };
        _chkAfficherMdp.CheckedChanged += (s, e) =>
        {
            _txtMotDePasse.PasswordChar = _chkAfficherMdp.Checked ? '\0' : '•';
        };

        // Raccourcis de test de rôles
        var pnlQuickRoles = new Panel
        {
            Location = new Point(40, 255),
            Width = 400,
            Height = 42,
            BackColor = Color.FromArgb(241, 245, 249)
        };

        var lblQuick = new Label
        {
            Text = "Test rapide :",
            Location = new Point(8, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 8f, FontStyle.Bold),
            ForeColor = Color.FromArgb(71, 85, 105)
        };

        var lnkAdmin = new LinkLabel
        {
            Text = "👑 Profil Admin",
            Location = new Point(90, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            LinkColor = Color.FromArgb(79, 70, 229)
        };
        lnkAdmin.Click += (s, e) =>
        {
            _txtIdentifiant.Text = "admin";
            _txtMotDePasse.Text = "admin";
        };

        var lnkVendeur = new LinkLabel
        {
            Text = "🛒 Profil Vendeur (Accès Restreint)",
            Location = new Point(190, 12),
            AutoSize = true,
            Font = new Font("Segoe UI", 8.5f),
            LinkColor = Color.FromArgb(16, 185, 129)
        };
        lnkVendeur.Click += (s, e) =>
        {
            _txtIdentifiant.Text = "vendeur";
            _txtMotDePasse.Text = "vendeur";
        };

        pnlQuickRoles.Controls.Add(lblQuick);
        pnlQuickRoles.Controls.Add(lnkAdmin);
        pnlQuickRoles.Controls.Add(lnkVendeur);

        _lblErreur = new Label
        {
            Location = new Point(40, 305),
            Width = 400,
            Height = 40,
            ForeColor = Color.FromArgb(220, 38, 38),
            Font = new Font("Segoe UI", 8.5f, FontStyle.Bold),
            Text = string.Empty
        };

        _btnConnexion = new Button
        {
            Text = "Se connecter",
            Location = new Point(40, 355),
            Width = 260,
            Height = 40,
            BackColor = Color.FromArgb(37, 99, 235), // Blue 600
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 10f, FontStyle.Bold),
            Cursor = Cursors.Hand
        };
        _btnConnexion.FlatAppearance.BorderSize = 0;
        _btnConnexion.Click += BtnConnexion_Click;

        _btnQuitter = new Button
        {
            Text = "Quitter",
            Location = new Point(315, 355),
            Width = 125,
            Height = 40,
            BackColor = Color.FromArgb(226, 232, 240),
            ForeColor = Color.FromArgb(71, 85, 105),
            FlatStyle = FlatStyle.Flat,
            Font = new Font("Segoe UI", 9.5f),
            Cursor = Cursors.Hand
        };
        _btnQuitter.FlatAppearance.BorderSize = 0;
        _btnQuitter.Click += (s, e) => { DialogResult = DialogResult.Cancel; Close(); };

        Controls.Add(pnlHeader);
        Controls.Add(lblId);
        Controls.Add(_txtIdentifiant);
        Controls.Add(lblMdp);
        Controls.Add(_txtMotDePasse);
        Controls.Add(_chkAfficherMdp);
        Controls.Add(pnlQuickRoles);
        Controls.Add(_lblErreur);
        Controls.Add(_btnConnexion);
        Controls.Add(_btnQuitter);

        AcceptButton = _btnConnexion;
        CancelButton = _btnQuitter;
    }

    private void BtnConnexion_Click(object? sender, EventArgs e)
    {
        _lblErreur.Text = string.Empty;
        try
        {
            // Vérification de l'identifiant et hachage sécurisé du mot de passe
            var user = _authService.Connecter(_txtIdentifiant.Text.Trim(), _txtMotDePasse.Text);
            UtilisateurConnecte = user;

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
