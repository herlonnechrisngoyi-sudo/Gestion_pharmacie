using GestionPharmacie.DAL.Entities;
using GestionPharmacie.DAL.Repositories;

namespace GestionPharmacie.BLL.Services;

public class AuditService
{
    private readonly IJournalAuditRepository _auditRepo;

    public AuditService(IJournalAuditRepository? auditRepo = null)
    {
        _auditRepo = auditRepo ?? new JournalAuditRepository();
    }

    public List<JournalAudit> ObtenirDernieresActions(int limit = 100)
    {
        return _auditRepo.GetAll(limit);
    }

    public List<JournalAudit> ObtenirParUtilisateur(int utilisateurId)
    {
        return _auditRepo.GetByUtilisateur(utilisateurId);
    }
}
