namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Types de mouvements impactant les stocks pharmaceutiques.
/// </summary>
public enum TypeMouvementStock
{
    Entree,
    Sortie_Vente,
    Sortie_Perime,
    Ajustement,
    Retour
}

/// <summary>
/// Entité enregistrant chaque mouvement de stock pour une traçabilité rigoureuse.
/// </summary>
public class MouvementStock
{
    public int Id { get; set; }
    public int ProduitId { get; set; }
    public int? LotId { get; set; }
    public TypeMouvementStock TypeMouvement { get; set; }
    public int Quantite { get; set; }
    public DateTime DateMouvement { get; set; } = DateTime.UtcNow;
    public string Motif { get; set; } = string.Empty;
    public int? UtilisateurId { get; set; }

    // Navigation
    public string? DesignationProduit { get; set; }
    public string? NumeroLot { get; set; }
    public string? NomUtilisateur { get; set; }
}
