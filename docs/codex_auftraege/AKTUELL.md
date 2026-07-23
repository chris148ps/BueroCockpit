# Codex-Auftrag BC-0035

## Status

GEPLANT

## Datum

2026-07-23

## Titel

Terminalserver-Installation und lokalen Auto-Update-Weg abnehmen

## Ziel

Den lokalen Releasekandidaten `0.4.23` auf dem Windows-x64-Terminalserver
installieren, die alte Inno-Installation sicher ablösen und Produktivdaten
sowie Velopack-Updateweg mit dem einzigen RDP-Benutzer real prüfen.

## Geplante Schritte

1. Übertragungsartefakte per SHA-256 prüfen.
2. Lokalen isolierten Update-Test `0.4.22 → 0.4.23` durchführen.
3. Produktivdaten kalt sichern und inventarisieren.
4. Alte Inno-Installation deinstallieren, Datenordner erhalten.
5. Velopack-Setup `0.4.23` installieren.
6. Version, Daten, Verknüpfungen, Updatekomponenten, Persistenz,
   Backup-Ansicht und lokalen iPad-Sync sichtbar prüfen.

## Sicherheitsgrenzen

- `docs/TERMINALSERVER_RELEASEKANDIDAT.md` vollständig befolgen.
- Keine Produktivdaten löschen, überschreiben oder mit Testdaten mischen.
- Kein Wechsel zu `C:\ProgramData\BueroCockpit`.
- Kein Commit, Push, Merge, Tag oder GitHub-Release.
- Bei Abweichung oder Fehler abbrechen und Protokolle sichern.

## Abnahmekriterien

- [ ] SHA-256-Prüfung auf dem Terminalserver erfolgreich
- [ ] Isolierter lokaler Velopack-Update-Test erfolgreich
- [ ] Produktivdaten vor Umstellung gesichert
- [ ] Alte Installation entfernt, Datenordner erhalten
- [ ] Version `0.4.23` über Velopack installiert
- [ ] `Update.exe` und `sq.version` vorhanden
- [ ] Vorhandene Daten und BC-0032-Korrektur sichtbar bestätigt
- [ ] Neustartpersistenz und Grundfunktionen erfolgreich
- [ ] Keine Veröffentlichung ausgeführt

## Ergebnis

Noch nicht begonnen.

## Beziehungen

- Vorgänger: `BC-0034`
