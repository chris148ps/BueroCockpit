# BüroCockpit Datenordner

Die lokale Datenquelle für BüroCockpit ist der Datenordner:

```text
BueroCockpit_Daten/
```

AppProjekte enthält nur Quellcode. GitHub enthält nur Quellcode, Release-Artefakte und Dokumentation, aber keine produktiven Daten, Anhänge, Backups oder Kundendaten.

Lokale AppSettings duerfen nur geraetespezifische Einstellungen enthalten. Betriebsrelevante Daten und gemeinsame Vorgaben gehoeren in den lokalen Datenordner.

## Aktive Struktur

Der aktive Arbeitsordner der Desktop-App ist:

```text
BueroCockpit_Daten/
```

Der aktive SyncRoot wird daraus abgeleitet:

```text
BueroCockpit_Daten/Sync/
```

Vorbereitete mobile Netzwerk-Eingaenge sollen kuenftig unter diesem SyncRoot landen:

```text
BueroCockpit_Daten/Sync/inbox/mobile-<yyyyMMdd-HHmmss>-<kurzid>/
BueroCockpit_Daten/Sync/processed/
BueroCockpit_Daten/Sync/conflicts/
```

Das ist nur die Zielablage fuer einen spaeteren manuellen lokalen Netzwerk-Sync. In diesem Stand wird kein Server gestartet und kein Upload angenommen. Bestehende manuelle Mobile-Inbox-Ordner `mobile-inbox`/`mobile-processed` bleiben Legacy/Kompatibilitaet und werden nicht verschoben.

Auftraege und Kategorien liegen fachlich im lokalen Datenordner. Die Desktop-App
speichert sie in `BueroCockpit_Daten/buerocockpit.db`. Ein spaeterer iPad-Lesestand
wird ueber den lokalen Netzwerk-Sync definiert.

Die Monteur-Zuordnung an Auftraegen ist Bestandteil der Auftragsdaten und muss
lokal erhalten bleiben.

Lokale AppSettings bleiben fuer geraetespezifische Einstellungen wie den lokalen
Pfad zum Datenordner, Darstellung oder lokale Update-Testkanaele zustaendig.
Historische iPad-Dateipfade bleiben nur als tolerant gelesener Altbestand erhalten.

## Nicht verwenden

Diese Ordnernamen sind keine aktive Datenquelle mehr:

```text
BueroCockpit_iPad_Bearbeitung
BueroCockpit_iPad_Live
Sync/Sync
```

`BueroCockpit_iPad_Bearbeitung` und `BueroCockpit_iPad_Live` waren alte Kopplungsnamen und sind keine aktive Datenquelle mehr.
Der alte Ordner `BueroCockpit_iPad_Bearbeitung` darf nicht mehr als aktive Quelle verwendet werden.

## Windows

Die App soll nicht auf einen festen Windows-Benutzer oder einen festen Anbieterpfad festgelegt werden. Es wird der konkret gewaehlte lokale Datenordner verwendet.

## iPad

Das iPad arbeitet künftig über lokalen Netzwerk-Sync. Offene Altbestände werden kontrolliert importiert oder archiviert, nicht in die aktive Inbox gemischt.

Die weitere iPad-Anbindung ist nur konzeptionell vorbereitet. Der kuenftige iPad-Abgleich erfolgt ueber einen separaten lokalen Netzwerk-Sync. Details stehen in [IPAD_SYNC_KONZEPT.md](IPAD_SYNC_KONZEPT.md) und [LOCAL_NETWORK_SYNC.md](LOCAL_NETWORK_SYNC.md).
