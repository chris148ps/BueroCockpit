# BüroCockpit Settings-Konzept

Stand: 2026-07-20.

Grundregel: `settings.local.json` darf nur geraetespezifische Einstellungen
enthalten. Fachliche Daten liegen auf jedem Desktop ausschließlich im lokalen
Standardordner des Betriebssystems. Desktop-Geräte tauschen vollständige
Datenstände nur über manuell exportierte und importierte Backup-Archive aus.
iPad und Desktop werden nur ueber den lokalen Netzwerk-Sync verbunden.

`AppProjekte` ist nur Quellcode. GitHub ist nur Quellcode, Dokumentation und Releases, nicht Produktivdaten. Fruehere externe Dateiablagen sind keine aktive Zielarchitektur mehr.

| Einstellung / Datenart | aktueller Speicherort | lokal oder zentral | fachliche Bewertung | Handlungsbedarf | Begründung |
|---|---|---|---|---|---|
| BackupExchangeDirectory | Windows `%LOCALAPPDATA%\BueroCockpit\settings.local.json`; macOS `~/Library/Application Support/BueroCockpitLocal/settings.local.json` | lokal | Austauschordner fuer geschlossene Backup-ZIP-Archive | auswählbar, niemals als Datenordner verwenden | Der Ordner ist geraetespezifisch und kann auf OneDrive liegen; dort wird keine laufende Datenbank geöffnet. |
| UpdateFeedUrl | `BueroCockpitLocal/settings.local.json` | lokal | Darf lokal bleiben | keiner, solange nur Test-/Sonderkanal | Leer nutzt den Standardkanal aus `UpdateService`; lokale Overrides sind Entwickler-/Diagnosekonfiguration. |
| AppearanceMode | `BueroCockpitLocal/settings.local.json` | lokal | Darf lokal bleiben | keiner | Reine UI-Praeferenz pro Geraet. |
| LocalNetworkSyncEnabled | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | standardmaessig `false`, kein automatischer Start | Aktivierung darf nur fuer dieses Geraet gelten und startet den Dienst nicht automatisch. |
| LocalNetworkSyncPort | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | `0`/leer/ungueltig wird lokal auf Default-Testport `53941` gesetzt | Portwahl ist geraete- und netzwerkabhaengig und darf nicht zentral verteilt werden. |
| LocalNetworkSyncDeviceName | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | optional | Anzeigename fuer spaeteres Pairing im lokalen Firmennetz. |
| LocalNetworkSyncDeviceId | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | automatisch erzeugt | Stabile lokale Kennung fuer spaetere Wiedererkennung gekoppelter Geraete. |
| LocalNetworkSyncPairingCode | `BueroCockpitLocal/settings.local.json` | lokal | Legacy / Toleranz | nicht mehr erzeugen oder anzeigen | Alter Wert aus der Pairing-Code-Vorbereitung; darf in vorhandenen lokalen Settings stehen bleiben und wird im aktuellen lokalen Netzwerk-Sync-Bedienweg ignoriert. |
| LocalNetworkSyncPairedDevices | `BueroCockpitLocal/settings.local.json` | lokal | Legacy / Toleranz | nicht im aktuellen Bedienweg verwenden | Alte Liste aus der Pairing-Code-Vorbereitung; darf in vorhandenen lokalen Settings stehen bleiben und wird im aktuellen lokalen Netzwerk-Sync-Bedienweg ignoriert. |
| TechnicianNames lokal | `BueroCockpitLocal/settings.local.json` oder alter `settings.json` | lokal | Legacy / Fallback | nicht mehr aktiv pflegen | Wird nur noch als Fallback gelesen, wenn zentrale Live-Settings leer sind. |
| technicianNames im Datenstand | `<lokaler Standardordner>/Sync/live/settings.json` | lokal im vollständigen Datenstand | Bestandteil von Export und Import | aktuell korrekt | Die Liste wird zusammen mit dem vollständigen Backup-Datenstand zwischen Desktop-Geräten übertragen. |
| Kategorien und Statuszuordnungen | Tabellen `Categories` und `WorkflowCategoryMappings` in `BueroCockpit_Daten/buerocockpit.db`; Kategorienexport nach `Sync/live/categories.json` | zentral | korrekt zentral gespeichert | umgesetzt | Normale Kategorien bleiben frei benutzerdefiniert. Statuszuordnungen verweisen ausschließlich auf stabile Kategorie-IDs. Variante A verbietet eine automatische Migration unveränderter Produktivdaten. |
| Aufträge | `BueroCockpit_Daten/buerocockpit.db`, Export nach `Sync/live/tasks.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Auftragsdaten sind Produktivdaten und gehoeren nicht in lokale AppSettings. |
| Monteur-Zuordnung in Aufträgen | `Tasks.Technician` in `BueroCockpit_Daten/buerocockpit.db`, Export nach `Sync/live/tasks.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Die Zuordnung ist Teil des Auftrags und muss zentral erhalten bleiben. |
| Alte iPad-Exportdaten | vorhandene lokale Exportdateien | Legacy / Altbestand | nicht als aktuelle Zielarchitektur verwenden | tolerant lesen, falls fuer vorhandene Anzeige noetig | Der neue Zielweg ist lokaler Netzwerk-Sync. |
| mobile Inbox / mobile Eingänge | vorbereitete lokale `Sync/inbox`-Struktur unter `BueroCockpit_Daten` | lokaler Eingang | Sollte lokal gespeichert werden | Konzept beibehalten | Mobile Eingänge sind fachliche Eingangsdaten und werden spaeter ueber lokalen Netzwerk-Sync uebernommen. |
| Produktiver Datenordner | Windows `%LOCALAPPDATA%\BueroCockpit`, macOS `~/Library/Application Support/BueroCockpit` | lokal | einzige produktive Datenquelle | verbindlich | Enthält Datenbank, Aufgabenordner, Anhaenge, lokale Backups und vorbereitete Sync-Struktur. |

