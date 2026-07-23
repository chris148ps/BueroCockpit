# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-23 20:02 +0200

## Auftrag

BC-0034 – Vollständigen Arbeitsstand sichern und Windows-x64-
Releasekandidaten `0.4.23` für den Terminalserver vorbereiten.

## Ergebnis

- Der vollständige uncommittete Ausgangsstand wurde außerhalb des Repositorys
  als verifiziertes Git-Bundle, Quellarchiv, Binär-Diff und SHA-256-Inventar
  gesichert.
- Lokaler Branch `codex/release-0.4.23-rc` wurde ohne Verlust vorhandener
  Änderungen angelegt.
- Version `0.4.23`, Windows-Publish, portable ZIP, Velopack-Setup,
  Full-NuGet-Paket, Portable-Paket und Update-Manifeste wurden frisch erzeugt.
- Die Installationswurzel `%LOCALAPPDATA%\BueroCockpitApp` ist vom produktiven
  Datenpfad `%LOCALAPPDATA%\BueroCockpit` getrennt.
- Ein lokaler Update-Test `0.4.22 → 0.4.23` und der vollständige
  Terminalserver-Testablauf wurden vorbereitet.

## Prüfungen

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- Workflow-/Kategorie-/Netzwerk-Integrationstests: erfolgreich.
- Backup-Austauschtests: erfolgreich.
- iPad-Simulator-Build: erfolgreich.
- Windows-Publish, Velopack-Paketierung, Manifest-, PE-, Archivinhalt- und
  SHA-256-Prüfung: erfolgreich.
- Auslieferungsarchive enthalten keine Datenbanken, Produktiv-, Test- oder
  PDB-Dateien.

## Artefakte

- Übergabe:
  `/Users/christian/AppProjekte/BueroCockpit/publish/terminalserver-0.4.23-rc`
- Installer:
  `/Users/christian/AppProjekte/BueroCockpit/publish/terminalserver-0.4.23-rc/BueroCockpitApp-win-x64-Setup.exe`
- Windows-Publish:
  `/Users/christian/AppProjekte/BueroCockpit/publish/windows-x64`
- Externe Ausgangssicherung:
  `/Users/christian/AppProjekte/BueroCockpit-RC-Backups/20260723-0.4.23-preparation`

## Grenzen

- Kein Commit, Push, Merge, Tag oder GitHub-Release.
- Keine produktiven Daten oder Cloud-Dateien verändert.
- Kein realer Windows-Start auf macOS möglich.
- Artefakte nicht codesigniert.
- Terminalserver-Abnahme ist BC-0035.
