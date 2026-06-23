# Speicheroptimierter iPad-Sync

## Ausgangslage

Der bisherige automatische Export erzeugte bei jeder relevanten Änderung ein vollständiges
`latest.bcsnapshot`-ZIP einschließlich aller Originalanhänge. Bei mehreren hundert Megabyte
musste OneDrive dieselben großen Daten wiederholt übertragen und versionieren. Das machte den
laufenden Sync langsam und ließ den Speicherverbrauch unnötig wachsen.

## Live-Struktur

Der automatische, ereignisgesteuerte Export schreibt nun nach `Sync/live/`:

```text
Sync/
├── live/
│   ├── metadata.json
│   ├── categories.json
│   ├── tasks.json
│   ├── attachments-index.json
│   └── previews/<sha256>.jpg
└── snapshots/latest.bcsnapshot
```

Die kleinen JSON-Dateien werden vollständig ersetzt. Es gibt weiterhin keinen Watcher, kein
Polling und keine automatische Datenübernahme vom iPad.

## Anhänge und Vorschauen

- Bildanhänge werden als JPEG mit maximal 1600 Pixeln an der langen Seite und Qualität 74
  exportiert. Ein vorhandenes Preview mit demselben Hash wird wiederverwendet.
- Für PDFs wird über die vorhandene PDFtoImage-Logik nur Seite 1 gerendert und anschließend mit
  denselben JPEG-Grenzen gespeichert. Schlägt das Rendern fehl, bleibt `previewAvailable=false`.
- Originale werden im Live-Sync grundsätzlich nicht kopiert. Der Index setzt
  `originalAvailableInLiveSync=false`, `originalDownloadMode=onDemandPlanned` und erklärt den
  Grund. Der Originalpfad im Büro-Datenordner bleibt unangetastet.

## Hash-Deduplizierung und inkrementelles Verhalten

Der vorhandene `ContentHash` wird verwendet. Fehlt er, berechnet der Export SHA-256. Derselbe
Dateiinhalt verweist auf dasselbe Preview. Existiert das Preview bereits, wird es nicht erneut
erzeugt. Das Exportlog nennt neue und übersprungene Vorschauen, entfernte Alt-Originale, Dauer
und Gesamtgröße des Live-Ordners.

## Bereinigung

Nach einem erfolgreichen Live-Export werden nicht mehr referenzierte Vorschauen entfernt.
Außerdem werden beim nächsten Sync alle früher automatisch kopierten Originale ausschließlich
unter `Sync/live/attachments` entfernt. Verknüpfte Verzeichnisse werden dabei aus
Sicherheitsgründen übersprungen. Dateien im produktiven Büro-Datenordner werden niemals
bereinigt. Erfolgreiche Exporte entfernen außerdem übrig gebliebene
`latest.bcsnapshot*.tmp`-Dateien im Snapshot-Ordner.

## Vollsnapshot

Der große Vollsnapshot bleibt für manuelle Übergaben und Backups erhalten. Er wird nur noch über
„iPad-Snapshot jetzt aktualisieren“ erzeugt; normale Repository-Speicherereignisse aktualisieren
ausschließlich `Sync/live`. Perspektivisch kann der manuelle Vollsnapshot in eine ausdrücklich
benannte Backup-Funktion verschoben werden. Die Umschaltung liegt in
`MainWindow.RefreshIpadSnapshot_OnClick`, das `ExportNowAsync` aufruft; der automatische Pfad ruft
`ExportLiveNowAsync` auf.

## iPad-Reader

Der Reader bleibt vollständig lesend. Bei einer Ordnerauswahl sucht er bevorzugt
`Sync/live` beziehungsweise `live` und fällt danach auf die bisherige Snapshot-Ordnerstruktur
zurück. Eine im Index referenzierte Vorschau kann direkt angezeigt werden. Die zusätzlichen
Indexfelder bereiten einen späteren On-Demand-Ablauf vor: Das iPad soll das Original erst beim
Antippen laden, anzeigen und den temporären Download anschließend wieder entfernen. Ein
Rückkanal oder Downloaddienst ist in diesem Schritt bewusst nicht enthalten. Es gibt keinen
Watcher und kein Polling; Aktualisierungen werden weiterhin nur durch eine bewusste Auswahl
beziehungsweise einen Import eingelesen.
