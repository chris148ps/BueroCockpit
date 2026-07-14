# Nächste Aufgabe

## Ziel

Die neue Vorgangstyp-, Kategorien- und Drag-&-Drop-Bedienung auf einer entsperrten macOS-Sitzung mit einem isolierten Testprofil sichtbar abnehmen.

## Geplante Schritte

1. App mit `BUEROCOCKPIT_DATA_DIRECTORY` und `BUEROCOCKPIT_LOCAL_CONFIG_DIRECTORY` auf temporäre Ordner starten.
2. Angebots- und Direktauftragsfolgen sichtbar anklicken, Übersichtseinträge öffnen und Vorgänge per Maus zwischen zulässigen Fachkategorien ziehen.
3. Nach Neustart Typ, Bearbeitungsstand, Kategorie, Filter, Zähler und Suchergebnis sichtbar sowie per SQLite prüfen.

## Vermutlich betroffene Dateien

- `MainWindow.axaml`
- `MainWindow.axaml.cs`
- nur bei reproduzierbarem Fehler die unmittelbar betroffene Persistenz- oder Testdokumentation

## Bereiche, die nicht verändert werden dürfen

- Produktive Daten, Anhänge, Netzwerk-/Sync-Funktionen, Release, Tags, Versionen und organisatorische Vorgangstypen.
