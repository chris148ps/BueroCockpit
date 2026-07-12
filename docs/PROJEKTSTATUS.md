# Projektstatus BüroCockpit

## Aktueller Entwicklungsstand

BüroCockpit ist eine lokale Avalonia/.NET-Desktopanwendung mit einer lesenden SwiftUI-iPad-App. Der Desktop enthält Übersicht, grobe Hauptnavigation, kompakte Vorgangstabellen, einen rechten Detailinspektor und eine chronologische Terminansicht mit Filtern.

## Architektur

- Desktop: Avalonia UI, führendes System, lokale SQLite-/Dateidaten.
- Status: additive WorkflowType-/WorkflowStep-Felder. Gespeicherte Werte sind führend; fehlende Bestandswerte werden defensiv aus Status und alten Kategorien abgeleitet.
- Navigation/Kategorien: Systemnavigation und bestehende Benutzerkategorien bleiben getrennt; Kategorie-IDs und Zuordnungen werden nicht verschoben.
- Termine: gültige DueDate-Werte werden nach Filter dedupliziert und chronologisch dargestellt.
- Monteure: leere UI-Auswahl speichert eine leere Technikerzuordnung und verändert keine Profile.
- Layout: Splitter- und Tabellenlayoutwerte liegen in lokalen AppSettings.

## Erledigte Hauptfunktionen

- Angebotsstatus „Angebot gesendet“ und Direktauftragsablauf.
- Kompakte Listen mit Status, Kunde, Ort, Termin und Techniker.
- Terminfilter Alle, Vergangen, Heute und Zukünftig.
- Leere Monteurzuordnung ohne Sentineltext in der Anzeige.
- Übersicht ohne reservierten Splitterbereich.
- Horizontales und vertikales Scrollen der Vorgangsliste.

## Bekannte offene Punkte

- Vollständig interaktive Spaltenbreiten und Spaltenreihenfolge fehlen noch.
- Getrennte Layoutdaten sind vorbereitet, aber noch nicht vollständig auf jede Tabellenzeile angewandt.
- Sichtbarer Neustarttest für mutierende Status-/Monteuränderungen und letzte Mac-Abnahme stehen wegen gesperrter Sitzung aus.
- Netzwerk-Sync bleibt deaktiviert.

## Wichtige Entscheidungen

- WorkflowStep ist die zentrale sichtbare Statusquelle.
- Leere Monteurzuordnung bleibt vollständig leer.
- Alte Kategorien bleiben kompatibel erhalten und werden nicht destruktiv migriert.
- Tabellenlayout wird ausschließlich lokal und getrennt nach Ansicht gespeichert.
