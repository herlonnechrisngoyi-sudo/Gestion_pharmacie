using GestionPharmacie.DAL.Entities;

namespace GestionPharmacie.BLL.Security;

/// <summary>
/// Gestion de la session utilisateur courante dans l'application.
/// </summary>
public static class SessionUtilisateur
{
    public static Utilisateur? UtilisateurCourant { get; private set; }

    public static bool EstConnecte => UtilisateurCourant != null;
    public static bool EstAdmin => UtilisateurCourant?.Role == RoleUtilisateur.Admin;
    public static string NomUtilisateur => UtilisateurCourant?.Nom ?? "Anonyme";
    public static int? UtilisateurId => UtilisateurCourant?.Id;

    public static void OuvrirSession(Utilisateur utilisateur)
    {
        UtilisateurCourant = utilisateur;
    }

    public static void FermerSession()
    {
        UtilisateurCourant = null;
    }
}
