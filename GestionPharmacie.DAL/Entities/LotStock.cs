namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Statut d'un lot de stock en pharmacie.
/// </summary>
public enum StatutLot
{
    Normal,
    Bloque
}

/// <summary>
/// Entité représentant un lot pharmaceutique spécifique avec traçabilité et péremption.
/// </summary>
public class LotStock
{
    public int Id { get; set; }
    public int ProduitId { get; set; }
    public int? FournisseurId { get; set; }
    public string NumeroLot { get; set; } = string.Empty;
    public DateTime DatePeremption { get; set; }
    public int QuantiteInitiale { get; set; }
    public int QuantiteActuelle { get; set; }
    public decimal CoutAchat { get; set; }
    public DateTime DateReception { get; set; } = DateTime.UtcNow;
    public StatutLot Statut { get; set; } = StatutLot.Normal;

    // Propriétés de navigation optionnelles / jointures d'affichage
    public string? DesignationProduit { get; set; }
    public string? NomFournisseur { get; set; }

    /// <summary>
    /// Nombre de jours restants avant la date de péremption (négatif si déjà périmé).
    /// </summary>
    public int JoursRestantsAvantPeremption => (DatePeremption.Date - DateTime.Today).Days;
}
