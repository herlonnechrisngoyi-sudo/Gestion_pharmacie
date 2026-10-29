using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.DAL.Entities;
using GestionPharmacie.DAL.Repositories;

namespace GestionPharmacie.BLL.Services;

public class ProduitService
{
    private readonly IProduitRepository _produitRepo;
    private readonly IJournalAuditRepository _auditRepo;

    public ProduitService(IProduitRepository? produitRepo = null, IJournalAuditRepository? auditRepo = null)
    {
        _produitRepo = produitRepo ?? new ProduitRepository();
        _auditRepo = auditRepo ?? new JournalAuditRepository();
    }

    public List<Produit> ObtenirTousLesProduits(bool seulementActifs = true)
    {
        return _produitRepo.GetAll(seulementActifs);
    }

    public List<Produit> Rechercher(string terme)
    {
        if (string.IsNullOrWhiteSpace(terme)) return _produitRepo.GetAll();
        return _produitRepo.Search(terme.Trim());
    }

    public List<Produit> ObtenirAlertesStockBas()
    {
        return _produitRepo.GetProduitsStockBas();
    }

    public List<ProduitStockResume> ObtenirSyntheseStocks()
    {
        return _produitRepo.GetSyntheseStocks();
    }

    public List<ProduitStockResume> ObtenirProduitsEnRupture()
    {
        return _produitRepo.GetSyntheseStocks().Where(p => p.EstEnRupture).ToList();
    }

    public List<ProduitStockResume> ObtenirProduitsStockFaible()
    {
        return _produitRepo.GetSyntheseStocks().Where(p => p.EstStockFaible).ToList();
    }

    public Produit ObtenirParId(int id)
    {
        return _produitRepo.GetById(id) ?? throw new MetierException($"Produit #{id} introuvable.");
    }

    public Produit EnregistrerProduit(Produit produit)
    {
        ValiderProduit(produit);

        var existant = _produitRepo.GetByReference(produit.ReferenceUnique);
        if (produit.Id == 0)
        {
            if (existant != null)
            {
                throw new MetierException($"Un produit avec la référence '{produit.ReferenceUnique}' existe déjà.");
            }

            int id = _produitRepo.Add(produit);
            produit.Id = id;

            _auditRepo.EnregistrerAction(
                SessionUtilisateur.UtilisateurId,
                "CREATION_PRODUIT",
                "produits",
                id,
                $"Ajout du produit '{produit.Designation}' (Réf: {produit.ReferenceUnique}) au prix de {produit.PrixVente:C}."
            );
        }
        else
        {
            if (existant != null && existant.Id != produit.Id)
            {
                throw new MetierException($"Un autre produit possède déjà la référence '{produit.ReferenceUnique}'.");
            }

            _produitRepo.Update(produit);

            _auditRepo.EnregistrerAction(
                SessionUtilisateur.UtilisateurId,
                "MODIFICATION_PRODUIT",
                "produits",
                produit.Id,
                $"Modification du produit '{produit.Designation}' (Réf: {produit.ReferenceUnique})."
            );
        }

        return produit;
    }

    public List<string> ObtenirCategories()
    {
        return _produitRepo.GetCategories();
    }

    public bool ArchiverProduit(int id, out string messageResultat)
    {
        var prod = ObtenirParId(id);
        bool aDesMouvements = _produitRepo.HasMouvements(id);

        _produitRepo.SetActif(id, false);

        if (aDesMouvements)
        {
            messageResultat = $"Le produit '{prod.Designation}' possède un historique de stocks ou de ventes. Il a été archivé (suppression logique pour préserver la traçabilité).";
        }
        else
        {
            messageResultat = $"Le produit '{prod.Designation}' a été archivé avec succès.";
        }

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            "ARCHIVAGE_PRODUIT",
            "produits",
            id,
            $"Archivage du produit '{prod.Designation}' (Réf: {prod.ReferenceUnique}). Historique existant : {aDesMouvements}."
        );

        return true;
    }

    public void ActiverProduit(int id)
    {
        var prod = ObtenirParId(id);
        _produitRepo.SetActif(id, true);

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            "REACTIVATION_PRODUIT",
            "produits",
            id,
            $"Réactivation du produit '{prod.Designation}' (Réf: {prod.ReferenceUnique})."
        );
    }

    public void DesactiverProduit(int id)
    {
        ArchiverProduit(id, out _);
    }

    private static void ValiderProduit(Produit produit)
    {
        if (string.IsNullOrWhiteSpace(produit.ReferenceUnique))
            throw new MetierException("La référence unique du produit est requise.");

        if (string.IsNullOrWhiteSpace(produit.Designation))
            throw new MetierException("La désignation du produit est requise.");

        if (string.IsNullOrWhiteSpace(produit.Categorie))
            throw new MetierException("La catégorie du produit est requise.");

        if (string.IsNullOrWhiteSpace(produit.UniteVente))
            throw new MetierException("L'unité de vente est requise (ex: Boîte, Flacon, Plaquette).");

        if (produit.PrixVente <= 0m)
            throw new MetierException("Le prix de vente unitaire doit être strictement positif.");

        if (produit.SeuilStockMin < 0)
            throw new MetierException("Le seuil de stock minimum ne peut pas être négatif.");
    }
}
