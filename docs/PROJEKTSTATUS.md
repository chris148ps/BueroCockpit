# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit ist eine lokale Avalonia/.NET-Desktopanwendung mit einer lesenden SwiftUI-iPad-App für Snapshots und mobile Erfassung. Der Desktop enthält eine ruhige Tagesübersicht, eine getrennte Systemnavigation mit groben Arbeitsbereichen, eine kompakte Vorgangstabelle und einen rechten Detailinspektor. Der Detailbereich ist über einen sichtbaren Splitter veränderbar.

## Architektur

- Desktop: Avalonia UI, führendes System, lokale SQLite-/Dateidaten.
- Vorgänge: Auftrags- und Angebotsabläufe werden über additive WorkflowType-/WorkflowStep-Felder gespeichert. Fehlende Felder werden aus Bestandsdaten defensiv abgeleitet.
- Navigation: Die tägliche Hauptnavigation zeigt Systembereiche; vorhandene Benutzerkategorien bleiben für Zuordnungen und Verwaltung erhalten.
- Auftragsansicht: Kompakte Liste mit Status, Kunde, Ort, Termin und Techniker sowie rechter Detailinspektor.
- Synchronisation: lokaler Netzwerk-Sync ist vorbereitet, aber kein echter produktiver Datentransfer aktiv.
- Veröffentlichung: größere Codex-Arbeiten werden über codex/work und den bestehenden Draft-PR nach main sichtbar gemacht; Merge bleibt manuell.

## Erledigte Hauptfunktionen

- Übersicht mit realen Tagesinformationen und ruhigen Leerzuständen.
- Kompakte grobe Hauptnavigation.
- Kompakte Auftragsliste ohne sichtbare technische Auftragsnummer als Primärbezeichnung.
- Sichtbarer verschiebbarer Splitter zwischen Liste und Detailbereich mit lokaler Breitenpersistenz.
- Gemeinsame sichtbare Workflow-Statusquelle für Stepper, Auswahl und Listen-Badge.
- Rückwärtskompatible Bestandsableitung ohne Löschung oder Neu-Zuordnung vorhandener Kategorien und Aufträge.

## Bekannte offene Punkte

- Die eigenständige chronologische Terminansicht mit den Filtern Alle, Vergangen, Heute und Zukünftig steht als nächste Aufgabe aus.
- Eine mutierende Statusprobe auf einem bestehenden Produktivauftrag wurde nicht durchgeführt, um Bestandsdaten unverändert zu lassen.
- Der echte lokale Netzwerk-Sync und produktive Datenübertragung sind weiterhin nicht aktiviert.

## Wichtige Entscheidungen

- Kundenname und WorkflowStep sind die primären sichtbaren Vorgangsinformationen.
- Technische IDs bleiben intern erhalten und werden nicht als sichtbare Nummernspalte in der Vorgangsliste verwendet.
- Die Breite des Detailbereichs ist lokale Geräteeinstellung und wird nicht synchronisiert.
- Produktivdaten, Tags, Releases und Versionsnummern bleiben außerhalb dieses Workflows.
