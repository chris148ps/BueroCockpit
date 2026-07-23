# Codex-Journal: Wiedervorlage, Kategorien und iPad-Sync

## Auftrag und Ausgangsstand

Der Auftrag umfasste drei Teile: einen optionalen Wiedervorlagegrund mit
korrekter Auftragsterminmarkierung, rekursive Oberkategorien und einen sicheren
Ausbau der iPad-App samt manuellem lokalem Sync. Ausgangspunkt war
`origin/main` bei `73b67e9`; gearbeitet wurde ohne Commit auf `codex/work`.

## Regelprüfung

Die Pflicht- und Fachdateien wurden vor Änderungen geprüft. Der neue Auftrag
ist die ausdrückliche Erweiterungsfreigabe, die der bisherige lokale
Sync-Stand verlangte. Unverändert blieben:

- Desktop-Dienst startet nur manuell.
- Nur `Jetzt synchronisieren` überträgt Daten.
- Bonjour sucht Geräte, startet aber keine Übertragung.
- Ein neues Gerät muss am Desktop freigegeben werden.
- Kein Cloudtransport und keine vollständige Datenbankkopie.
- Keine stille Desktopüberschreibung oder Produktivmigration.

## Teil 1

`FollowUpReason` wurde als einheitliches optionales Feld in Modell, SQLite,
Speicherpfad, Clone/Undo, Duplikat und Snapshot-DTO ergänzt. Die Migration
verwendet `ALTER TABLE` über den vorhandenen additiven
`AddColumnIfMissing`-Pfad.

Die Wiedervorlagenkarte verwendet für die hervorgehobene Zeile ausschließlich
`DueDate`: heute, morgen, naher Wochentag oder abgekürzter Wochentag mit Datum.
Das Wiedervorlagedatum bleibt eine eigene neutrale Zeile. Einzelkarten haben
nur neutrale Rahmen; der äußere Wiedervorlagenbereich und der Zähler dürfen
dezent signalisieren.

## Teil 2

`CategoryHierarchyFilter` ermittelt iterativ und zyklussicher die eigene
Kategorie-ID und alle rekursiven Nachfolger in einem
case-insensitiven `HashSet`. Derselbe Matcher wird für Aufgabenliste, Suche und
Zähler verwendet. Mehrfachzuordnungen aus tolerierten Altbeständen führen
nicht zu doppelten Listeneinträgen oder Zählungen. Gelöschte und archivierte
Vorgänge werden für normale Kategorien ausgeschlossen.

## Teil 3

Der bestehende reale Stand umfasste bereits Bonjour, Pairing, mobile
Offline-Eingänge, Fotos, Markup, PencilKit, Dateien, Mobile-Inbox-Upload,
Idempotenz und Paketkonflikterhalt. Ergänzt wurden:

- `GET /local-sync/snapshot` mit denselben Authentisierungsheadern,
- Zugriff erst nach `trusted`-Prüfung,
- temporärer plattformneutraler Snapshot statt SQLite-Kopie,
- versionierte Antwortheader,
- Streaming und temporäre Bereinigung,
- iPad-Download innerhalb des bestehenden manuellen Sync-Ablaufs,
- Validierung vor atomarer lokaler Installation,
- Erhalt lokaler mobiler Eingänge,
- sichtbare Empfangszahl,
- Adresse, Monteur und Wiedervorlagegrund im Snapshotmodell und Detail.

Direkte Upserts bestehender Desktopaufträge wurden nicht aktiviert. Ohne
gemeinsame Revision und Basisversion wären Konflikte nicht sicher lösbar.
`docs/IPAD_FUNKTIONSMATRIX.md` dokumentiert deshalb Stufe A als implementiert
und die Teilstände aus B bis D ohne Platzhalterbehauptungen.

## Prüfungen

Erfolgreich:

- Workflow-/Kategorie-Integrationstest mit temporärer SQLite-Datenbank,
- additive Migration nach testweisem Entfernen von `FollowUpReason`,
- Grund speichern, vollständig entfernen und exportieren,
- Auftragstermin heute, morgen und überfällig,
- rekursive Kategoriehierarchie über drei Ebenen,
- realer Snapshot-Export,
- HTTP 403 ohne Pairing und unveränderter Snapshot mit Pairing,
- Mobile-Inbox-Upload, Wiederholung, Prüfsummenfehler, Konflikt und
  Wiederaufnahme,
- `dotnet build`,
- `dotnet build -r osx-arm64`,
- `dotnet build -r win-x64`,
- iOS-Simulator-Build ohne Codesignatur.

Nicht ausgeführt beziehungsweise nicht bestanden:

- physischer iPad-/Windows-Zielgerätetest,
- sichtbare Hell-/Dunkel-Abnahme des neuen Wiedervorlagenlayouts,
- echte Bonjour-Suche und kompletter neuer Download auf einem physischen iPad,
- Bearbeitung eines vorhandenen Desktopauftrags auf dem iPad, weil diese
  Funktion noch nicht implementiert ist.

Beim versuchten UI-Smoke-Test startete die Computer-Use-Appauflösung das alte
macOS-Bundle ohne die isolierte Testumgebung. Der Prozess wurde sofort beendet.
Die produktive Datenbank blieb nach read-only Prüfung unverändert; nur die
Sperrdatei im zentralen Ordner erhielt einen neuen Zeitstempel und wurde wegen
des Verbots von Cloud-Dateiänderungen nicht eigenmächtig entfernt.

## Git

Kein Commit, Push, Merge, Pull Request, Release, Tag oder Versionswechsel.
