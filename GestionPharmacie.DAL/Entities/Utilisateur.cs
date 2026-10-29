namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Rôle d'un utilisateur du système.
/// </summary>
public enum RoleUtilisateur
{
    Admin,
    Vendeur
}

/// <summary>
/// Entité représentant un utilisateur du système de gestion pharmaceutique.
/// </summary>
public class Utilisateur
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Identifiant { get; set; } = string.Empty;
    public string MotDePasseHash { get; set; } = string.Empty;
    public RoleUtilisateur Role { get; set; } = RoleUtilisateur.Vendeur;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}
