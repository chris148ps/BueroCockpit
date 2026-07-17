# BüroCockpit Settings-Konzept

Stand: 2026-07-15.

Grundregel: `settings.local.json` darf nur geraetespezifische Einstellungen enthalten. Fachliche Daten liegen lokal unter `BueroCockpit_Daten/`. iPad und Desktop werden kuenftig nur ueber den lokalen Netzwerk-Sync verbunden.

`AppProjekte` ist nur Quellcode. GitHub ist nur Quellcode, Dokumentation und Releases, nicht Produktivdaten. Fruehere externe Dateiablagen sind keine aktive Zielarchitektur mehr.

| Einstellung / Datenart | aktueller Speicherort | lokal oder zentral | fachliche Bewertung | Handlungsbedarf | Begründung |
|---|---|---|---|---|---|
| OneDriveEditDirectory | `BueroCockpitLocal/settings.local.json` | lokal | Legacy-Name fuer lokalen Datenordner | intern tolerant belassen | Der Feldname ist historisch; fachlich beschreibt er den lokal gewaehlten Datenordner. |
| IpadLiveFileTargetPath | `BueroCockpitLocal/settings.local.json` | lokal | Legacy / Toleranz | nicht mehr im aktuellen Bedienweg verwenden | Alter Zielpfad aus frueherer dateibasierter iPad-Leseschicht; darf in vorhandenen lokalen Settings stehen bleiben. |
| UpdateFeedUrl | `BueroCockpitLocal/settings.local.json` | lokal | Darf lokal bleiben | keiner, solange nur Test-/Sonderkanal | Leer nutzt den Standardkanal aus `UpdateService`; lokale Overrides sind Entwickler-/Diagnosekonfiguration. |
| AppearanceMode | `BueroCockpitLocal/settings.local.json` | lokal | Darf lokal bleiben | keiner | Reine UI-Praeferenz pro Geraet. |
| LocalNetworkSyncEnabled | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | standardmaessig `false`, kein automatischer Start | Aktivierung darf nur fuer dieses Geraet gelten und startet den Dienst nicht automatisch. |
| LocalNetworkSyncPort | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | `0`/leer/ungueltig wird lokal auf Default-Testport `53941` gesetzt | Portwahl ist geraete- und netzwerkabhaengig und darf nicht zentral verteilt werden. |
| LocalNetworkSyncDeviceName | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | optional | Anzeigename fuer spaeteres Pairing im lokalen Firmennetz. |
| LocalNetworkSyncDeviceId | `BueroCockpitLocal/settings.local.json` | lokal | Vorbereitung / lokal | automatisch erzeugt | Stabile lokale Kennung fuer spaetere Wiedererkennung gekoppelter Geraete. |
| LocalNetworkSyncPairingCode | `BueroCockpitLocal/settings.local.json` | lokal | Legacy / Toleranz | nicht mehr erzeugen oder anzeigen | Alter Wert aus der Pairing-Code-Vorbereitung; darf in vorhandenen lokalen Settings stehen bleiben und wird im aktuellen lokalen Netzwerk-Sync-Bedienweg ignoriert. |
| LocalNetworkSyncPairedDevices | `BueroCockpitLocal/settings.local.json` | lokal | Legacy / Toleranz | nicht im aktuellen Bedienweg verwenden | Alte Liste aus der Pairing-Code-Vorbereitung; darf in vorhandenen lokalen Settings stehen bleiben und wird im aktuellen lokalen Netzwerk-Sync-Bedienweg ignoriert. |
| TechnicianNames lokal | `BueroCockpitLocal/settings.local.json` oder alter `settings.json` | lokal | Legacy / Fallback | nicht mehr aktiv pflegen | Wird nur noch als Fallback gelesen, wenn zentrale Live-Settings leer sind. |
| technicianNames zentral | `BueroCockpit_Daten/Sync/live/settings.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Gemeinsame Monteur-/Technikerliste muss auf Windows, MacBook und Mac mini identisch sein. |
| Kategorien und Statuszuordnungen | Tabellen `Categories` und `WorkflowCategoryMappings` in `BueroCockpit_Daten/buerocockpit.db`; Kategorienexport nach `Sync/live/categories.json` | zentral | korrekt zentral gespeichert | umgesetzt | Normale Kategorien bleiben frei benutzerdefiniert. Statuszuordnungen verweisen ausschließlich auf stabile Kategorie-IDs. Variante A verbietet eine automatische Migration unveränderter Produktivdaten. |
| Aufträge | `BueroCockpit_Daten/buerocockpit.db`, Export nach `Sync/live/tasks.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Auftragsdaten sind Produktivdaten und gehoeren nicht in lokale AppSettings. |
| Monteur-Zuordnung in Aufträgen | `Tasks.Technician` in `BueroCockpit_Daten/buerocockpit.db`, Export nach `Sync/live/tasks.json` | zentral | Sollte zentral gespeichert werden | aktuell korrekt | Die Zuordnung ist Teil des Auftrags und muss zentral erhalten bleiben. |
| Alte iPad-Exportdaten | vorhandene lokale Exportdateien | Legacy / Altbestand | nicht als aktuelle Zielarchitektur verwenden | tolerant lesen, falls fuer vorhandene Anzeige noetig | Der neue Zielweg ist lokaler Netzwerk-Sync. |
| mobile Inbox / mobile Eingänge | vorbereitete lokale `Sync/inbox`-Struktur unter `BueroCockpit_Daten` | lokaler Eingang | Sollte lokal gespeichert werden | Konzept beibehalten | Mobile Eingänge sind fachliche Eingangsdaten und werden spaeter ueber lokalen Netzwerk-Sync uebernommen. |
| BueroCockpit_Daten | lokaler Datenordner `BueroCockpit_Daten/` | lokal | Lokale Datenquelle | aktuell korrekt | Enthält Datenbank, Aufgabenordner, Anhaenge, Backups und vorbereitete Sync-Struktur. |

