# BüroCockpit 0.4.23 – Terminalserver-Abnahme

## Zweck und Grenzen

Dieser Ablauf prüft den lokalen Releasekandidaten `0.4.23`. Er erstellt keinen
Commit, keinen Tag, keinen Push und keinen GitHub-Release.

Der Terminalserver wird von genau einem RDP-Benutzer verwendet. Deshalb bleiben
die produktiven Daten benutzerbezogen unter:

```text
%LOCALAPPDATA%\BueroCockpit
```

Die Velopack-Installation liegt getrennt unter:

```text
%LOCALAPPDATA%\BueroCockpitApp
```

Diese Trennung ist zwingend. Der Installer, Velopack-Updates und die
Deinstallation dürfen `%LOCALAPPDATA%\BueroCockpit` nicht als
Installationswurzel verwenden oder löschen.

## Bereitgestellte Dateien

Der Übertragungsordner liegt nach dem lokalen Paketlauf unter:

```text
publish\terminalserver-0.4.23-rc
```

Wichtige Inhalte:

- `BueroCockpitApp-win-x64-Setup.exe`: eigentlicher Velopack-Installer `0.4.23`
- `BueroCockpit-windows-x64.zip`: portable Kontrollausgabe
- `velopack-feed`: lokaler Ziel-Feed `0.4.23`
- `update-test\initial`: synthetische Velopack-Testbasis `0.4.22`
- `update-test\feed`: lokaler Update-Feed `0.4.23`
- `SHA256SUMS.txt`: Prüfsummen der übertragenen Dateien

Die Artefakte sind in diesem lokalen Lauf nicht signiert. Windows SmartScreen
kann deshalb warnen. Vor einer späteren breiten Veröffentlichung sollte eine
Windows-Codesignatur eingerichtet werden.

## A. Übertragung prüfen

Den gesamten Ordner auf den Terminalserver kopieren und dort in PowerShell
ausführen:

```powershell
Set-Location "<kopierter Ordner>\terminalserver-0.4.23-rc"
Get-Content .\SHA256SUMS.txt
Get-ChildItem -Recurse -File |
    Where-Object Name -ne "SHA256SUMS.txt" |
    Get-FileHash -Algorithm SHA256 |
    Sort-Object Path
```

Die Werte müssen mit `SHA256SUMS.txt` übereinstimmen.

## B. Auto-Update isoliert prüfen

Dieser Test läuft vor der produktiven Umstellung und verwendet ausschließlich
isolierte Testpfade.

1. BüroCockpit vollständig beenden.
2. Die synthetische Basis ohne automatischen App-Start installieren:

```powershell
& ".\update-test\initial\BueroCockpitApp-win-x64-Setup.exe" `
    --silent `
    --log "$env:TEMP\BueroCockpit-velopack-install.log"
```

3. Isolierte Pfade setzen und die installierte App starten:

```powershell
$env:BUEROCOCKPIT_DATA_DIRECTORY =
    Join-Path $env:TEMP "BueroCockpit-RC-UpdateTest\Data"
$env:BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY =
    Join-Path $env:TEMP "BueroCockpit-RC-UpdateTest\Local"

& "$env:LOCALAPPDATA\BueroCockpitApp\BueroCockpit.exe"
```

4. Unter `Einstellungen > Updates` als lokalen Update-Kanal den vollständigen
   Pfad zu `update-test\feed` eintragen.
5. `Nach Updates suchen` ausführen. Version `0.4.23` muss gefunden werden.
6. Update installieren und den automatischen Neustart abwarten.
7. Angezeigte Version `0.4.23` sowie folgende Dateien prüfen:

```powershell
Test-Path "$env:LOCALAPPDATA\BueroCockpitApp\Update.exe"
Test-Path "$env:LOCALAPPDATA\BueroCockpitApp\current\sq.version"
Test-Path "$env:LOCALAPPDATA\BueroCockpitApp\current\BueroCockpit.exe"
```

8. Prüfen, dass `%LOCALAPPDATA%\BueroCockpit` während dieses isolierten Tests
   unverändert geblieben ist.

## C. Produktivdaten vor der Umstellung sichern

1. Alte BüroCockpit-Version normal starten und – falls dort vorhanden – eine
   Anwendungssicherung erzeugen.
2. BüroCockpit vollständig beenden. Im Task-Manager darf kein
   `BueroCockpit.exe` mehr laufen.
3. Den vorhandenen Datenstand kalt und ohne Löschung kopieren:

