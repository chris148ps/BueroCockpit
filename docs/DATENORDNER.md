# BüroCockpit Datenordner

Die zentrale Datenquelle für BüroCockpit ist der OneDrive-Ordner:

```text
BueroCockpit_Daten/
```

AppProjekte enthält nur Quellcode. GitHub enthält nur Quellcode, Release-Artefakte und Dokumentation, aber keine produktiven Daten, Anhänge, Backups oder Kundendaten.

Lokale AppSettings duerfen nur geraetespezifische Einstellungen enthalten. Betriebsrelevante Daten und gemeinsame Vorgaben gehoeren in den zentralen Datenordner.

## Aktive Struktur

Der aktive Arbeitsordner der Desktop-App ist:

```text
BueroCockpit_Daten/
```

Der aktive SyncRoot wird daraus abgeleitet:

```text
BueroCockpit_Daten/Sync/
```

Die aktuelle Legacy-/Uebergangs-Live-Datei liegt damit unter:

```text
BueroCockpit_Daten/Sync/live.bclive
```

Zentrale Live-Einstellungen liegen unter:

```text
BueroCockpit_Daten/Sync/live/settings.json
```

Diese Datei enthält gemeinsame Vorgaben, die auf allen Geräten identisch sein
sollen, aktuell insbesondere `technicianNames` für die Monteur-Auswahl in
Aufgaben. Techniker/Monteure liegen kuenftig zentral in dieser Datei; lokale
`TechnicianNames` in `settings.local.json` sind nur noch Legacy/Fallback zum
einmaligen Befuellen leerer Live-Settings.

Auftraege und Kategorien liegen fachlich im zentralen Datenordner. Die Desktop-App
speichert sie in `BueroCockpit_Daten/buerocockpit.db`; der iPad-Live-Export
schreibt daraus zusaetzlich diese Lesedateien:

```text
BueroCockpit_Daten/Sync/live/tasks.json
BueroCockpit_Daten/Sync/live/categories.json
BueroCockpit_Daten/Sync/live/metadata.json
```

Die Monteur-Zuordnung an Auftraegen ist Bestandteil der Auftragsdaten und muss
zentral bleiben. `tasks.json` ist ein Export der zentralen Daten, nicht die
primaere Schreibquelle.

Lokale AppSettings bleiben fuer geraetespezifische Einstellungen wie den lokalen
Pfad zum gemeinsamen Datenordner, Darstellung, lokale Update-Testkanaele oder
lokale Uebergangspfade zustaendig. `IpadLiveFileTargetPath` ist Legacy/Uebergang;
die aktive Sync-Struktur wird aus `BueroCockpit_Daten/Sync/` abgeleitet. iCloud-Live
wird nicht mehr als zentrale Hauptloesung weiterentwickelt.

## Nicht verwenden

Diese Ordnernamen sind keine aktive Datenquelle mehr:

```text
BueroCockpit_iPad_Bearbeitung
BueroCockpit_iPad_Live
Sync/Sync
```

`BueroCockpit_iPad_Bearbeitung` war ein fachlich falscher alter OneDrive-Name. `BueroCockpit_iPad_Live` war ein iCloud-Testordner. iCloud ist nicht mehr die aktive Hauptdatenquelle.
Der alte Ordner `BueroCockpit_iPad_Bearbeitung` darf nicht mehr als aktive Quelle verwendet werden.

## Windows

Der Firmen-PC soll denselben Datenordnernamen verwenden:

```text
OneDrive - Elektro Schweim\Dokumente\BueroCockpit_Daten
```

Die App soll nicht auf einen festen Windows-Benutzer festgelegt werden. Wenn der lokale OneDrive-Pfad anders liegt, wird der konkrete lokale OneDrive-Benutzerpfad verwendet.

## iPad

Das iPad arbeitet künftig über die definierte Sync- und Mobile-Inbox-Struktur unter `BueroCockpit_Daten/Sync/`. Alte iCloud-Testordner werden nicht als aktive Quelle verwendet. Offene Altbestände werden kontrolliert importiert oder archiviert, nicht in die aktive Inbox gemischt.

Die weitere iPad-Anbindung ist nur konzeptionell vorbereitet. iCloud-Live ist Legacy/Uebergang und wird nicht weiter als aktive Hauptdatenquelle ausgebaut. Der kuenftige iPad-Abgleich soll nicht ueber iCloud-Live erfolgen, sondern ueber einen separaten lokalen Netzwerk-Sync. Details stehen in [IPAD_SYNC_KONZEPT.md](IPAD_SYNC_KONZEPT.md).
