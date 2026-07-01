# BüroCockpit Datenordner

Die zentrale Datenquelle für BüroCockpit ist der OneDrive-Ordner:

```text
BueroCockpit_Daten/
```

AppProjekte enthält nur Quellcode. GitHub enthält nur Quellcode, Release-Artefakte und Dokumentation, aber keine produktiven Daten, Anhänge, Backups oder Kundendaten.

## Aktive Struktur

Der aktive Arbeitsordner der Desktop-App ist:

```text
BueroCockpit_Daten/
```

Der aktive SyncRoot wird daraus abgeleitet:

```text
BueroCockpit_Daten/Sync/
```

Die wichtige Live-Datei liegt damit unter:

```text
BueroCockpit_Daten/Sync/live.bclive
```

## Nicht verwenden

Diese Ordnernamen sind keine aktive Datenquelle mehr:

```text
BueroCockpit_iPad_Bearbeitung
BueroCockpit_iPad_Live
Sync/Sync
```

`BueroCockpit_iPad_Bearbeitung` war ein fachlich falscher alter OneDrive-Name. `BueroCockpit_iPad_Live` war ein iCloud-Testordner. iCloud ist nicht mehr die aktive Hauptdatenquelle.

## Windows

Der Firmen-PC soll denselben Datenordnernamen verwenden:

```text
OneDrive - Elektro Schweim\Dokumente\BueroCockpit_Daten
```

Die App soll nicht auf einen festen Windows-Benutzer festgelegt werden. Wenn der lokale OneDrive-Pfad anders liegt, wird der konkrete lokale OneDrive-Benutzerpfad verwendet.

## iPad

Das iPad arbeitet künftig über die definierte Sync- und Mobile-Inbox-Struktur unter `BueroCockpit_Daten/Sync/`. Alte iCloud-Testordner werden nicht als aktive Quelle verwendet. Offene Altbestände werden kontrolliert importiert oder archiviert, nicht in die aktive Inbox gemischt.

Die weitere iPad-Anbindung ist nur konzeptionell vorbereitet. iCloud wird nicht weiter als aktive Hauptdatenquelle ausgebaut. Geplant sind entweder eine Anbindung ueber Microsoft Graph/OneDrive-API oder ein separater lokaler Netzwerk-Sync im Firmennetz. Details stehen in [IPAD_SYNC_KONZEPT.md](IPAD_SYNC_KONZEPT.md).
