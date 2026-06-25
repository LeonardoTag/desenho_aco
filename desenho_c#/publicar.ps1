<#
.SYNOPSIS
    Compila e empacota o Capital Aco para distribuição.
    Saída: dist\CapitalAco_Instalar_vX.X.X.exe — pronto para enviar à equipe.

.REQUISITOS
    • .NET SDK 10 instalado (já usado no desenvolvimento)
    • Inno Setup 6  →  https://jrsoftware.org/isdl.php   (grátis, ~5 MB)
      Instale com as opções padrão antes de rodar este script.

.USO
    No PowerShell, dentro da pasta desenho_c#:
        .\publicar.ps1
#>

$ErrorActionPreference = "Stop"

# ─── Caminhos ────────────────────────────────────────────────────────────────
$ScriptDir  = $PSScriptRoot
$ProjDir    = Join-Path $ScriptDir "CapitalAco.DrawingMacro.App"
$DataSource = Join-Path $ScriptDir "data\chapas.csv"           # fonte canônica
$IconSource = Join-Path $ProjDir   "Assets\logo.ico"
$DistDir    = Join-Path $ScriptDir "dist"
$PublishDir = Join-Path $DistDir   "publish"
$StagingDir = Join-Path $DistDir   "staging"
$IssPath    = Join-Path $DistDir   "CapitalAco.iss"

# ─── Versão (lida de Configuracao.cs) ────────────────────────────────────────
$ConfigCs = Get-Content (Join-Path $ProjDir "Models\Configuracao.cs") -Raw
$Versao = if ($ConfigCs -match 'VersaoApp[^=]+=\s*"(\d+\.\d+\.\d+)"') { $Matches[1] } else { "1.0.0" }
$InstallerName = "CapitalAco_Instalar_v$Versao"

# ─── Candidatos ISCC ─────────────────────────────────────────────────────────
$IsccCandidates = @(
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles(x86)}\Inno Setup 5\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 5\ISCC.exe"
)
$Iscc = $IsccCandidates | Where-Object { Test-Path $_ } | Select-Object -First 1

# ─── Banner ───────────────────────────────────────────────────────────────────
Write-Host ""
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "  Capital Aco  —  Build de Distribuicao" -ForegroundColor Cyan
Write-Host "  Versao: $Versao" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# ─── [1] Limpar dist anterior ────────────────────────────────────────────────
Write-Host "[1/4] Limpando dist anterior..." -ForegroundColor Yellow
if (Test-Path $DistDir) { Remove-Item $DistDir -Recurse -Force }
New-Item -ItemType Directory $PublishDir | Out-Null
New-Item -ItemType Directory $StagingDir | Out-Null

# ─── [2] Publicar ────────────────────────────────────────────────────────────
Write-Host "[2/4] Publicando (win-x64, self-contained, single-file)..." -ForegroundColor Yellow
Write-Host "      (primeira execucao pode demorar 1-2 minutos)" -ForegroundColor DarkGray

