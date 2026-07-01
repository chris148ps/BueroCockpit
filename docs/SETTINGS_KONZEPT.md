# BüroCockpit Settings-Konzept

Stand: 2026-07-02.

Grundregel: `settings.local.json` darf nur geraetespezifische Einstellungen enthalten. Zentrale betriebliche Einstellungen und fachliche Daten liegen unter `BueroCockpit_Daten/` und fuer die iPad-/Live-Leseschicht unter `BueroCockpit_Daten/Sync/live/`.

`AppProjekte` ist nur Quellcode. GitHub ist nur Quellcode, Dokumentation und Releases, nicht Produktivdaten. iCloud ist keine aktive Hauptdatenquelle mehr.

| Einstellung / Datenart | aktueller Speicherort | lokal oder zentral | fachliche Bewertung | Handlungsbedarf | Begründung |
|---|---|---|---|---|---|
| OneDriveEditDirectory | `BueroCockpitLocal/settings.local.json` | lokal | Darf lokal bleiben | keiner | Der konkrete Pfad zum gemeinsamen Datenordner ist geraete- und Benutzerkonto-spezifisch. |
| IpadLiveFileTargetPath | `BueroCockpitLocal/settings.local.json` | lokal | Legacy / Altlast | als Uebergang belassen, nicht ausbauen | Frei waehlbarer Zielordner fuer `live.bclive`; aktive Hauptstruktur ist `BueroCockpit_Daten/Sync/`. |
| UpdateFeedUrl | `BueroCockpitLocal/settings.local.json` | lokal | Darf lokal bleiben | keiner, solange nur Test-/Sonderkanal | Leer nutzt den Standardkanal aus `UpdateService`; lokale Overrides sind Entwickler-/Diagnosekonfiguration. |
| AppearanceMode | `BueroCockpitLocal/settings.local.json` | lokal | Darf lokal bleiben | keiner | Reine UI-Praeferenz pro Geraet. |
| TechnicianNames lokal | `BueroCockpitLocal/settings.local.json` oder alter `settings.json` | lokal | Legacy / Fallback | nicht mehr aktiv pflegen | Wird nur noch als Fallback gelesen, wenn zentrale Live-Settings leer sind. |
| technicianNames zentral | `BueroCockpit_Daten/Sync/live/settings.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Gemeinsame Monteur-/Technikerliste muss auf Windows, MacBook und Mac mini identisch sein. |
| Kategorien | `BueroCockpit_Daten/buerocockpit.db`, Export nach `Sync/live/categories.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Kategorien sind fachliche Struktur und werden von allen Geraeten geteilt. |
| Aufträge | `BueroCockpit_Daten/buerocockpit.db`, Export nach `Sync/live/tasks.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Auftragsdaten sind Produktivdaten und gehoeren nicht in lokale AppSettings. |
| Monteur-Zuordnung in Aufträgen | `Tasks.Technician` in `BueroCockpit_Daten/buerocockpit.db`, Export nach `Sync/live/tasks.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Die Zuordnung ist Teil des Auftrags und muss zentral erhalten bleiben. |
| Snapshot-/iPad-Exportdaten | `BueroCockpit_Daten/Sync/live/`, `Sync/live.bclive`, `Sync/snapshots/latest.bcsnapshot` | zentraler Export | Sollte zentral verfuegbar sein | aktuell korrekt als Export | Die Dateien sind Leseschicht fuer iPad/Snapshot, keine lokale AppSetting-Quelle. |
| mobile Inbox / mobile Eingänge | gemeinsame `mobile-inbox`/`mobile-processed`-Ordner bzw. vorbereitete `Sync/inbox`-Struktur unter `BueroCockpit_Daten` | zentraler Eingang | Sollte zentral gespeichert werden | Konzept beibehalten | Mobile Eingänge sind fachliche Eingangsdaten und muessen im gemeinsamen Datenordner liegen. |
| iCloud-Live-Pfade | alte frei waehlbare Ordner, z. B. iCloud-Testordner | lokal/extern | Legacy / Altlast | nicht als aktive Quelle verwenden | iCloud ist keine aktive Hauptdatenquelle mehr. |
| BueroCockpit_Daten | OneDrive-Datenordner `BueroCockpit_Daten/` | zentral | Zentrale Datenquelle | aktuell korrekt | Enthält Datenbank, Aufgabenordner, Anhaenge, Backups und Sync-Struktur. |
| Sync/live/settings.json | `BueroCockpit_Daten/Sync/live/settings.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Gemeinsame Live-Settings, aktuell `technicianNames`. |
| Sync/live/tasks.json | `BueroCockpit_Daten/Sync/live/tasks.json` | zentraler Export | Sollte zentral verfuegbar sein | aktuell korrekt | Enthält iPad-lesbare Auftragsdaten aus der zentralen Datenbank. |
| Sync/live/categories.json | `BueroCockpit_Daten/Sync/live/categories.json` | zentraler Export | Sollte zentral verfuegbar sein | aktuell korrekt | Enthält iPad-lesbare Kategorien aus der zentralen Datenbank. |
| Sync/live/metadata.json | `BueroCockpit_Daten/Sync/live/metadata.json` | zentraler Export | Darf zentral liegen | aktuell korrekt | Beschreibt Exportformat, Zeit, App und Exportgeraet. |

## Audit-Ergebnis

Gefundene lokale AppSettings:

- `OneDriveEditDirectory`: korrekt lokal, weil jeder Rechner einen anderen lokalen OneDrive-Pfad haben kann.
- `IpadLiveFileTargetPath`: Legacy/Uebergang, weil die aktive Hauptquelle `BueroCockpit_Daten/Sync/` ist.
- `AppearanceMode`: korrekt lokal, weil es eine UI-Praeferenz ist.
- `UpdateFeedUrl`: korrekt lokal, solange es als lokaler Test-/Sonderkanal verstanden wird.
- `TechnicianNames`: Legacy/Fallback, nicht mehr fachlich fuehrend.

Gefundene zentrale Settings und Daten:

- `Sync/live/settings.json` mit `technicianNames`.
- `buerocockpit.db` im zentralen Datenordner mit Kategorien, Auftraegen, Materialien, Anhaengen, Schreibtischdaten und Monteur-Zuordnung.
- `Sync/live/tasks.json`, `categories.json`, `metadata.json` als iPad-/Live-Export.
- `Sync/live.bclive` und `Sync/snapshots/latest.bcsnapshot` als Paketexporte.
- mobile Eingangsordner unter der gemeinsamen Sync-Struktur.

Derzeit wurden keine weiteren globalen Standardwerte, Vorlagen oder fachlichen Sync-Konfigurationen gefunden, die nur lokal in `AppSettings` liegen. Sollten kuenftig globale Vorgaben hinzukommen, gehoeren sie nicht in `settings.local.json`, sondern in eine zentrale Datei unter `BueroCockpit_Daten/Sync/live/` oder in die zentrale Datenbank, je nach Schreibmodell.
