namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Entité représentant un fournisseur de produits pharmaceutiques.
/// </summary>
public class Fournisseur
{
    public int Id { get; set; }
    public string Nom { get; set; } = string.Empty;
    public string Telephone { get; set; } = string.Empty;
    public string Adresse { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}
