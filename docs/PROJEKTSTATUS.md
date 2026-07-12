# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit ist eine lokale Avalonia/.NET-Desktopanwendung mit einer lesenden SwiftUI-iPad-App. Der Desktop enthält eine ruhige Übersicht, grobe Systemnavigation, kompakte Vorgangslisten, einen rechten Detailinspektor und eine chronologische Terminprojektion mit Zeitfiltern.

## Architektur

- Desktop: Avalonia UI, führendes System, lokale SQLite-/Dateidaten.
- Status: additive WorkflowType-/WorkflowStep-Felder; alte Datensätze werden defensiv aus Status und Unterkategorien abgeleitet.
- Navigation: Systembereiche sind von bestehenden Benutzerkategorien getrennt; Kategorienauswahl und Verwaltung greifen auf die vorhandene Benutzerkategorienquelle zu.
- Termine: reale Aufgaben mit gültigem DueDate werden dedupliziert, chronologisch und filterbar projiziert.
- Monteure: „Kein Monteur“ ist ein UI-Sentinel und speichert eine leere Zuordnung; Profile bleiben unverändert.
- Synchronisation: vorbereitet, aber kein echter produktiver Datentransfer aktiv.

## Erledigte Hauptfunktionen

- Angebotsworkflow mit „Angebot gesendet“.
- Direktauftragsworkflow mit Auftrag, Material, Termin und Erledigt.
- Kompakte Listen mit Kundenname als Primärbezeichnung.
- Terminansicht mit Alle, Vergangen, Heute und Zukünftig.
- Neutrale Darstellung fehlender Monteurzuordnung.
- Sichtbarer verschiebbarer Detail-Splitter und lokale Detailbreitenpersistenz.

## Bekannte offene Punkte

- Vollständige per-Maus-Spaltenbreitenänderung, frei speicherbare Reihenfolge und getrennte Layoutpersistenz für Aufträge, Angebote und Termine fehlen noch.
- Mutierende End-to-End-Neustarttests auf Produktivdaten wurden aus Sicherheitsgründen nicht ausgeführt.
- Echter lokaler Netzwerk-Sync bleibt deaktiviert.

## Wichtige Entscheidungen

- WorkflowStep ist die gemeinsame sichtbare Statusquelle.
- Alte Kategorien bleiben erhalten und werden nicht automatisch verschoben oder gelöscht.
- Technische IDs bleiben intern und werden nicht als sichtbare Primärnummer verwendet.
- UI-Layoutwerte werden ausschließlich lokal gespeichert.