dotnet publish $ProjDir `
    -c Release `
    -r win-x64 `
    --self-contained true `
    -p:PublishSingleFile=true `
    -p:EnableCompressionInSingleFile=true `
    -p:IncludeNativeLibrariesForSelfExtract=true `
    -p:PublishReadyToRun=false `
    -o $PublishDir `
    --nologo -v quiet

if ($LASTEXITCODE -ne 0) { throw "Falha no dotnet publish. Veja os erros acima." }

# ─── [3] Montar staging ──────────────────────────────────────────────────────
Write-Host "[3/4] Montando pacote..." -ForegroundColor Yellow

# Tudo do publish (EXE + eventuais arquivos de dados nao embutidos)
Copy-Item "$PublishDir\*" $StagingDir -Recurse -Force

# chapas.csv (dado obrigatorio)
if (-not (Test-Path $DataSource)) {
    # Fallback: bin do projeto
    $DataSource = Join-Path $ProjDir "bin\Release\net10.0-windows\data\chapas.csv"
}
if (-not (Test-Path $DataSource)) {
    $DataSource = Join-Path $ProjDir "bin\Debug\net10.0-windows\data\chapas.csv"
}
if (-not (Test-Path $DataSource)) {
    throw "chapas.csv nao encontrado. Execute uma build normal antes de publicar."
}
$DataStaging = Join-Path $StagingDir "data"
New-Item -ItemType Directory $DataStaging -Force | Out-Null
Copy-Item $DataSource $DataStaging -Force

# Remover arquivos desnecessarios para distribuicao
$RemovePatterns = @("*.pdb", "*.deps.json", "*.runtimeconfig.json")
foreach ($pat in $RemovePatterns) {
    Get-ChildItem $StagingDir -Filter $pat -Recurse | Remove-Item -Force
}

# Resumo do staging
$ExeFile = Get-ChildItem $StagingDir -Filter "*.exe" | Select-Object -First 1
$TotalMB  = [math]::Round((Get-ChildItem $StagingDir -Recurse -File | Measure-Object Length -Sum).Sum / 1MB, 1)
Write-Host "      EXE: $($ExeFile.Name)   $([math]::Round($ExeFile.Length/1MB,1)) MB" -ForegroundColor DarkGray
Write-Host "      Total staging: $TotalMB MB" -ForegroundColor DarkGray

# ─── [4] Gerar .iss e compilar ───────────────────────────────────────────────
Write-Host "[4/4] Gerando instalador com Inno Setup..." -ForegroundColor Yellow

$IconLine = if (Test-Path $IconSource) { "SetupIconFile=$IconSource" } else { "" }

$Iss = @"
; Instalador Capital Aco — gerado por publicar.ps1
; Compilar com: Inno Setup 6  https://jrsoftware.org/isinfo.php

[Setup]
AppName=Capital Aco
AppVersion=$Versao
AppPublisher=Capital Aco
DefaultDirName={localappdata}\CapitalAco
DefaultGroupName=Capital Aco
PrivilegesRequired=lowest
OutputDir=.
OutputBaseFilename=$InstallerName
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern
DisableDirPage=yes
DisableProgramGroupPage=yes
MinVersion=10.0
UninstallDisplayName=Capital Aco
UninstallDisplayIcon={app}\CapitalAco.DrawingMacro.App.exe
$IconLine

[Languages]
Name: "brazilianportuguese"; MessagesFile: "compiler:Languages\BrazilianPortuguese.isl"

[Files]
; Todos os arquivos do publish + data/chapas.csv
; biblioteca_pecas.json NAO esta no staging — o app cria automaticamente na primeira vez
Source: "staging\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs

[Icons]
Name: "{userdesktop}\Capital Aco";              Filename: "{app}\CapitalAco.DrawingMacro.App.exe"; WorkingDir: "{app}"
Name: "{userprograms}\Capital Aco\Capital Aco"; Filename: "{app}\CapitalAco.DrawingMacro.App.exe"; WorkingDir: "{app}"
Name: "{userprograms}\Capital Aco\Desinstalar"; Filename: "{uninstallexe}"

[Run]
Filename: "{app}\CapitalAco.DrawingMacro.App.exe"; Description: "Abrir o Capital Aco agora"; Flags: nowait postinstall skipifsilent

[UninstallDelete]
; Remove pastas criadas pelo app (logs, relatorios) — data\ preservada para guardar a biblioteca
Type: filesandordirs; Name: "{app}\logs"
Type: filesandordirs; Name: "{app}\files"
"@

$Iss | Out-File $IssPath -Encoding utf8 -NoNewline

if (-not $Iscc) {
    Write-Host ""
    Write-Host "ATENCAO: Inno Setup nao encontrado." -ForegroundColor Red
    Write-Host "  1. Baixe e instale em: https://jrsoftware.org/isdl.php" -ForegroundColor Yellow
    Write-Host "  2. Depois rode este script novamente, ou execute:" -ForegroundColor Yellow
    Write-Host "     iscc `"$IssPath`"" -ForegroundColor Cyan
    Write-Host ""
    Write-Host "O staging esta pronto em: $StagingDir" -ForegroundColor Cyan
    exit 0
}

Push-Location $DistDir
try {
    & $Iscc $IssPath
    if ($LASTEXITCODE -ne 0) { throw "Inno Setup falhou. Veja os erros acima." }
} finally {
    Pop-Location
}

# ─── Resultado ───────────────────────────────────────────────────────────────
$OutFile = Join-Path $DistDir "$InstallerName.exe"
$SizeMB  = [math]::Round((Get-Item $OutFile).Length / 1MB, 1)

Write-Host ""
Write-Host "============================================" -ForegroundColor Green
Write-Host "  INSTALADOR PRONTO!" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host "  Arquivo : $OutFile" -ForegroundColor Green
Write-Host "  Tamanho : $SizeMB MB" -ForegroundColor Green
Write-Host ""
Write-Host "  Envie este arquivo para a equipe." -ForegroundColor Cyan
Write-Host "  Eles so precisam dar duplo-clique e seguir o assistente." -ForegroundColor Cyan
Write-Host ""
