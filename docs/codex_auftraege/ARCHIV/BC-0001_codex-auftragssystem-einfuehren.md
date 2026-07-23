# Codex-Auftrag BC-0001

## Status

ABGESCHLOSSEN

## Datum

2026-07-20

## Titel

Codex-Auftragssystem für BüroCockpit einführen

## Ziel

Ein kompaktes und dauerhaftes Auftragssystem einführen, damit der aktuelle Auftrag sofort erkennbar ist und historische Aufträge nur bei Bedarf gelesen werden.

## Geplante Schritte

1. Verzeichnisstruktur unter `docs/codex_auftraege/` anlegen.
2. Index, Zeigerdatei, Vorlage und Roadmap erstellen.
3. `AGENTS.md` um verbindliche Leseregeln und den Abschlussablauf ergänzen.
4. Später wichtige historische Aufträge aus den Journaleinträgen rekonstruieren.

## Sicherheitsgrenzen

- Nur Projektdokumentation ändern.
- Keine App-Funktionalität verändern.
- Kein Commit, Push, Merge, Tag oder Release.
- Keine bestehenden uncommitteten Änderungen überschreiben.

## Abnahmekriterien

- [ ] Struktur vollständig angelegt
- [ ] `AGENTS.md` konsistent ergänzt
- [ ] `git diff --check` erfolgreich
- [ ] Historische Rekonstruktion separat durchgeführt

## Ergebnis

Die Grundstruktur des Codex-Auftragssystems wurde angelegt und in AGENTS.md verankert.

## Tests

- `git diff --check`: erfolgreich
- Konsistenzsuche: erfolgreich

## Beziehungen

- Verwandte Aufträge: lokale Daten- und Backup-Architektur
- Zugehörige Journaldatei: noch nicht angelegt