## Audit-Ergebnis

Gefundene lokale AppSettings:

- `OneDriveEditDirectory`: historischer Feldname, aktuell tolerant als lokal gewaehlter Datenordner zu behandeln.
- `IpadLiveFileTargetPath`: Legacy/Toleranz. Nicht weiter als Hauptloesung ausbauen; spaetere Abloesung durch lokalen Netzwerk-Sync.
- `AppearanceMode`: korrekt lokal, weil es eine UI-Praeferenz ist.
- `UpdateFeedUrl`: korrekt lokal, solange es als lokaler Test-/Sonderkanal verstanden wird.
- `LocalNetworkSyncEnabled`, `LocalNetworkSyncPort`, `LocalNetworkSyncDeviceName`, `LocalNetworkSyncDeviceId`: korrekt lokal, weil der Netzwerk-Sync-Dienst pro Rechner bewusst gestartet und konfiguriert werden muss. `LocalNetworkSyncPort` und `LocalNetworkSyncDeviceName` sind in den Desktop-Einstellungen bearbeitbar. Alte Felder wie `LocalNetworkSyncPairingCode` und `LocalNetworkSyncPairedDevices` bleiben nur als Legacy/Toleranz lokal lesbar. Alle Werte werden ausschliesslich in `BueroCockpitLocal/settings.local.json` gespeichert. Standard ist deaktiviert; daraus folgt kein automatischer Serverstart.
- Pairing-Geheimnisse, TrustKeys und SharedSecrets: muessen lokal bleiben und duerfen nicht zentral gespeichert werden.
- Der aktuelle lokale Netzwerk-Sync-Bedienweg ist in `docs/LOCAL_NETWORK_PAIRING.md` beschrieben; alte Code-Felder bleiben nur tolerant lesbar.
- `TechnicianNames`: Legacy/Fallback, nicht mehr fachlich fuehrend.

Gefundene zentrale Settings und Daten:

- `buerocockpit.db` im lokalen Datenordner mit Kategorien, zentralen
  Statuszuordnungen, Auftraegen, Materialien, Anhaengen, Schreibtischdaten und
  Monteur-Zuordnung.
- mobile Eingangsordner unter der gemeinsamen Sync-Struktur.

Für die weitere Umsetzung gilt `docs/ARBEITSKATEGORIEN.md`. Bestehende
`CategoryId`-/`CategoryIds`-Werte sind bis zur kontrollierten Umstellung
tolerant zu lesen. Für neue oder bewusst geänderte Vorgänge darf nur noch die
eine aktuelle normale Kategorie fortgeschrieben werden.

Derzeit wurden keine weiteren globalen Standardwerte, Vorlagen oder fachlichen Sync-Konfigurationen gefunden, die nur lokal in `AppSettings` liegen. Sollten kuenftig globale Vorgaben hinzukommen, gehoeren sie in die lokale Datenbank oder in ein bewusst definiertes lokales Netzwerk-Sync-Format, nicht in alte Datei-Kopplungen.
