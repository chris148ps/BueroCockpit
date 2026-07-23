# BüroCockpit Datenordner

Die produktive Datenquelle für BüroCockpit liegt auf jedem Gerät
ausschließlich im lokalen Standardordner des Betriebssystems:

```text
Windows: %LOCALAPPDATA%\BueroCockpit
macOS:   ~/Library/Application Support/BueroCockpit
```

AppProjekte enthält nur Quellcode. GitHub enthält nur Quellcode, Release-Artefakte und Dokumentation, aber keine produktiven Daten, Anhänge, Backups oder Kundendaten.

Lokale AppSettings duerfen nur geraetespezifische Einstellungen enthalten. Betriebsrelevante Daten und gemeinsame Vorgaben gehoeren in den lokalen Datenordner.

## Aktive Struktur

Der aktive Arbeitsordner der Desktop-App ist immer der jeweilige lokale
Standardordner. Ein frei wählbarer produktiver Datenordner ist in dieser Stufe
nicht vorgesehen.

`storage-location.json` und `storage-location.local.json` werden nicht mehr
gelesen oder migriert. Die früheren Einstellungsfelder
`OneDriveEditDirectory` und `IpadLiveFileTargetPath` gehören nicht mehr zum
aktiven Modell; vorhandene JSON-Eigenschaften werden beim Lesen ignoriert und
bestimmen keinen Pfad. Ist der Standardordner selbst über einen Symlink oder eine
Verzeichnisverknüpfung umgeleitet, blockiert die App den Start vor dem Öffnen
der Datenbank und zeigt Sollpfad und erkanntes Ziel an.
Fehlt am echten lokalen Sollpfad eine Datenbank, übernimmt die App keine alte
Quelle. Sie bietet beim ersten Öffnen stattdessen sichtbar den bewussten Weg zu
`Backup von anderem Gerät einspielen` an; alternativ kann ausdrücklich leer
lokal begonnen werden.

Der aktive SyncRoot wird daraus abgeleitet:

```text
<lokaler Standardordner>/Sync/
```

Vorbereitete mobile Netzwerk-Eingaenge sollen kuenftig unter diesem SyncRoot landen:

```text
BueroCockpit_Daten/Sync/inbox/mobile-<yyyyMMdd-HHmmss>-<kurzid>/
BueroCockpit_Daten/Sync/processed/
BueroCockpit_Daten/Sync/conflicts/
```

Das ist nur die Zielablage fuer einen spaeteren manuellen lokalen Netzwerk-Sync. In diesem Stand wird kein Server gestartet und kein Upload angenommen. Bestehende manuelle Mobile-Inbox-Ordner `mobile-inbox`/`mobile-processed` bleiben Legacy/Kompatibilitaet und werden nicht verschoben.

Auftraege und Kategorien liegen fachlich im lokalen Datenordner. Die Desktop-App
speichert sie in `<lokaler Standardordner>/buerocockpit.db`. Ein iPad-Lesestand
wird ueber den lokalen Netzwerk-Sync definiert.

Die Monteur-Zuordnung an Auftraegen ist Bestandteil der Auftragsdaten und muss
lokal erhalten bleiben.

Lokale AppSettings bleiben fuer geraetespezifische Einstellungen wie
Darstellung, Backup-Austauschordner oder lokale Update-Testkanaele zustaendig.

## Austausch zwischen Desktop-Geräten

Windows, Mac mini und MacBook öffnen niemals gemeinsam dieselbe Datenbank.
Ein vollständiger Datenstand wird ausschließlich über bewusst exportierte und
importierte Backup-ZIP-Archive ausgetauscht. Der dafür lokal konfigurierte
OneDrive-Austauschordner enthält nur fertige Archive; keine laufende SQLite-
Datenbank wird dort geöffnet oder bearbeitet.

Der sichere Bedienablauf lautet:

1. Neuestes Backup auf dem nächsten Gerät importieren.
2. Nur auf diesem Gerät weiterarbeiten.
3. Nach Abschluss ein neues Backup für das nächste Gerät exportieren.

Backups ersetzen beim Import den vollständigen lokalen Datenstand. Es gibt
keine automatische Zusammenführung paralleler Änderungen.

### Austausch-Export

`Backup für anderes Gerät erstellen` schließt zunächst offene App-Änderungen
ab, erstellt die Datenbank über die SQLite-Backup-API und sammelt die
produktiven Dateien in einem lokalen Staging-Ordner. Lock-, WAL-, temporäre,
Log-, Debug-, Build-, Test- und alte Backup-Dateien werden ausgeschlossen.
Gerätelokale Dateien wie `settings.local.json`, Gerätefreigaben,
Sync-Checkpoints sowie Austauschzustand und -journal werden ebenfalls nicht
übertragen und beim Import auf dem Zielgerät erhalten.
`manifest.json` enthält Backup- und Parent-ID, Zeitpunkte, Gerät, Betriebssystem,
App- und Schema-Version, Datenbankgröße und -Hash sowie die vollständige
Dateiliste mit Größe und SHA-256. Erst das fertige lokale ZIP wird als
temporäre Datei in den Austauschordner kopiert und dort atomar auf seinen
endgültigen Namen umbenannt.

### Austausch-Import

Die App zeigt die Archive absteigend nach Erstellungszeit mit Gerät, Zeitpunkt,
App-Version, Backup-ID, Parent-ID und Datenbankgröße. Vor dem Ersetzen werden
ZIP-Pfade, Manifest, vollständige Dateiliste, Größen, SHA-256,
`PRAGMA integrity_check` und Schema-Version geprüft. Danach wird zwingend ein
vollständiges lokales Rückfall-ZIP erzeugt. Der geprüfte Stand wird im gleichen
lokalen Dateisystem bereitgestellt und über Verzeichnisumbenennung aktiviert;
bei einem Fehler wird der vorherige Ordner zurückgestellt.

Der lokale Abstammungszustand liegt unter Windows in
`%LOCALAPPDATA%\BueroCockpit\backup-exchange-state.local.json` und unter macOS
in `~/Library/Application Support/BueroCockpitLocal/backup-exchange-state.local.json`;
der Verlauf liegt jeweils daneben als
`backup-exchange-journal.local.jsonl`. Diese Gerätelokaldateien werden nicht
exportiert. Ein linearer Export verwendet die aktuelle Backup-ID als
`ParentBackupId`. Abweichende, lokal veränderte, ältere oder offenbar
veraltete Stände verlangen eine zweite eindeutige Bestätigung. Es findet keine
Zusammenführung statt.

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

Die App verwendet `%LOCALAPPDATA%\BueroCockpit`. Ein OneDrive- oder anderer
Cloudpfad darf nicht als produktiver Datenordner übernommen werden.

## iPad

Das iPad arbeitet künftig über lokalen Netzwerk-Sync. Offene Altbestände werden kontrolliert importiert oder archiviert, nicht in die aktive Inbox gemischt.

Die weitere iPad-Anbindung ist nur konzeptionell vorbereitet. Der kuenftige iPad-Abgleich erfolgt ueber einen separaten lokalen Netzwerk-Sync. Details stehen in [IPAD_SYNC_KONZEPT.md](IPAD_SYNC_KONZEPT.md) und [LOCAL_NETWORK_SYNC.md](LOCAL_NETWORK_SYNC.md).
