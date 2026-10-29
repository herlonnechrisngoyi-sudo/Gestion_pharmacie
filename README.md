# Système de Gestion de Pharmacie (.NET Windows Forms - Architecture 3-Tiers)

Application Windows Forms moderne en architecture 3-tiers avec base de données locale **SQLite** (`Microsoft.Data.Sqlite`).

---

## 🏛️ Architecture 3-Tiers

L'application est structurée en 3 couches distinctes et découplées :

```
┌─────────────────────────────────────────────────────────────────┐
│  1. Couche UI (Présentation / Windows Forms)                    │
│     - LoginForm : Authentification, masquage & sélection rôle   │
│     - DashboardForm : Résolution ≥ 1366x768, Sidebar stable,     │
│       4 KPIs temps réel, Alertes stock & Péremption (90 jours), │
│       Masquage automatique des données sensibles pour Vendeur   │
└──────────────────────────────┬──────────────────────────────────┘
                               │ Référence
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  2. Couche Métier (BLL - Business Logic Layer)                  │
│     - AuthentificationService : Hachage PBKDF2/SHA256 avec sel   │
│     - ProduitService : Synthèse stocks, ruptures, stocks faibles│
│     - StockService : Lots FEFO, alerte péremption paramétrable  │
│     - VenteService : Transactions caisse & précision décimale   │
│     - FournisseurService, AuditService                          │
└──────────────────────────────┬──────────────────────────────────┘
                               │ Référence
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│  3. Couche Accès aux Données (DAL - Data Access Layer)          │
│     - DatabaseConnection : Centralisée, WAL, PRAGMA FK = ON     │
│     - DatabaseInitializer : DDL/DML, index, seeds par défaut    │
│     - Entités & Repositories paramétrés anti-injection SQL      │
│     - Précision monétaire 'decimal' stricte                     │
└──────────────────────────────┬──────────────────────────────────┘
                               │ Connexion locale
                               ▼
                     [ Data/pharmacie.db ]
```

---

## 🔐 Sécurité & Profils d'Accès Dynamiques

| Fonctionnalité | 👑 Administrateur | 🛒 Vendeur (Comptoir) |
| :--- | :---: | :---: |
| **Authentification sécurisée** | ✅ Identifiant + PBKDF2 | ✅ Identifiant + PBKDF2 |
| **Tableau de Bord & KPIs** | ✅ Accès complet | ✅ Accès adapté |
| **Prix de vente des articles** | ✅ Visible | ✅ Visible |
| **Coûts d'achat des lots** | ✅ Visible | ❌ **Masqué automatiquement** |
| **Journal d'Audit & Sécurité** | ✅ Visible | ❌ **Accès masqué** |
| **Configuration & Paramètres** | ✅ Visible | ❌ **Accès masqué** |
| **Caisse & Encaissements** | ✅ Disponible | ✅ Disponible |

---

## 📊 Tableau de Bord (DashboardForm)

- **Ergonomie adaptée** : Conçu pour une résolution minimale de **1366x768** avec layout fluide (Split/TableLayoutPanel, auto-scroll, bordures nettes).
- **Menu latéral stable (Sidebar)** : Largeur fixe 260px, badge de rôle dynamique, indication visuelle claire de l'onglet actif et bouton de déconnexion.
- **Indicateurs Clés (KPIs)** :
  1. **Chiffre d'affaires du jour** (`decimal`, formaté en devise monétaire).
  2. **Alertes de ruptures de stock** (Articles à 0 unité).
  3. **Stocks faibles** (Stock disponible $\le$ seuil minimum paramétré).
  4. **Lots proches de la péremption** (Basé sur le délai paramétrable de **90 jours** par défaut, modifiable directement via le sélecteur).

---

## 🚀 Lancement & Comptes de Test

```powershell
# 1. Exécution de la suite de tests unitaires (5/5 réussis)
dotnet test GestionPharmacie.slnx

# 2. Lancement de l'application
dotnet run --project GestionPharmacie.UI/GestionPharmacie.UI.csproj
```

### Comptes d'accès par défaut :
1. **Administrateur** :
   - Identifiant : `admin`
   - Mot de passe : `admin`
2. **Vendeur** :
   - Identifiant : `vendeur`
   - Mot de passe : `vendeur`
*(Des liens de pré-remplissage rapide sont disponibles directement sur l'écran de connexion)*
