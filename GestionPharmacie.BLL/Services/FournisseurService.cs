using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.DAL.Entities;
using GestionPharmacie.DAL.Repositories;

namespace GestionPharmacie.BLL.Services;

public class FournisseurService
{
    private readonly IFournisseurRepository _fournisseurRepo;
    private readonly IJournalAuditRepository _auditRepo;

    public FournisseurService(IFournisseurRepository? fournisseurRepo = null, IJournalAuditRepository? auditRepo = null)
    {
        _fournisseurRepo = fournisseurRepo ?? new FournisseurRepository();
        _auditRepo = auditRepo ?? new JournalAuditRepository();
    }

    public List<Fournisseur> ObtenirTous(bool seulementActifs = true) => _fournisseurRepo.GetAll(seulementActifs);

    public List<Fournisseur> Rechercher(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return ObtenirTous(false);
        return _fournisseurRepo.Search(query.Trim());
    }

    public void ChangerStatut(int id, bool actif)
    {
        var f = ObtenirParId(id);
        _fournisseurRepo.SetActif(id, actif);

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            actif ? "ACTIVATION_FOURNISSEUR" : "DESACTIVATION_FOURNISSEUR",
            "fournisseurs",
            id,
            $"Changement de statut du fournisseur '{f.Nom}' : {(actif ? "Actif" : "Inactif")}."
        );
    }

    public Fournisseur ObtenirParId(int id)
    {
        return _fournisseurRepo.GetById(id) ?? throw new MetierException($"Fournisseur #{id} introuvable.");
    }

    public Fournisseur EnregistrerFournisseur(Fournisseur fournisseur)
    {
        if (string.IsNullOrWhiteSpace(fournisseur.Nom))
        {
            throw new MetierException("Le nom du fournisseur est obligatoire.");
        }

        if (fournisseur.Id == 0)
        {
            int id = _fournisseurRepo.Add(fournisseur);
            fournisseur.Id = id;

            _auditRepo.EnregistrerAction(
                SessionUtilisateur.UtilisateurId,
                "CREATION_FOURNISSEUR",
                "fournisseurs",
                id,
                $"Création du fournisseur '{fournisseur.Nom}'."
            );
        }
        else
        {
            _fournisseurRepo.Update(fournisseur);

            _auditRepo.EnregistrerAction(
                SessionUtilisateur.UtilisateurId,
                "MODIFICATION_FOURNISSEUR",
                "fournisseurs",
                fournisseur.Id,
                $"Modification du fournisseur '{fournisseur.Nom}'."
            );
        }

        return fournisseur;
    }

    public void SupprimerFournisseur(int id)
    {
        var f = ObtenirParId(id);
        _fournisseurRepo.Delete(id);

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            "DESACTIVATION_FOURNISSEUR",
            "fournisseurs",
            id,
            $"Désactivation du fournisseur '{f.Nom}'."
        );
    }
}
