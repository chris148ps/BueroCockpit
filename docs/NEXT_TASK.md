# Nächste Aufgabe

## Ziel

Den vollständigen aktuellen Entwicklungsstand sicher in einen nachvollziehbaren Releasekandidaten für den Windows-Terminalserver überführen und daraus eine installierbare Windows-x64-Version mit Velopack-fähiger Auto-Update-Basis erzeugen.

## Ausgangslage

- Letzte veröffentlichte Version: `0.4.22`
- Nächste vorgesehene Patch-Version: `0.4.23`
- Hauptentwicklungsstand: Mac mini, Branch `codex/work`
- Der lokale Arbeitsbaum kann umfangreiche noch nicht veröffentlichte Änderungen enthalten.
- Nur ein RDP-Benutzer verwendet den Terminalserver.
- Produktive Windows-Daten dürfen deshalb unter `%LOCALAPPDATA%\BueroCockpit` liegen.
- Die alte Terminalserver-Installation besitzt noch keine verlässlich nutzbare Auto-Update-Basis.

## Geplante Schritte

1. Alle verbindlichen Regel-, Status- und Releasedateien lesen und auf Widersprüche prüfen.
2. Den vollständigen lokalen Entwicklungsstand sichern, ohne uncommittierte Änderungen zu verlieren.
3. Den Stand nachvollziehbar in einen sauberen Releasekandidaten überführen.
4. Version `0.4.23` nur nach ausdrücklicher Freigabe konsistent vorbereiten.
5. Windows-x64-Publish, Velopack-Paket und geeigneten Erstinstaller für den Terminalserver erzeugen.
6. Sicherstellen, dass Installer und spätere Updates `%LOCALAPPDATA%\BueroCockpit` niemals löschen oder überschreiben.
7. Installation und Datenübernahme anschließend real auf dem Terminalserver prüfen.
8. Erst nach erfolgreicher Abnahme über Commit, Push, Tag und GitHub-Release entscheiden.

## Grenzen

- keine produktiven Daten oder Datenbanken in Git oder Installer aufnehmen
- keine bestehenden Änderungen verwerfen, zurücksetzen oder überschreiben
- kein Versionswechsel, Tag oder Release ohne ausdrückliche Freigabe
- keine direkte produktive SQLite-Datenbank in OneDrive
- kein automatischer bidirektionaler Datenabgleich
- iPad-Direktübertragung und lokale Netzwerklogik fachlich nicht verändern
