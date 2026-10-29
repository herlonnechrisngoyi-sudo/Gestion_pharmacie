using System.Windows.Forms;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.DAL;
using GestionPharmacie.UI.Forms;

namespace GestionPharmacie.UI;

static class Program
{
    /// <summary>
    /// Point d'entrée principal de l'application Windows Forms.
    /// Initialise la base SQLite (DDL/DML, admin & vendeur par défaut) et orchestre la boucle de session.
    /// </summary>
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();

        try
        {
            // Initialisation centralisée de la base SQLite (création des tables DDL et comptes admin & vendeur DML)
            DatabaseInitializer.InitializeDatabase();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"Erreur critique lors de l'initialisation de la base SQLite :\n{ex.Message}",
                "Erreur d'Initialisation",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error
            );
            return;
        }

        // Boucle d'authentification et de session
        while (true)
        {
            using var loginForm = new LoginForm();
            if (loginForm.ShowDialog() != DialogResult.OK)
            {
                break; // L'utilisateur quitte l'application
            }

            // Redirection dynamique vers le Tableau de bord selon le profil connecté
            using var dashboard = new DashboardForm();
            Application.Run(dashboard);

            // Si l'utilisateur s'est déconnecté, la boucle réaffiche l'écran de connexion
            if (!SessionUtilisateur.EstConnecte)
            {
                continue;
            }

            break;
        }
    }
}