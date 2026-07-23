# Codex-Journal: Windows-Releasekandidat 0.4.23

## Zeitpunkt

2026-07-23 20:02 +0200

## Auftrag

BC-0034 – Den vollständigen uncommitteten Stand von `codex/work` sichern und
Version `0.4.23` als lokalen Windows-x64-Releasekandidaten für die manuelle
Terminalserver-Abnahme vorbereiten.

## Konsistenzprüfung

- Standard-Lesestapel sowie `CODEX_PROJEKTREGELN.md`,
  `CODEX_AUFTRAGSPRUEFUNG.md`, `TESTRICHTLINIEN.md`,
  `DESIGN_RICHTLINIEN.md` und `RELEASE_PROZESS.md` wurden geprüft.
- Die dauerhaften Release-Gates wurden nicht gelockert. Dieser Lauf erzeugte
  ausschließlich lokale Testartefakte; vor einer Veröffentlichung ist die
  vollständige Prüfung auf sauberem `main` erneut erforderlich.
- BC-0033 wurde nach ausdrücklicher Nutzerfreigabe als `ERSETZT` archiviert.
- Produktive Daten, Datenbanken, Anhänge, Backups, Cloud- und OneDrive-Dateien
  wurden nicht verändert.

## Sicherung und Branch

- Externe Sicherung:
  `/Users/christian/AppProjekte/BueroCockpit-RC-Backups/20260723-0.4.23-preparation`
- Enthalten sind vollständiges Git-Bundle, Quellarchiv aller vorhandenen
  getrackten und ungetrackten Quelldateien, Binär-Diff, Status, Dateiliste und
  SHA-256-Prüfsummen.
- Git-Bundle und Quellarchiv wurden erfolgreich verifiziert.
- Lokaler Branch: `codex/release-0.4.23-rc`
- Ausgangs-HEAD: `73b67e9a8ea4b5436a7b7f68a4571e28c20af3ca`
- Kein Commit, Push, Tag oder GitHub-Release.

## Änderungen

- Projekt-, Assembly-, Datei- und Inno-Version auf `0.4.23` gesetzt.
- Velopack-`packId` von `BueroCockpit` auf `BueroCockpitApp` getrennt, damit
  Programmdateien unter `%LOCALAPPDATA%\BueroCockpitApp` und Produktivdaten
  unter `%LOCALAPPDATA%\BueroCockpit` liegen.
- Paketierung um Autor und Installer-Symbol ergänzt.
- PDB-Dateien aus Windows-Auslieferungen und dem optionalen Inno-Inhalt
  ausgeschlossen.
- Veraltetes separates Update-Repository im Hilfsskript auf das tatsächlich
  vom `UpdateService` verwendete Repository korrigiert.
- Lokalen Velopack-Testkanal mit synthetischer Basis `0.4.22` und Ziel
  `0.4.23` automatisiert.
- Detaillierten Terminalserver-Test-, Sicherungs-, Installations- und
  Rückfallablauf dokumentiert.

## Erzeugte Artefakte

- Übergabeordner:
  `publish/terminalserver-0.4.23-rc`
- Installer:
  `publish/terminalserver-0.4.23-rc/BueroCockpitApp-win-x64-Setup.exe`
- Windows-ZIP:
  `publish/terminalserver-0.4.23-rc/BueroCockpit-windows-x64.zip`
- Velopack-Paket:
  `publish/terminalserver-0.4.23-rc/velopack-feed/BueroCockpitApp-0.4.23-win-x64-full.nupkg`
- Der gesamte Übergabeordner besitzt ein erfolgreich geprüftes
  `SHA256SUMS.txt`.

## Prüfungen

- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- Workflow-/Kategorie-/Netzwerk-Integrationstests: erfolgreich.
- Backup-Austauschtests: erfolgreich.
- `xcodebuild` für den iPad-Simulator mit isoliertem DerivedData und
  `CODE_SIGNING_ALLOWED=NO`: erfolgreich.
- Windows-Publish und Velopack-Paketierung: erfolgreich.
- Velopack bestätigte `VelopackApp.Run()`, Version `0.4.23`, x64 und
  Desktop-/Startmenü-Verknüpfungen.
- PE-, Manifest-, Paketinhalt- und SHA-256-Prüfung: erfolgreich.
- Keine Datenbanken, Produktiv-/Testdaten oder PDB-Dateien in den
  Auslieferungsarchiven.

## Noch offen

- Windows-EXE und Installer konnten auf dem Mac mangels Windows/Wine nicht real
  gestartet werden.
- Installer, Apps-&-Features-Eintrag, Erhalt der produktiven Daten und lokaler
  Auto-Update-Weg müssen auf dem Terminalserver real geprüft werden.
- Die Artefakte sind nicht codesigniert.
- Der optionale Inno-Installer wurde auf macOS nicht kompiliert; der
  Velopack-Setup-Installer ist das vorgesehene Installationsartefakt.
