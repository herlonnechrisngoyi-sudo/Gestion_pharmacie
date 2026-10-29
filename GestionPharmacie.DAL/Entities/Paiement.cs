namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Modes de paiement acceptés en caisse.
/// </summary>
public enum ModePaiement
{
    Especes,
    CarteBancaire,
    Virement,
    Cheque,
    MobileMoney
}

/// <summary>
/// Entité représentant un règlement de vente.
/// </summary>
public class Paiement
{
    public int Id { get; set; }
    public int VenteId { get; set; }
    public DateTime DatePaiement { get; set; } = DateTime.UtcNow;
    public decimal Montant { get; set; }
    public ModePaiement Mode { get; set; } = ModePaiement.Especes;
    public string? ReferenceTransaction { get; set; }
}
