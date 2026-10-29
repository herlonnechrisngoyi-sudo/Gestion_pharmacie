namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Statut d'une vente en caisse.
/// </summary>
public enum StatutVente
{
    Validee,
    Annulee
}

/// <summary>
/// Entité représentant l'entête d'une transaction de vente pharmaceutique.
/// </summary>
public class Vente
{
    public int Id { get; set; }
    public string NumeroFacture { get; set; } = string.Empty;
    public DateTime DateVente { get; set; } = DateTime.UtcNow;
    public int UtilisateurId { get; set; }
    public decimal MontantTotal { get; set; }
    public decimal MontantPaye { get; set; }
    public decimal RenduMonnaie { get; set; }
    public StatutVente Statut { get; set; } = StatutVente.Validee;

    // Navigation / jointures
    public string? NomVendeur { get; set; }
    public List<LigneVente> Lignes { get; set; } = new();
    public List<Paiement> Paiements { get; set; } = new();
}

/// <summary>
/// Détail d'une ligne d'article vendue rattachée à un lot spécifique.
/// </summary>
public class LigneVente
{
    public int Id { get; set; }
    public int VenteId { get; set; }
    public int LotId { get; set; }
    public int Quantite { get; set; }
    public decimal PrixUnitaire { get; set; }
    public decimal SousTotal { get; set; }

    // Navigation
    public string? DesignationProduit { get; set; }
    public string? NumeroLot { get; set; }
}
