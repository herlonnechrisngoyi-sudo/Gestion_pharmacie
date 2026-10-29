namespace GestionPharmacie.BLL.DTOs;

/// <summary>
/// DTO représentant une ligne de réception d'approvisionnement (produit, lot, péremption, quantité, coût).
/// </summary>
public class LigneReceptionDto
{
    public int ProduitId { get; set; }
    public string ReferenceProduit { get; set; } = string.Empty;
    public string DesignationProduit { get; set; } = string.Empty;
    public string NumeroLot { get; set; } = string.Empty;
    public DateTime DatePeremption { get; set; } = DateTime.Today.AddMonths(12);
    public int Quantite { get; set; } = 1;
    public decimal CoutAchat { get; set; } = 0m;
    public decimal MontantTotal => Quantite * CoutAchat;
}
