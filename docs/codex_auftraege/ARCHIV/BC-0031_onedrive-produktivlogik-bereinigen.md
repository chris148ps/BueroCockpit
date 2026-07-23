# Codex-Auftrag BC-0031

## Status

ABGESCHLOSSEN

## Datum

2026-07-20

## Titel

Veraltete OneDrive-Produktivlogik bereinigen

## Ziel

Eindeutig veraltete OneDrive-Produktdaten- und Desktop-Dateitransportlogik
entfernen. Lokaler Produktivdatenordner, Backup-/Import-Austausch,
Pfadsicherungen, iPad-Netzwerk-Sync und Mobile Inbox bleiben erhalten.

## Ergebnis

- Alte OneDrive-/Live-Zielpfade und Desktop-Dateiexporttrigger entfernt.
- `live.bclive` als Desktop-Transport entfernt; der lokale Netzwerkdienst
  erhält direkt ein temporäres `.bcsnapshot`.
- Automatische plattformübergreifende Umschreibung alter absoluter Datenpfade
  entfernt.
- Schutz vor umgeleiteten Produktivpfaden, BackupExchangeService,
  Netzwerk-Sync und Mobile Inbox erhalten.
- Unter `Services/LocalSync/` wurde keine Datei gelöscht.
- `AGENTS.md` verwendet für normale neue und fortgesetzte Aufträge den
  reduzierten sieben Dateien umfassenden Standard-Lesestapel. Fachdateien,
  `INDEX.md`, Auftragsarchiv und Journal werden nur noch bei konkretem Anlass
  gezielt gelesen.
- `PROJEKTREGISTER.md` ist als maßgebliche Quelle dauerhafter
  Architekturentscheidungen festgelegt.

## Tests

- `git diff --check`: erfolgreich.
- Backup-Austauschtests: erfolgreich.
- Workflow-/Kategorie-/Netzwerk-Integrationstests: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.
- Kein `xcodebuild`, da keine iPad-Datei geändert wurde.

## Offene Prüfung

Die bewusst verschobene sichtbare Backup-/Import-Geräteprüfung bleibt offen.
Der vorhandene macOS-Symlink und der OneDrive-Altbestand wurden nicht verändert.

## Beziehungen

- Zugehörige Journaldatei:
  `docs/codex_journal/2026-07-20_01-24_onedrive-produktivlogik-bereinigt.md`
- Vorgänger: `BC-0030`
- Nachfolger: `BC-0032`