## Audit-Ergebnis

Gefundene lokale AppSettings:

- `OneDriveEditDirectory` und `IpadLiveFileTargetPath` sind aus dem aktiven
  Einstellungsmodell entfernt. Alte JSON-Eigenschaften werden beim Lesen als
  unbekannt ignoriert und können keinen Pfad mehr bestimmen.
- `BackupExchangeDirectory`: neuer rein lokaler Verweis auf den
  Backup-Austauschordner. Der Zielordner darf cloud-synchronisiert sein, enthält
  aber ausschließlich geschlossene Archive.
- Backup-Abstammung und Verlauf liegen unter Windows neben
  `settings.local.json` in `%LOCALAPPDATA%\BueroCockpit` und unter macOS in
  `~/Library/Application Support/BueroCockpitLocal`; beide bleiben
  gerätespezifisch und werden nicht in Austausch-Backups aufgenommen.
- `AppearanceMode`: korrekt lokal, weil es eine UI-Praeferenz ist.
- `UpdateFeedUrl`: korrekt lokal, solange es als lokaler Test-/Sonderkanal verstanden wird.
- `LocalNetworkSyncEnabled`, `LocalNetworkSyncPort`, `LocalNetworkSyncDeviceName`, `LocalNetworkSyncDeviceId`: korrekt lokal, weil der Netzwerk-Sync-Dienst pro Rechner bewusst gestartet und konfiguriert werden muss. `LocalNetworkSyncPort` und `LocalNetworkSyncDeviceName` sind in den Desktop-Einstellungen bearbeitbar. Alte Felder wie `LocalNetworkSyncPairingCode` und `LocalNetworkSyncPairedDevices` bleiben nur als Legacy/Toleranz lokal lesbar. Alle Werte werden ausschliesslich in `BueroCockpitLocal/settings.local.json` gespeichert. Standard ist deaktiviert; daraus folgt kein automatischer Serverstart.
- Pairing-Geheimnisse, TrustKeys und SharedSecrets: muessen lokal bleiben und duerfen nicht zentral gespeichert werden.
- Der aktuelle lokale Netzwerk-Sync-Bedienweg ist in `docs/LOCAL_NETWORK_PAIRING.md` beschrieben; alte Code-Felder bleiben nur tolerant lesbar.
- `TechnicianNames`: Legacy/Fallback, nicht mehr fachlich fuehrend.

Gefundene zentrale Settings und Daten:

- `buerocockpit.db` im lokalen Standarddatenordner mit Kategorien,
  Statuszuordnungen, Auftraegen, Materialien, Anhaengen, Schreibtischdaten und
  Monteur-Zuordnung.
- mobile Eingangsordner unter der lokalen Sync-Struktur.

Für die weitere Umsetzung gilt `docs/ARBEITSKATEGORIEN.md`. Bestehende
`CategoryId`-/`CategoryIds`-Werte sind bis zur kontrollierten Umstellung
tolerant zu lesen. Für neue oder bewusst geänderte Vorgänge darf nur noch die
eine aktuelle normale Kategorie fortgeschrieben werden.

Derzeit wurden keine weiteren globalen Standardwerte, Vorlagen oder fachlichen Sync-Konfigurationen gefunden, die nur lokal in `AppSettings` liegen. Sollten kuenftig globale Vorgaben hinzukommen, gehoeren sie in die lokale Datenbank oder in ein bewusst definiertes lokales Netzwerk-Sync-Format, nicht in alte Datei-Kopplungen.
