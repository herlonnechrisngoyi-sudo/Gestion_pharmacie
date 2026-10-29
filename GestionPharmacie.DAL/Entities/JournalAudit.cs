namespace GestionPharmacie.DAL.Entities;

/// <summary>
/// Entité enregistrant les actions critiques du système pour la conformité et l'audit.
/// </summary>
public class JournalAudit
{
    public int Id { get; set; }
    public int? UtilisateurId { get; set; }
    public string Action { get; set; } = string.Empty;
    public string TableConcernee { get; set; } = string.Empty;
    public int? EntiteId { get; set; }
    public string Details { get; set; } = string.Empty;
    public DateTime DateAction { get; set; } = DateTime.UtcNow;

    // Navigation
    public string? NomUtilisateur { get; set; }
}