```powershell
$data = Join-Path $env:LOCALAPPDATA "BueroCockpit"
$stamp = Get-Date -Format "yyyyMMdd-HHmmss"
$backup = Join-Path $env:USERPROFILE "Documents\BueroCockpit-RC-$stamp"

New-Item -ItemType Directory -Path $backup -Force | Out-Null
robocopy $data (Join-Path $backup "Data") /E /COPY:DAT /DCOPY:DAT /R:1 /W:1 /XJ
if ($LASTEXITCODE -gt 7) {
    throw "Sicherung mit robocopy fehlgeschlagen: $LASTEXITCODE"
}

Get-ChildItem $data -Recurse -File |
    Get-FileHash -Algorithm SHA256 |
    Select-Object Path, Hash |
    Export-Csv (Join-Path $backup "data-before.csv") `
        -NoTypeInformation -Encoding UTF8
```

4. Sicherungsordner und mindestens `buerocockpit.db` kontrollieren.

## D. Alte Installation ablösen

1. Ziel der bisherigen Desktop-/Startmenü-Verknüpfung prüfen. Liegt die alte
   Programmdatei wider Erwarten innerhalb
   `%LOCALAPPDATA%\BueroCockpit`, den Vorgang abbrechen.
2. Die alte Inno-Installation ausschließlich über
   `Einstellungen > Apps > Installierte Apps` deinstallieren.
3. `%LOCALAPPDATA%\BueroCockpit` weder auswählen noch manuell löschen.
4. Direkt nach der Deinstallation prüfen:

```powershell
Test-Path "$env:LOCALAPPDATA\BueroCockpit\buerocockpit.db"
```

Das Ergebnis muss bei vorhandenem Datenbestand `True` sein.

## E. Releasekandidat 0.4.23 installieren

Den neuen Installer zunächst ohne automatischen App-Start ausführen:

```powershell
& ".\BueroCockpitApp-win-x64-Setup.exe" `
    --silent `
    --log "$env:TEMP\BueroCockpit-0.4.23-install.log"
```

Danach müssen folgende Dateien vorhanden sein:

```powershell
Test-Path "$env:LOCALAPPDATA\BueroCockpitApp\BueroCockpit.exe"
Test-Path "$env:LOCALAPPDATA\BueroCockpitApp\Update.exe"
Test-Path "$env:LOCALAPPDATA\BueroCockpitApp\current\sq.version"
Test-Path "$env:LOCALAPPDATA\BueroCockpitApp\current\BueroCockpit.exe"
Test-Path "$env:LOCALAPPDATA\BueroCockpit\buerocockpit.db"
```

Anschließend BüroCockpit über die neue Verknüpfung starten.

## F. Sichtbare Abnahme

Mindestens prüfen:

1. Version `0.4.23` wird angezeigt.
2. Vorhandene Vorgänge, Kategorien, Einstellungen und Anhänge sind vollständig
   sichtbar.
3. Ein erledigter Vorgang bleibt in seiner konfigurierten normalen Kategorie.
4. Ein isoliert angelegter Testvorgang lässt sich speichern und nach Neustart
   wieder öffnen.
5. Backup-/Import-Ansicht öffnet sich; produktive Daten werden dabei nicht
   testweise ersetzt.
6. Lokaler Sync-Dienst startet nur nach bewusster Bedienung und ist nach Stop
   nicht mehr erreichbar.
7. iPad erreicht den Terminalserver im Firmennetz per manueller Adresse; die
   automatische Suche benötigt Bonjour/mDNS.
8. Unter `Einstellungen > Updates` wird der GitHub-Release-Kanal angezeigt.
   Ein öffentliches Update auf `0.4.23` ist vor dem späteren GitHub-Release
   noch nicht zu erwarten.

## G. Abbruch und Rückfall

Bei einem Fehler:

1. BüroCockpit beenden.
2. Installations- und Updateprotokolle sichern:
   - `%TEMP%\BueroCockpit-0.4.23-install.log`
   - `%LOCALAPPDATA%\velopack\velopack_BueroCockpitApp.log`
3. Produktivdaten nicht löschen oder überschreiben.
4. Den Lauf mit Fehlermeldung, Screenshot und den gesicherten Protokollen
   abbrechen.
5. Die kalte Sicherung nur nach gesonderter Entscheidung zurückspielen.

## Veröffentlichungssperre

Erst nach erfolgreicher Terminalserver-Abnahme wird getrennt entschieden über:

- Commit und Übernahme auf `main`
- Tag `v0.4.23`
- Push
- GitHub-Release mit allen Velopack-Pflichtartefakten
- praktischen Auto-Update-Test über den veröffentlichten GitHub-Kanal

Vor diesen Schritten ist der vollständige Release-Gate erneut auf einem
sauberen `main` auszuführen.
