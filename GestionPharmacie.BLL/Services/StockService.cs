using GestionPharmacie.BLL.DTOs;
using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.DAL.Connection;
using GestionPharmacie.DAL.Entities;
using GestionPharmacie.DAL.Repositories;

namespace GestionPharmacie.BLL.Services;

public class StockService
{
    private readonly ILotStockRepository _lotRepo;
    private readonly IMouvementStockRepository _mouvementRepo;
    private readonly IJournalAuditRepository _auditRepo;
    private readonly IProduitRepository _produitRepo;
    private readonly DatabaseConnection _db;

    public StockService(
        ILotStockRepository? lotRepo = null,
        IMouvementStockRepository? mouvementRepo = null,
        IJournalAuditRepository? auditRepo = null,
        IProduitRepository? produitRepo = null,
        DatabaseConnection? db = null)
    {
        _lotRepo = lotRepo ?? new LotStockRepository();
        _mouvementRepo = mouvementRepo ?? new MouvementStockRepository();
        _auditRepo = auditRepo ?? new JournalAuditRepository();
        _produitRepo = produitRepo ?? new ProduitRepository();
        _db = db ?? DatabaseConnection.Instance;
    }

    public List<LotStock> ObtenirTousLesLots() => _lotRepo.GetAll();

    public List<LotStock> ObtenirLotsParProduit(int produitId) => _lotRepo.GetLotsByProduit(produitId);

    public List<LotStock> ObtenirLotsFEFO(int produitId) => _lotRepo.GetLotsDisponiblesParFEFO(produitId);

    public List<LotStock> ObtenirLotsProchesPeremption(int jours = 90) => _lotRepo.GetLotsProchesPeremption(jours);

    /// <summary>
    /// Réceptionne un nouveau lot ou incrémente le stock d'un lot existant (Approvisionnement).
    /// </summary>
    public LotStock ReceptionnerLot(int produitId, int? fournisseurId, string numeroLot, DateTime datePeremption, int quantite, decimal coutAchat)
    {
        if (quantite <= 0) throw new MetierException("La quantité reçue doit être strictement supérieure à zéro.");
        if (coutAchat < 0m) throw new MetierException("Le coût d'achat ne peut pas être négatif.");
        if (string.IsNullOrWhiteSpace(numeroLot)) throw new MetierException("Le numéro de lot est obligatoire.");
        if (datePeremption.Date <= DateTime.Today) throw new MetierException("La date de péremption doit être ultérieure à aujourd'hui.");

        var produit = _produitRepo.GetById(produitId) ?? throw new MetierException($"Produit #{produitId} inexistant.");
        string numLotNettoye = numeroLot.Trim().ToUpperInvariant();

        return _db.ExecuteInTransaction((conn, trans) =>
        {
            var lotExistant = _lotRepo.GetByProduitEtNumeroLot(produitId, numLotNettoye, trans);
            int lotId;
            LotStock lotResultat;

            if (lotExistant != null)
            {
                // Lot déjà existant pour ce produit : incrémentation automatique du stock physique
                _lotRepo.IncrementerStock(lotExistant.Id, quantite, coutAchat, trans);
                lotId = lotExistant.Id;
                lotExistant.QuantiteActuelle += quantite;
                lotExistant.QuantiteInitiale += quantite;
                lotExistant.CoutAchat = coutAchat;
                lotResultat = lotExistant;
            }
            else
            {
                var nouveauLot = new LotStock
                {
                    ProduitId = produitId,
                    FournisseurId = fournisseurId,
                    NumeroLot = numLotNettoye,
                    DatePeremption = datePeremption.Date,
                    QuantiteInitiale = quantite,
                    QuantiteActuelle = quantite,
                    CoutAchat = coutAchat,
                    DateReception = DateTime.UtcNow,
                    Statut = StatutLot.Normal
                };

                lotId = _lotRepo.Add(nouveauLot, trans);
                nouveauLot.Id = lotId;
                lotResultat = nouveauLot;
            }

            // Enregistrer le mouvement de stock
            var mvt = new MouvementStock
            {
                ProduitId = produitId,
                LotId = lotId,
                TypeMouvement = TypeMouvementStock.Entree,
                Quantite = quantite,
                DateMouvement = DateTime.UtcNow,
                Motif = $"Réception approvisionnement lot {numLotNettoye}",
                UtilisateurId = SessionUtilisateur.UtilisateurId
            };
            _mouvementRepo.Add(mvt, trans);

            // Audit
            _auditRepo.EnregistrerAction(
                SessionUtilisateur.UtilisateurId,
                "RECEPTION_LOT",
                "lots_stocks",
                lotId,
                $"Réception de {quantite} unité(s) pour '{produit.Designation}' - Lot: {numLotNettoye} - Coût: {coutAchat:C}.",
                trans
            );

            return lotResultat;
        });
    }

