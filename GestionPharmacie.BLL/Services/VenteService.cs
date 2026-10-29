using GestionPharmacie.BLL.DTOs;
using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.DAL.Entities;
using GestionPharmacie.DAL.Repositories;

namespace GestionPharmacie.BLL.Services;

public class VenteService
{
    private readonly IVenteRepository _venteRepo;
    private readonly ILotStockRepository _lotRepo;
    private readonly IProduitRepository _produitRepo;

    public VenteService(
        IVenteRepository? venteRepo = null,
        ILotStockRepository? lotRepo = null,
        IProduitRepository? produitRepo = null)
    {
        _venteRepo = venteRepo ?? new VenteRepository();
        _lotRepo = lotRepo ?? new LotStockRepository();
        _produitRepo = produitRepo ?? new ProduitRepository();
    }

    public List<Vente> ObtenirVentesRecentes(int limit = 50) => _venteRepo.GetVentesRecentes(limit);

    public Vente? ObtenirVenteParId(int id) => _venteRepo.GetById(id);

    public decimal ObtenirChiffreAffairesAujourdhui() => _venteRepo.GetChiffreAffairesJour(DateTime.Today);

    /// <summary>
    /// Valide et enregistre une vente complète avec ses lignes, paiements et mouvements de stocks.
    /// Utilise des types 'decimal' pour tous les montants monétaires.
    /// </summary>
    public Vente ValiderVente(List<LignePanierDto> panier, ModePaiement modePaiement, decimal montantPaye, string? referenceTransaction = null)
    {
        if (panier == null || panier.Count == 0)
        {
            throw new MetierException("Le panier de vente est vide.");
        }

        if (montantPaye <= 0m)
        {
            throw new MetierException("Le montant remis par le client doit être supérieur à zéro.");
        }

        int utilisateurId = SessionUtilisateur.UtilisateurId ?? 1;

        // Calcul du total décimal
        decimal montantTotal = 0m;
        foreach (var item in panier)
        {
            if (item.Quantite <= 0)
            {
                throw new MetierException($"La quantité pour '{item.DesignationProduit}' doit être strictement positive.");
            }
            if (item.PrixUnitaire < 0m)
            {
                throw new MetierException($"Le prix unitaire de '{item.DesignationProduit}' est invalide.");
            }

            // Vérification de la disponibilité du lot
            var lot = _lotRepo.GetById(item.LotId) ?? throw new MetierException($"Lot #{item.LotId} introuvable.");
            if (lot.Statut == StatutLot.Bloque)
            {
                throw new MetierException($"Le lot '{lot.NumeroLot}' du produit '{item.DesignationProduit}' est bloqué à la vente.");
            }
            if (lot.DatePeremption.Date < DateTime.Today)
            {
                throw new MetierException($"Le lot '{lot.NumeroLot}' est périmé depuis le {lot.DatePeremption:dd/MM/yyyy}.");
            }
            if (lot.QuantiteActuelle < item.Quantite)
            {
                throw new MetierException($"Stock insuffisant pour le lot '{lot.NumeroLot}' (Disponible: {lot.QuantiteActuelle}, Demandé: {item.Quantite}).");
            }

            montantTotal += item.SousTotal;
        }

        if (montantPaye < montantTotal)
        {
            throw new MetierException($"Montant insuffisant. Total: {montantTotal:C}, Payé: {montantPaye:C}. Manque: {(montantTotal - montantPaye):C}");
        }

        decimal renduMonnaie = montantPaye - montantTotal;

        // Numéro de facture unique
        string numeroFacture = GenererNumeroFacture();

        var vente = new Vente
        {
            NumeroFacture = numeroFacture,
            DateVente = DateTime.UtcNow,
            UtilisateurId = utilisateurId,
            MontantTotal = montantTotal,
            MontantPaye = montantPaye,
            RenduMonnaie = renduMonnaie,
            Statut = StatutVente.Validee
        };

        var lignes = new List<LigneVente>();
        var mouvements = new List<MouvementStock>();

        foreach (var p in panier)
        {
            lignes.Add(new LigneVente
            {
                LotId = p.LotId,
                Quantite = p.Quantite,
                PrixUnitaire = p.PrixUnitaire,
                SousTotal = p.SousTotal
            });

            mouvements.Add(new MouvementStock
            {
                ProduitId = p.ProduitId,
                LotId = p.LotId,
                TypeMouvement = TypeMouvementStock.Sortie_Vente,
                Quantite = p.Quantite,
                DateMouvement = DateTime.UtcNow,
                Motif = $"Sortie pour vente #{numeroFacture}",
                UtilisateurId = utilisateurId
            });
        }

        var paiements = new List<Paiement>
        {
            new Paiement
            {
                DatePaiement = DateTime.UtcNow,
                Montant = montantTotal,
                Mode = modePaiement,
                ReferenceTransaction = referenceTransaction
            }
        };

        int venteId = _venteRepo.EnregistrerVenteComplete(vente, lignes, paiements, mouvements);
        vente.Id = venteId;
        vente.Lignes = lignes;
        vente.Paiements = paiements;

        return vente;
    }

    public void AnnulerVente(int venteId, string motif)
    {
        if (string.IsNullOrWhiteSpace(motif))
        {
            throw new MetierException("Le motif d'annulation de vente est obligatoire.");
        }

        int userId = SessionUtilisateur.UtilisateurId ?? 1;
        bool ok = _venteRepo.AnnulerVente(venteId, userId, motif);
        if (!ok)
        {
            throw new MetierException("Impossible d'annuler cette vente.");
        }
    }

    private static string GenererNumeroFacture()
    {
        return $"FAC-{DateTime.UtcNow:yyyyMMdd}-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";
    }
}
