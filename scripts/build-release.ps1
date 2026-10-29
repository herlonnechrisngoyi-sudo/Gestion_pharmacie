# Script de Build Release — GestionPharmacie
# Usage : .\scripts\build-release.ps1 [-Version "1.2.3"] [-OutputDir ".\publish"]
# Prérequis : .NET 10 SDK, Inno Setup 6 (iscc.exe dans le PATH ou spécifié via -InnoSetupPath)

param(
    [string]$Version    = "1.0.0",
    [string]$OutputDir  = ".\publish",
    [string]$InnoSetupPath = "C:\Program Files (x86)\Inno Setup 6\ISCC.exe",
    [switch]$BuildInstaller,
    [switch]$SkipTests
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ─── Couleurs de sortie ───────────────────────────────────────────────────────
function Write-Step   { param($msg) Write-Host "`n[>>] $msg" -ForegroundColor Cyan }
function Write-Ok     { param($msg) Write-Host "[ OK] $msg" -ForegroundColor Green }
function Write-Fail   { param($msg) Write-Host "[ERR] $msg" -ForegroundColor Red; exit 1 }

$Root      = Split-Path -Parent $PSScriptRoot
$UiProject = Join-Path $Root "GestionPharmacie.UI\GestionPharmacie.UI.csproj"
$SlnFile   = Join-Path $Root "GestionPharmacie.slnx"
$PublishDir = Join-Path $Root $OutputDir
$InstallerDir = Join-Path $Root "installer"

Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  GestionPharmacie — Build Release v$Version" -ForegroundColor Magenta
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta

# ─── 1. Vérification des prérequis ───────────────────────────────────────────
Write-Step "Vérification des prérequis"
$dotnetVer = dotnet --version
if ($LASTEXITCODE -ne 0) { Write-Fail ".NET SDK non trouvé. Installez .NET 10 SDK." }
Write-Ok ".NET SDK : $dotnetVer"

# ─── 2. Tests unitaires ───────────────────────────────────────────────────────
if (-not $SkipTests) {
    Write-Step "Exécution des tests"
    dotnet test $SlnFile -c Release -v minimal
    if ($LASTEXITCODE -ne 0) { Write-Fail "Des tests ont échoué. Build annulé." }
    Write-Ok "Tous les tests sont passés."
} else {
    Write-Host "  [SKIP] Tests ignorés (flag -SkipTests activé)." -ForegroundColor Yellow
}

# ─── 3. Nettoyage ────────────────────────────────────────────────────────────
Write-Step "Nettoyage du répertoire de publication"
if (Test-Path $PublishDir) {
    Remove-Item $PublishDir -Recurse -Force
}
New-Item -ItemType Directory -Path $PublishDir | Out-Null
Write-Ok "Répertoire créé : $PublishDir"

# ─── 4. Publication autonome (self-contained, single-file) ───────────────────
Write-Step "Publication de l'application (self-contained, single-file)"
$publishArgs = @(
    "publish", $UiProject,
    "-c", "Release",
    "-r", "win-x64",
    "--self-contained", "true",
    "-p:PublishSingleFile=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:AssemblyVersion=$Version",
    "-p:FileVersion=$Version",
    "-p:InformationalVersion=$Version",
    "-o", $PublishDir,
    "-v", "minimal"
)
dotnet @publishArgs
if ($LASTEXITCODE -ne 0) { Write-Fail "La publication a échoué." }
Write-Ok "Exécutable publié dans : $PublishDir"

# ─── 5. Copie des fichiers annexes ───────────────────────────────────────────
Write-Step "Copie des fichiers de distribution (LICENSE, README, NOTICES)"
$filesToCopy = @(
    (Join-Path $Root "LICENSE"),
    (Join-Path $Root "README.md"),
    (Join-Path $Root "THIRD-PARTY-NOTICES.txt")
)
foreach ($f in $filesToCopy) {
    if (Test-Path $f) {
        Copy-Item $f -Destination $PublishDir
        Write-Ok "  Copié : $(Split-Path $f -Leaf)"
    } else {
        Write-Host "  [WARN] Fichier manquant : $f" -ForegroundColor Yellow
    }
}

# ─── 6. Génération du fichier ZIP de distribution ────────────────────────────
Write-Step "Création de l'archive ZIP de distribution"
$zipPath = Join-Path $Root "GestionPharmacie-v$Version-win-x64.zip"
if (Test-Path $zipPath) { Remove-Item $zipPath -Force }
Compress-Archive -Path "$PublishDir\*" -DestinationPath $zipPath
Write-Ok "Archive créée : $zipPath"

# ─── 7. Build de l'installateur Inno Setup (optionnel) ───────────────────────
if ($BuildInstaller) {
    Write-Step "Compilation de l'installateur Inno Setup"
    $issFile = Join-Path $InstallerDir "setup.iss"

    if (-not (Test-Path $InnoSetupPath)) {
        Write-Host "  [WARN] ISCC.exe non trouvé à : $InnoSetupPath" -ForegroundColor Yellow
        Write-Host "         Installez Inno Setup 6 ou utilisez -InnoSetupPath pour spécifier le chemin." -ForegroundColor Yellow
    } elseif (-not (Test-Path $issFile)) {
        Write-Host "  [WARN] Fichier setup.iss non trouvé : $issFile" -ForegroundColor Yellow
    } else {
        & $InnoSetupPath $issFile /DAppVersion=$Version /DPublishDir=$PublishDir
        if ($LASTEXITCODE -ne 0) { Write-Fail "La compilation de l'installateur a échoué." }
        Write-Ok "Installateur compilé avec succès."
    }
}

# ─── Résumé ──────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta
Write-Host "  BUILD TERMINÉ AVEC SUCCÈS — v$Version" -ForegroundColor Green
Write-Host "  Exécutable  : $PublishDir\GestionPharmacie.exe" -ForegroundColor White
Write-Host "  Archive ZIP : $zipPath" -ForegroundColor White
Write-Host "═══════════════════════════════════════════════════════════" -ForegroundColor Magenta
