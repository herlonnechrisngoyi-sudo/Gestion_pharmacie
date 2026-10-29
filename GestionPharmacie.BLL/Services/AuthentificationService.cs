using GestionPharmacie.BLL.Exceptions;
using GestionPharmacie.BLL.Security;
using GestionPharmacie.DAL.Entities;
using GestionPharmacie.DAL.Repositories;
using GestionPharmacie.DAL.Security;

namespace GestionPharmacie.BLL.Services;

public class AuthentificationService
{
    private readonly IUtilisateurRepository _utilisateurRepo;
    private readonly IJournalAuditRepository _auditRepo;

    public AuthentificationService(IUtilisateurRepository? utilisateurRepo = null, IJournalAuditRepository? auditRepo = null)
    {
        _utilisateurRepo = utilisateurRepo ?? new UtilisateurRepository();
        _auditRepo = auditRepo ?? new JournalAuditRepository();
    }

    /// <summary>
    /// Authentifie un utilisateur avec identifiant et mot de passe en clair.
    /// </summary>
    public Utilisateur Connecter(string identifiant, string motDePasse)
    {
        if (string.IsNullOrWhiteSpace(identifiant) || string.IsNullOrWhiteSpace(motDePasse))
        {
            throw new MetierException("L'identifiant et le mot de passe sont obligatoires.");
        }

        var utilisateur = _utilisateurRepo.GetByIdentifiant(identifiant);
        if (utilisateur == null || !utilisateur.Actif)
        {
            throw new MetierException("Identifiant ou mot de passe incorrect.");
        }

        bool match = PasswordHasher.VerifyPassword(motDePasse, utilisateur.MotDePasseHash);
        if (!match)
        {
            _auditRepo.EnregistrerAction(
                utilisateur.Id,
                "ECHEC_CONNEXION",
                "utilisateurs",
                utilisateur.Id,
                $"Tentative de connexion échouée pour l'utilisateur '{identifiant}'."
            );
            throw new MetierException("Identifiant ou mot de passe incorrect.");
        }

        SessionUtilisateur.OuvrirSession(utilisateur);

        _auditRepo.EnregistrerAction(
            utilisateur.Id,
            "CONNEXION",
            "utilisateurs",
            utilisateur.Id,
            $"Connexion réussie de l'utilisateur '{utilisateur.Nom}' ({utilisateur.Role})."
        );

        return utilisateur;
    }

    /// <summary>
    /// Crée un nouvel utilisateur (réservé aux administrateurs).
    /// </summary>
    public Utilisateur CreerUtilisateur(string nom, string identifiant, string motDePasse, RoleUtilisateur role)
    {
        if (string.IsNullOrWhiteSpace(nom)) throw new MetierException("Le nom de l'utilisateur est obligatoire.");
        if (string.IsNullOrWhiteSpace(identifiant)) throw new MetierException("L'identifiant est obligatoire.");
        if (string.IsNullOrWhiteSpace(motDePasse) || motDePasse.Length < 4)
            throw new MetierException("Le mot de passe doit comporter au moins 4 caractères.");

        var existant = _utilisateurRepo.GetByIdentifiant(identifiant);
        if (existant != null)
        {
            throw new MetierException($"Un compte avec l'identifiant '{identifiant}' existe déjà.");
        }

        string hash = PasswordHasher.HashPassword(motDePasse);
        var nouvelUtilisateur = new Utilisateur
        {
            Nom = nom.Trim(),
            Identifiant = identifiant.Trim(),
            MotDePasseHash = hash,
            Role = role,
            Actif = true,
            DateCreation = DateTime.UtcNow
        };

        int id = _utilisateurRepo.Add(nouvelUtilisateur);
        nouvelUtilisateur.Id = id;

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            "CREATION_UTILISATEUR",
            "utilisateurs",
            id,
            $"Création du compte '{identifiant}' avec le rôle {role}."
        );

        return nouvelUtilisateur;
    }

    /// <summary>
    /// Modifie le mot de passe d'un utilisateur après vérification de l'ancien mot de passe.
    /// </summary>
    public void ChangerMotDePasse(int utilisateurId, string ancienMotDePasse, string nouveauMotDePasse)
    {
        var user = _utilisateurRepo.GetById(utilisateurId) ?? throw new MetierException("Utilisateur introuvable.");

        if (!PasswordHasher.VerifyPassword(ancienMotDePasse, user.MotDePasseHash))
        {
            throw new MetierException("L'ancien mot de passe est incorrect.");
        }

        if (string.IsNullOrWhiteSpace(nouveauMotDePasse) || nouveauMotDePasse.Length < 4)
        {
            throw new MetierException("Le nouveau mot de passe doit comporter au moins 4 caractères.");
        }

        string nouveauHash = PasswordHasher.HashPassword(nouveauMotDePasse);
        _utilisateurRepo.UpdateMotDePasse(utilisateurId, nouveauHash);

        _auditRepo.EnregistrerAction(
            SessionUtilisateur.UtilisateurId,
            "MODIFICATION_MDP",
            "utilisateurs",
            utilisateurId,
            $"Modification du mot de passe pour l'utilisateur '{user.Identifiant}'."
        );
    }
}
