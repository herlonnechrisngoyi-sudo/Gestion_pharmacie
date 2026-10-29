; ═══════════════════════════════════════════════════════════════════════════════
; GestionPharmacie — Script Inno Setup 6
; Génère un installateur professionnel pour Windows (x64)
;
; Prérequis :
;   - Inno Setup 6.x  → https://jrsoftware.org/isinfo.php
;   - Répertoire publish\ généré par scripts\build-release.ps1
;   - (Optionnel) Fichier icône : installer\app.ico
;
; Compilation manuelle :
;   ISCC.exe installer\setup.iss /DAppVersion=1.2.3 /DPublishDir=.\publish
; ═══════════════════════════════════════════════════════════════════════════════

; ── Définition des constantes (surchargeable via /DParam=value en ligne de commande) ──
#ifndef AppVersion
  #define AppVersion "1.0.0"
#endif

#ifndef PublishDir
  #define PublishDir "..\publish"
#endif

#define AppName        "Gestion de Pharmacie"
#define AppExeName     "GestionPharmacie.exe"
#define AppPublisher   "GestionPharmacie Contributors"
#define AppURL         "https://github.com/votre-org/gestion-pharmacie"
#define AppSupportURL  "https://github.com/votre-org/gestion-pharmacie/issues"
#define AppId          "{{A1B2C3D4-E5F6-7890-ABCD-EF1234567890}"
; ↑ Générez un GUID unique sur https://guidgenerator.com/ et remplacez celui-ci

[Setup]
; ── Identité ──
AppId={#AppId}
AppName={#AppName}
AppVersion={#AppVersion}
AppVerName={#AppName} v{#AppVersion}
AppPublisher={#AppPublisher}
AppPublisherURL={#AppURL}
AppSupportURL={#AppSupportURL}
AppUpdatesURL={#AppURL}/releases

; ── Répertoire d'installation par défaut ──
DefaultDirName={autopf}\{#AppName}
DefaultGroupName={#AppName}
DisableProgramGroupPage=yes

; ── Architecture ──
ArchitecturesInstallIn64BitMode=x64compatible
ArchitecturesAllowed=x64compatible

; ── Comportement de l'installateur ──
; Demande les droits admin uniquement si nécessaire (installation machine ou utilisateur)
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; ── Licence (EULA) ──
LicenseFile=..\LICENSE

; ── Icône de l'installateur ──
; Décommentez la ligne suivante si vous avez un fichier app.ico dans ce dossier :
; SetupIconFile=app.ico

; ── Fichier de sortie ──
OutputDir=..\dist
OutputBaseFilename=GestionPharmacie-v{#AppVersion}-Setup
Compression=lzma2/ultra64
SolidCompression=yes
InternalCompressLevel=ultra64

; ── Métadonnées du fichier d'installation ──
VersionInfoVersion={#AppVersion}
VersionInfoCompany={#AppPublisher}
VersionInfoDescription={#AppName} Installateur
VersionInfoCopyright=Copyright (c) 2026 {#AppPublisher}
VersionInfoProductName={#AppName}
VersionInfoProductVersion={#AppVersion}

; ── Apparence ──
WizardStyle=modern
WizardResizable=yes
ShowLanguageDialog=no

; ── Désinstallation ──
UninstallDisplayName={#AppName}
; UninstallDisplayIcon={app}\{#AppExeName}
CreateUninstallRegKey=yes

[Languages]
Name: "french"; MessagesFile: "compiler:Languages\French.isl"

[Tasks]
; Raccourcis optionnels que l'utilisateur peut cocher/décocher pendant l'installation
Name: "desktopicon";    Description: "Créer une icône sur le &Bureau";             GroupDescription: "Raccourcis :"; Flags: unchecked
Name: "startmenuicon";  Description: "Créer un raccourci dans le &Menu Démarrer";  GroupDescription: "Raccourcis :"; Flags: checkedonce

[Files]
; ── Exécutable principal ──
Source: "{#PublishDir}\{#AppExeName}";         DestDir: "{app}"; Flags: ignoreversion

; ── Documentation et licences ──
Source: "{#PublishDir}\LICENSE";                DestDir: "{app}"; Flags: ignoreversion isreadme
Source: "{#PublishDir}\README.md";              DestDir: "{app}"; Flags: ignoreversion
Source: "{#PublishDir}\THIRD-PARTY-NOTICES.txt"; DestDir: "{app}"; Flags: ignoreversion

; NOTE : L'exécutable self-contained single-file intègre le runtime .NET,
; toutes les DLL de dépendance et les ressources nationales. Aucun autre
; fichier binaire n'est nécessaire dans le répertoire publish.

[Icons]
; Raccourci Menu Démarrer (toujours créé)
Name: "{group}\{#AppName}";              Filename: "{app}\{#AppExeName}";  Tasks: startmenuicon
Name: "{group}\Désinstaller {#AppName}"; Filename: "{uninstallexe}";       Tasks: startmenuicon

; Raccourci Bureau (optionnel)
Name: "{autodesktop}\{#AppName}";        Filename: "{app}\{#AppExeName}";  Tasks: desktopicon

[Run]
; Proposer de lancer l'application à la fin de l'installation
Filename: "{app}\{#AppExeName}"; \
  Description: "Lancer {#AppName} maintenant"; \
  Flags: nowait postinstall skipifsilent

[UninstallRun]
; Rien de spécial à faire à la désinstallation (pas de service à arrêter)

[Dirs]
; Répertoire de données utilisateur — créé lors de l'installation pour éviter les erreurs UAC
Name: "{localappdata}\{#AppName}";        Permissions: users-modify

[Registry]
; Enregistrement de l'application dans "Ajout/Suppression de programmes"
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}";   ValueType: string; ValueName: "InstallPath"; ValueData: "{app}"; Flags: uninsdeletekey
Root: HKCU; Subkey: "Software\{#AppPublisher}\{#AppName}";   ValueType: string; ValueName: "Version";     ValueData: "{#AppVersion}"

[Code]
// ── Vérification de la version Windows minimale ──
// GestionPharmacie nécessite Windows 10 x64 ou supérieur (.NET 10 WinForms)
function InitializeSetup(): Boolean;
var
  Version: TWindowsVersion;
begin
  GetWindowsVersionEx(Version);
  // Windows 10 = MajorVersion 10, Build >= 10240
  if (Version.Major < 10) or
     ((Version.Major = 10) and (Version.Build < 10240)) then
  begin
    MsgBox(
      'GestionPharmacie nécessite Windows 10 ou une version ultérieure.' + #13#10 +
      'L''installation ne peut pas continuer.',
      mbCriticalError, MB_OK
    );
    Result := False;
  end else
    Result := True;
end;
