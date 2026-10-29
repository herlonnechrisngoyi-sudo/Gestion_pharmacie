namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Entité représentant un produit pharmaceutique (médicament, dispositif, consommable).
/// </summary>
public class Produit
{
    public int Id { get; set; }
    public string ReferenceUnique { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Categorie { get; set; } = string.Empty;
    public string Dosage { get; set; } = string.Empty;
    public string Forme { get; set; } = string.Empty;
    public string UniteVente { get; set; } = string.Empty;
    public decimal PrixVente { get; set; }
    public int SeuilStockMin { get; set; } = 5;
    public bool Actif { get; set; } = true;
    public DateTime DateCreation { get; set; } = DateTime.UtcNow;
}
