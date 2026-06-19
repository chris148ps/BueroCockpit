param()

$ErrorActionPreference = "Stop"

# Dieses Skript ist bewusst für Windows gedacht.
# Vorher müssen die Publish-Ordner vorhanden sein, z. B. durch:
#   ./scripts/publish-windows.sh

$ProjectRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$X64Publish = Join-Path $ProjectRoot "publish\windows-x64"
$InstallerScript = Join-Path $ProjectRoot "installer\BueroCockpit.iss"
$SetupExe = Join-Path $ProjectRoot "publish\installer\BueroCockpitSetup.exe"

if (-not (Test-Path $X64Publish)) {
    throw "Publish-Ordner fehlt: $X64Publish. Bitte zuerst publish/windows-x64 erzeugen."
}

if (-not (Test-Path $InstallerScript)) {
    throw "Inno-Setup-Datei fehlt: $InstallerScript"
}

$IsccCommand = Get-Command "ISCC.exe" -ErrorAction SilentlyContinue
if ($IsccCommand) {
    $IsccPath = $IsccCommand.Source
} else {
    $Candidates = @(
        "${env:LOCALAPPDATA}\Programs\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
        "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
    )
    $IsccPath = $Candidates | Where-Object { $_ -and (Test-Path $_) } | Select-Object -First 1
}

if (-not $IsccPath) {
    throw "ISCC.exe wurde nicht gefunden. Bitte Inno Setup 6 installieren oder ISCC.exe in PATH aufnehmen."
}

Write-Host "Inno Setup Compiler: $IsccPath"
Write-Host "Erstelle Installer..."
& $IsccPath $InstallerScript

if (-not (Test-Path $SetupExe)) {
    throw "Installer wurde nicht gefunden: $SetupExe"
}

Write-Host ""
Write-Host "Windows-Installer erstellt:"
Write-Host $SetupExe
