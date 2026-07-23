# Nächste Aufgabe

## Auftrag

`BC-0035` – Terminalserver-Installation und lokalen Auto-Update-Weg abnehmen

## Ziel

Den lokalen Releasekandidaten `0.4.23` auf dem Windows-x64-Terminalserver mit
dem einzigen produktiven RDP-Benutzer installieren, den vorhandenen alten
Installationsstand sicher ablösen und Datenbestand sowie Velopack-Updateweg
real prüfen.

## Ausgangslage

- Letzte veröffentlichte Version: `0.4.22`
- Lokaler Releasekandidat: `0.4.23`
- Hauptentwicklungsstand: Mac mini, Branch `codex/work`
- Nur ein RDP-Benutzer verwendet den Terminalserver.
- Produktive Windows-Daten bleiben unter `%LOCALAPPDATA%\BueroCockpit`.
- Die alte Installation besitzt noch keine verlässlich nutzbare
  Auto-Update-Basis.

## Geplante Schritte

1. Übertragungsdateien anhand `SHA256SUMS.txt` prüfen.
2. Mit den bereitgestellten isolierten Pfaden den lokalen Velopack-Test
   `0.4.22 → 0.4.23` durchführen.
3. Produktivdaten unter `%LOCALAPPDATA%\BueroCockpit` bei beendeter App
   vollständig sichern und ein SHA-256-Inventar erstellen.
4. Alte Inno-Installation über Windows deinstallieren, ohne den Datenordner
   anzufassen.
5. `BueroCockpitApp-win-x64-Setup.exe` installieren und Programmwurzel
   `%LOCALAPPDATA%\BueroCockpitApp` samt `Update.exe` und `sq.version` prüfen.
6. Version, vorhandene Daten, Kategorien, Anhänge, Neustartpersistenz,
   BC-0032-Korrektur, Backup-Ansicht und manuellen lokalen iPad-Sync sichtbar
   abnehmen.

## Sicherheitsgrenzen

- Ablauf aus `docs/TERMINALSERVER_RELEASEKANDIDAT.md` befolgen.
- Produktivdaten weder löschen noch durch Testdaten ersetzen.
- Kein Wechsel zu `C:\ProgramData\BueroCockpit`.
- Kein Commit, Push, Tag oder GitHub-Release.
- Bei jedem Fehler abbrechen, Protokolle sichern und Produktivdaten
  unverändert lassen.