    /// <summary>
    /// Valide un bon d'approvisionnement complet multi-lignes de façon transactionnelle ACID.
    /// </summary>
    public void ValiderBonDeReception(int? fournisseurId, string? bonLivraisonRef, List<LigneReceptionDto> lignes)
    {
        if (lignes == null || lignes.Count == 0)
        {
            throw new MetierException("Aucune ligne de réception à valider.");
        }

        _db.ExecuteInTransaction((conn, trans) =>
        {
            foreach (var ligne in lignes)
            {
                if (ligne.Quantite <= 0)
                    throw new MetierException($"Quantité invalide pour le produit '{ligne.DesignationProduit}'.");
                if (ligne.CoutAchat < 0m)
                    throw new MetierException($"Coût d'achat invalide pour le produit '{ligne.DesignationProduit}'.");
                if (string.IsNullOrWhiteSpace(ligne.NumeroLot))
                    throw new MetierException($"Le numéro de lot est requis pour '{ligne.DesignationProduit}'.");
                if (ligne.DatePeremption.Date <= DateTime.Today)
                    throw new MetierException($"La date de péremption du lot '{ligne.NumeroLot}' doit être ultérieure à aujourd'hui.");

                string numLot = ligne.NumeroLot.Trim().ToUpperInvariant();
                var lotExistant = _lotRepo.GetByProduitEtNumeroLot(ligne.ProduitId, numLot, trans);
                int lotId;

                if (lotExistant != null)
                {
                    _lotRepo.IncrementerStock(lotExistant.Id, ligne.Quantite, ligne.CoutAchat, trans);
                    lotId = lotExistant.Id;
                }
                else
                {
                    var nouveauLot = new LotStock
                    {
                        ProduitId = ligne.ProduitId,
                        FournisseurId = fournisseurId,
                        NumeroLot = numLot,
                        DatePeremption = ligne.DatePeremption.Date,
                        QuantiteInitiale = ligne.Quantite,
                        QuantiteActuelle = ligne.Quantite,
                        CoutAchat = ligne.CoutAchat,
                        DateReception = DateTime.UtcNow,
                        Statut = StatutLot.Normal
                    };
                    lotId = _lotRepo.Add(nouveauLot, trans);
                }

                // Mouvement stock
                var mvt = new MouvementStock
                {
                    ProduitId = ligne.ProduitId,
                    LotId = lotId,
                    TypeMouvement = TypeMouvementStock.Entree,
                    Quantite = ligne.Quantite,
                    DateMouvement = DateTime.UtcNow,
                    Motif = string.IsNullOrWhiteSpace(bonLivraisonRef)
                        ? $"Réception lot {numLot}"
                        : $"Réception BL {bonLivraisonRef} - lot {numLot}",
                    UtilisateurId = SessionUtilisateur.UtilisateurId
                };
                _mouvementRepo.Add(mvt, trans);
            }

            // Audit global du bon de réception
            _auditRepo.EnregistrerAction(
                SessionUtilisateur.UtilisateurId,
                "BON_RECEPTION",
                "lots_stocks",
                null,
                $"Validation du bon de réception ({lignes.Count} article(s)). BL: {bonLivraisonRef ?? "N/A"}.",
                trans
            );
        });
    }

    /// <summary>
    /// Bloque un lot (ex: suspicion de défaut, rappel de lot, péremption constatée).
    /// </summary>
    public void BloquerLot(int lotId, string motif)
    {
        var lot = _lotRepo.GetById(lotId) ?? throw new MetierException($"Lot #{lotId} inexistant.");
        _lotRepo.ChangerStatut(lotId, StatutLot.Bloque);

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            "BLOCAGE_LOT",
            "lots_stocks",
            lotId,
            $"Blocage du lot '{lot.NumeroLot}'. Motif : {motif}"
        );
    }

    /// <summary>
    /// Débloque un lot après vérification ou levée d'alerte.
    /// </summary>
    public void DebloquerLot(int lotId)
    {
        var lot = _lotRepo.GetById(lotId) ?? throw new MetierException($"Lot #{lotId} inexistant.");
        _lotRepo.ChangerStatut(lotId, StatutLot.Normal);

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            "DEBLOCAGE_LOT",
            "lots_stocks",
            lotId,
            $"Déblocage du lot '{lot.NumeroLot}'."
        );
    }
}
