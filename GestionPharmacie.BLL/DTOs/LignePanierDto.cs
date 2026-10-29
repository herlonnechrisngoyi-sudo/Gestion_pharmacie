namespace GestionPharmacie.BLL.DTOs;

/// <summary>
/// DTO représentant une ligne de vente dans le panier de caisse avant validation.
/// </summary>
public class LignePanierDto
{
    public int ProduitId { get; set; }
    public string DesignationProduit { get; set; } = string.Empty;
    public int LotId { get; set; }
    public string NumeroLot { get; set; } = string.Empty;
    public int Quantite { get; set; }
    public decimal PrixUnitaire { get; set; }
    public decimal SousTotal => Quantite * PrixUnitaire;
}
