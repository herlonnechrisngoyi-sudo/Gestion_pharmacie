namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Synthèse du stock disponible par produit avec niveau d'alerte (Rupture / Faible / Normal).
/// </summary>
public class ProduitStockResume
{
    public int ProduitId { get; set; }
    public string ReferenceUnique { get; set; } = string.Empty;
    public string Designation { get; set; } = string.Empty;
    public string Categorie { get; set; } = string.Empty;
    public decimal PrixVente { get; set; }
    public int SeuilStockMin { get; set; }
    public int StockDisponible { get; set; }
    public bool Actif { get; set; }

    public bool EstEnRupture => StockDisponible <= 0;
    public bool EstStockFaible => StockDisponible > 0 && StockDisponible <= SeuilStockMin;

    public string StatutStock => EstEnRupture ? "Rupture de Stock" : (EstStockFaible ? "Stock Faible" : "Normal");
}
