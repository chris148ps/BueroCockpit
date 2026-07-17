# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-17 16:11 +0200

## Auftrag

Den behobenen macOS-Absturz bei nicht verfügbaren Cloud-Miniaturen als neues
vollständiges Windows-Auto-Update veröffentlichen.

## Konsistenzprüfung

- Geprüft wurden `AGENTS.md`, `CODEX_PROJEKTREGELN.md`,
  `CODEX_AUFTRAGSPRUEFUNG.md`, `TESTRICHTLINIEN.md`,
  `DESIGN_RICHTLINIEN.md`, `RELEASE_PROZESS.md`, `PROJEKTSTATUS.md`,
  `NEXT_TASK.md` und die tatsächliche Implementierung.
- Releaseprozess, Agentenregeln, Designrichtlinien, Updatequelle und
  Artefaktprüfung stimmen überein.
- Der separate Inno-Installer bleibt optional; alle Velopack-Pflichtdateien
  sind verbindlich.
- Die offene praktische Windows-/iPad-Zielgeräte-Abnahme bleibt offen und
  wurde nicht als bestanden dokumentiert.
- Es bestand kein ungeklärter Regel-, Fach-, Design-, Sicherheits- oder
  Releasewiderspruch.

## Enthaltener Hotfix

- Ursache war eine in OneDrive formal vorhandene, aber lokal nicht lesbare
  Anhang-Miniatur.
- Skia erhielt zuvor einen Dateistream und beendete den Prozess bei einem
  späteren `System.IO.IOException: Operation timed out`.
- `ThumbnailBitmapCache` liest Miniaturen jetzt vollständig innerhalb des
  geschützten .NET-Blocks in einen Memory-Stream.
- Nicht verfügbare oder beschädigte Miniaturen werden ausgelassen, ohne die
  App zu beenden.
- Der Fehler wurde mit einer lesenden Datenbankkopie und isolierter lokaler
  Konfiguration vor der Änderung reproduziert und nach der Änderung im echten
  macOS-Bundle erfolgreich erneut geprüft.

## Builds und Artefakte

- Workflow-/Kategorie-Integrationstest: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `./scripts/release.sh 0.4.22`: erfolgreich.
- Windows-ZIP, Velopack-Setup, Full-NuGet-Paket und alle drei
  Manifestdateien wurden im aktuellen Lauf frisch erzeugt.
- Alle sechs Dateien sind größer als 0 Byte und enthalten beziehungsweise
  referenzieren Version `0.4.22`.
- Lokale und von GitHub gemeldete SHA-256-Prüfsummen stimmen für alle sechs
  Releaseassets überein.
- Der optionale Inno-Installer wurde nicht erzeugt und nicht hochgeladen.

## Veröffentlichung

- Hotfix-Commit: `b859494`.
- Release-Commit und Tagziel: `86f2d976109969d894febe01d716b00f10ec411a`.
- Annotierter Tag: `v0.4.22`.
- `main` und Tag wurden erfolgreich zu GitHub gepusht.
- GitHub Release:
  `https://github.com/chris148ps/BueroCockpit/releases/tag/v0.4.22`.
- `gh release view v0.4.22` und die GitHub-Latest-API bestätigen ein
  veröffentlichtes Release, weder Draft noch Prerelease, mit allen sechs
  Assets im Status `uploaded`.

## Grenzen und nächster Schritt

- Produktive Daten, OneDrive-Dateien und Sync-Daten wurden nicht verändert.
- Port `53941` blieb geschlossen.
- Der praktische Auto-Update-Test auf dem Windows-Firmenrechner und die
  physische iPad-Vollübertragung bleiben die genau eine nächste
  Zielgeräte-Abnahme in `docs/NEXT_TASK.md`.
