# iPad Snapshot Read-Only Vorbereitung

Stand: 2026-06-20

> Historischer Vorbereitungsstand: Diese Datei beschreibt ein altes
> Snapshot-Format und ist keine aktuelle Fachregel. Für Vorgangstyp,
> Workflowstatus, aktuelle normale Kategorie und Statuszuordnungen gilt ausschließlich
> `docs/ARBEITSKATEGORIEN.md`.

## Aktueller Status im Repository

- Es gibt derzeit kein separates iPad-, iOS-, SwiftUI-, MAUI- oder anderes Mobile-Projekt im Repository.
- Vorhanden ist die bestehende Avalonia-Desktop-App mit dem Snapshot-Exportservice `Services/IpadSnapshotExportService.cs`.
- Der Export schreibt ausschließlich lesbare Snapshot-Dateien nach `Sync/snapshots/` im gemeinsam gewählten OneDrive-Arbeitsordner.
- Die Desktop-App bleibt unverändert; diese Datei dient nur als technische Vorbereitung für eine spätere iPad-Leseversion.

## Snapshot-Ordner

Der Exportservice erzeugt folgende Struktur unter dem ausgewählten OneDrive-Arbeitsordner:

- `Sync/snapshots/metadata.json`
- `Sync/snapshots/categories.json`
- `Sync/snapshots/tasks.json`
- `Sync/snapshots/attachments-index.json`

## JSON-Format

Der Export verwendet `System.Text.Json` mit `PropertyNamingPolicy = JsonNamingPolicy.CamelCase`.
Damit heißen die Felder im JSON klein geschrieben im camelCase-Format.

### `metadata.json`

Felder:

- `formatVersion`
- `exportedAt`
- `appName`
- `appVersion`
- `deviceName`
- `source`

Semantik:

- `formatVersion` ist aktuell `1`.
- `exportedAt` ist der Export-Zeitpunkt als `DateTimeOffset`.
- `appName` ist `BueroCockpit`.
- `appVersion` ist optional.
- `deviceName` ist optional.
- `source` ist aktuell `BueroCockpit`.

### `categories.json`

Felder pro Kategorie:

- `id`
- `name`
- `order`

Hinweis:

- Die Kategorien werden aus der lokalen Kategorie-Reihenfolge exportiert.
- Im Repository werden Kategorien über `SortOrder` und `Name` geordnet.

### `tasks.json`

Felder pro Aufgabe:

- `id`
- `title`
- `customerName`
- `categoryIds`
- `categoryNames`
- `dueDate`
- `reminderDate`
- `createdAt`
- `updatedAt`
- `materialOrderedAt`
- `status`
- `notes`
- `shortText`
- `attachmentRefs`

Hinweis:

- `categoryIds` enthält historische Kategorie-IDs der Aufgabe. Das Feld darf
  in einer späteren Anpassung nicht als mehrere gleichzeitig sichtbare normale Kategorien
  interpretiert werden.
- `categoryNames` enthält die zugehörigen historischen lesbaren Namen. Eine
  neue Darstellung muss genau eine aktuelle Kategorie eindeutig führen.
- `notes` ist die ausführliche Beschreibung, falls vorhanden.
- `shortText` ist eine gekürzte Textversion der Beschreibung.
- `attachmentRefs` enthält die Anhang-IDs.

### `attachments-index.json`

Felder pro Anhang:

- `id`
- `taskId`
- `fileName`
- `relativePath`
- `isImportant`
- `fileExists`

Hinweis:

- Die Datei ist nur ein Index.
- Das iPad soll diese Daten zunächst nur anzeigen, nicht öffnen oder schreiben.

## Leseregeln für eine spätere iPad-App

- Nur lesen, kein Rückschreiben.
- Keine direkte Datenbanknutzung auf dem iPad.
- Kein LiveReload.
- Kein `FileSystemWatcher`.
- Kein Polling.
- Keine automatische Hintergrundüberwachung.
- Keine automatische Datenneuladung.
- Keine Meldung über Änderungen durch andere Geräte.
- Fehlende Dateien oder ungültiges JSON müssen als normaler Fehlerzustand behandelt werden, nicht als Absturz.
- Fehlende Felder sollen tolerant ignoriert werden.

## Vorgeschlagene Modellnamen für eine spätere iPad-App

Die folgende Benennung ist bewusst simpel und stabil gehalten:

- `SnapshotMetadata`
- `SnapshotCategory`
- `SnapshotTask`
- `SnapshotAttachmentIndex`
- `SnapshotReader`
- `SnapshotBrowserViewModel`
- `SnapshotCategoryListViewModel`
- `SnapshotTaskDetailViewModel`
- `SnapshotLoadState`
- `SnapshotErrorState`

## Vorgeschlagene Dateinamen für eine spätere iPad-App

Wenn ein separates iPad-Projekt angelegt wird, bietet sich folgende Grundstruktur an:

- `Models/SnapshotMetadata.swift`
- `Models/SnapshotCategory.swift`
- `Models/SnapshotTask.swift`
- `Models/SnapshotAttachmentIndex.swift`
- `Services/SnapshotReader.swift`
- `ViewModels/SnapshotBrowserViewModel.swift`
- `Views/SnapshotRootView.swift`
- `Views/SnapshotCategoryListView.swift`
- `Views/SnapshotTaskDetailView.swift`
- `Views/SnapshotEmptyStateView.swift`
- `Views/SnapshotErrorView.swift`

## Nächster konkreter Umsetzungsschritt

1. Ein separates iPad-Projekt anlegen, sobald die Plattform entschieden ist.
2. Den `SnapshotReader` zuerst nur auf `metadata.json`, `categories.json` und `tasks.json` ausrichten.
3. Danach eine reine Leseansicht mit Kategorie-Liste, Aufgabenliste und Detailansicht bauen.
4. Anhänge zunächst nur als Metadaten anzeigen.
5. Erst danach über eine mögliche Öffnen-/Vorschau-Funktion nachdenken.
