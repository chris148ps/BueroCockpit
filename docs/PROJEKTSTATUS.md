# Projektstatus BüroCockpit

## Verbindliches fachliches Zielbild

BüroCockpit unterscheidet dauerhaft:

1. genau einen Vorgangstyp,
2. genau einen Workflowstatus,
3. genau eine aktuell zugeordnete normale Kategorie.

Die normalen Kategorien in der linken Navigation sind benutzerdefiniert. Der
Endbenutzer darf sie frei anlegen, umbenennen, verschieben, verschachteln und
löschen, soweit keine System- oder Sicherheitsregel entgegensteht.

Als feste Navigation bleiben nur Übersicht, Alle Vorgänge, Papierkorb,
Einstellungen, das unter Einstellungen geführte Archiv und ein technisch
erforderlicher mobiler Eingang. Angebote, Aufträge, Material und Termine sind
normale frei verwaltete Kategorien und keine zusätzlichen Systemansichten.

Für jede zulässige Kombination aus Vorgangstyp und Workflowstatus kann der
Benutzer eine Zielkategorie konfigurieren. Die Zuordnung wird intern über die
stabile Kategorie-ID gespeichert und bleibt deshalb bei Umbenennung oder
Verschiebung der Kategorie erhalten.

Beim Statuswechsel wird der Vorgang automatisch in die konfigurierte
Zielkategorie verschoben. Ein Vorgang darf niemals gleichzeitig in mehreren
normalen Kategorien erscheinen.

Die verbindliche Fachlogik steht in `docs/ARBEITSKATEGORIEN.md`.

## Vorgangstypen

Beim Erstellen eines neuen Vorgangs muss zwischen folgenden Typen gewählt werden:

- Angebotsvorgang
- Direktauftrag

Empfohlene Anfangsstatus:

- Angebotsvorgang → `Angebot`
- Direktauftrag → `Auftrag`

Der Vorgangstyp muss später bewusst und nach Bestätigung änderbar sein. Daten
wie Kunde, Beschreibung, Anhänge, Material, Termine, Techniker und
Wiedervorlagen bleiben dabei erhalten.

## Automatische Statuszuordnungen

Die Kategorienamen sind nicht fest vorgeschrieben. Folgende Zuordnungen sind
nur Beispiele:

- Angebotsvorgang / Angebot → Angebote
- Angebotsvorgang / Angebot gesendet → Angebote / Gesendet
- Angebotsvorgang / Auftrag → Angebote / Beauftragt
- Direktauftrag / Auftrag → Aufträge
- Material → Material
- Termin → Termine
- Erledigt → Erledigt

Verbindlich ist immer die vom Benutzer gewählte stabile Kategorie-ID.

## Tatsächlicher Implementierungsstand

Die Desktop-App setzt die konfigurierbare Fachlogik um:

- `WorkflowType` und `WorkflowStep` bleiben getrennte Quellen für Vorgangstyp
  und Workflowstatus.
- `WorkflowCategoryMappings` speichert die zentrale Zielkategorie pro
  Kombination ausschließlich über die stabile Kategorie-ID.
- Unter `Einstellungen > Kategorien` sind sämtliche Angebots- und
  Direktauftragsstatus einzeln konfigurierbar; fehlende oder ausgeblendete
  Ziele werden sichtbar ungültig.
- Neue Vorgänge verlangen die Auswahl `Angebotsvorgang` oder `Direktauftrag`
  und werden ohne gültige Anfangszuordnung nicht angelegt.
- Die nachträgliche Typänderung verlangt eine Bestätigung und bei
  inkompatiblem Status eine ausdrückliche Statusauswahl.
- Ein Statuswechsel übernimmt genau eine konfigurierte Kategorie. Ohne gültige
  Zuordnung wird die Änderung blockiert und auf die Einstellungen verwiesen.
- Manuelle Kategorieauswahl und Drag & Drop ändern ausschließlich die aktuelle
  Kategorie; Haupt- und Unterkategorien sind gleichermaßen auswählbar.
- Das Löschen einer verwendeten Kategorie verlangt eine ausdrückliche
  Ersatz-/Entfernungsentscheidung oder Abbruch; Vorgänge werden nicht still
  verschoben.
- Neue und bewusst geänderte Vorgänge schreiben genau eine Kategorie fort.
  Unveränderte Legacy-Mehrfachzuordnungen bleiben beim Laden und bei reinen
  Konfigurationsänderungen unverändert.
- Navigation, Zähler und Suche verwenden für eine gewählte normale Kategorie
  dieselbe rekursive Menge aus eigener ID und allen Nachfolger-IDs. Eine
  Oberkategorie zeigt damit direkte und beliebig tief untergeordnete Vorgänge
  ohne Doppelungen; Status- und Kategorie-Badges bleiben getrennt.
- Der Workflowstatus `Erledigt` bleibt in seiner frei konfigurierten normalen
  Zielkategorie sichtbar. Nur Status `Archiv` oder die tatsächliche Kategorie
  `Archiv` werden vom normalen Kategorien-, Zähler- und Suchfilter
  ausgeschlossen.
- Vorgänge besitzen den optionalen, additiv migrierten `FollowUpReason`.
  Detailansicht, Speichern, Duplizieren und Desktop-iPad-Snapshot erhalten den
  Wert. Die Wiedervorlagenübersicht zeigt Kunde, Titel, tatsächlichen
  Auftragstermin, Wiedervorlagedatum, optionalen Grund und Monteur; nur der
  Auftragstermin steuert die farbliche Terminmarkierung.
- Neue mobile Eingänge und Duplikate verwenden die Statuszuordnung. Der
  additive Snapshot-Export enthält `currentCategoryId`, `workflowType`,
  `workflowStep` und `status`; alte Leser tolerieren die zusätzlichen Felder.
- Die feste Navigation enthält keine Angebots-, Auftrags-, Material- oder
  Terminansicht mehr. Normale Vorgänge werden ausschließlich über ihre eine
  stabile Kategorie-ID oder die technische Gesamtansicht `Alle Vorgänge`
  angezeigt.
- Statuswechsel navigieren in die konfigurierte Zielkategorie und halten den
  bearbeiteten Vorgang samt Detailansicht ausgewählt; verschachtelte Ziele
  werden dafür aufgeklappt.
- Der Detailkopf bleibt beim Scrollen sichtbar, die Termine folgen direkt auf
  Aufgabe und der Workflow wird als verbundener, zugänglicher Stepper aus der
  gemeinsamen `WorkflowStep`-Quelle dargestellt.
- Der Workflow-Stepper verwendet ein eigenes responsives Wrap-Panel. Vollständige
  Schritte bleiben zusammen, Beschriftungen dürfen innerhalb des Schritts umbrechen,
  und Verbindungslinien werden am Anfang jeder neuen Zeile ausgeblendet. Eine
  horizontale Scrollleiste ist dafür nicht mehr erforderlich.
- Die Sortierauswahl enthält nur noch eigenständige Sortierungen `Uhrzeit`, `Name`,
  `Erstellt am`, `Wiedervorlage`, `Gesendet am`, `Geändert am` und `Manuell`.
  Status, Kunde, Kategorie, Ort, Termin, Techniker und Titel bleiben ausschließlich
  über ihre sichtbaren Tabellenköpfe direkt sortierbar. Alte gespeicherte Werte
  werden weiterhin tolerant gelesen; unbekannte Werte fallen auf `Erstellt am`.
- Header-Sortierungen, die absichtlich nicht mehr im Dropdown stehen, bleiben
  nach einem Neustart erhalten; ein leerer ComboBox-Auswahlzustand überschreibt
  sie nicht mit `Erstellt am`.

## Lokale Datenhaltung und manueller Desktop-Austausch

- Windows verwendet ausschließlich `%LOCALAPPDATA%\BueroCockpit`, macOS
  ausschließlich `~/Library/Application Support/BueroCockpit`. Der
  Test-Override `BUEROCOCKPIT_DATA_DIRECTORY` bleibt nur für isolierte
  automatisierte Prüfungen erhalten.
- Frühere `storage-location.json`- und
  `storage-location.local.json`-Konfigurationen werden weder gelesen noch
  automatisch migriert. Die frühere OneDrive-Erkennung,
  Windows-/macOS-Pfadübersetzung und das Migrationsskript sind aus dem aktiven
  Produktivweg entfernt.
- `OneDriveEditDirectory` und `IpadLiveFileTargetPath` sind nicht mehr Teil des
  aktiven Desktop-Einstellungsmodells. Alte JSON-Eigenschaften werden ignoriert.
  Die Desktop-App erzeugt weder beim Speichern noch manuell eine
  `live.bclive`-Datei.
- Der lokale Netzwerkdienst erzeugt seinen `.bcsnapshot` ausschließlich in
  einem isolierten temporären Ordner und liefert ihn über den freigegebenen
  lokalen Netzwerkendpunkt aus. Der Paketgenerator enthält weiterhin die
  vollständige zentrale Monteurliste.
- Alte absolute Windows-, macOS- oder Cloudpfade werden nicht mehr automatisch
  anhand von `Tasks`, `DeskItems` oder `BueroCockpit` auf den aktuellen lokalen
  Datenordner umgeschrieben. Neue verwaltete Pfade bleiben relativ zum lokalen
  Datenordner.
- Ein Symlink oder eine Verzeichnisverknüpfung im produktiven Standardpfad
  blockiert den Start, bevor eine SQLite-Datenbank geöffnet wird. Vorhandene
  Cloud-Ordner werden dabei nicht verändert.
- Fehlt lokal eine Datenbank, bietet ein Startdialog den bewussten Importweg
  unter `Daten & Pfade` an; eine alte Cloudquelle wird nicht automatisch
  erkannt oder übernommen.
- Der lokal konfigurierte `BackupExchangeDirectory` darf auf OneDrive liegen,
  enthält aber ausschließlich vollständig geschlossene ZIP-Archive. Laufende
  SQLite-, WAL- oder Lock-Dateien werden dort nie geöffnet.
- Der zusätzliche Austausch-Export verwendet die SQLite-Backup-API, erstellt
  ein vollständiges Produktivdaten-ZIP zunächst lokal, versieht jede Datei mit
  Größe und SHA-256 in `manifest.json` und veröffentlicht das Archiv erst nach
  vollständigem Schreiben über eine atomare Umbenennung im Austauschordner.
- Der Austausch-Import validiert ZIP-Pfade, Manifest, vollständige Dateiliste,
  SHA-256, Datenbankgröße, `PRAGMA integrity_check` und Schema-Version, erzeugt
  zwingend ein vollständiges lokales Rückfall-ZIP und aktiviert den geprüften
  Stand über lokalen Verzeichnistausch mit automatischer Rückstellung bei
  Fehlern. Danach werden die App-Daten neu geladen.
- Backup-ID, Parent-ID, Datenbankrevision, letzte Import-/Exportwerte und
  Gerätebezug bleiben unter Windows in
  `%LOCALAPPDATA%\BueroCockpit\backup-exchange-state.local.json` und unter
  macOS in
  `~/Library/Application Support/BueroCockpitLocal/backup-exchange-state.local.json`;
  der lokale Verlauf liegt jeweils daneben als
  `backup-exchange-journal.local.jsonl`.
- Gerätelokale Einstellungen, Gerätefreigaben, Netzwerk-Checkpoints,
  Austauschzustand und -journal werden nicht exportiert und bleiben beim
  vollständigen Import auf dem jeweiligen Zielgerät erhalten. Das gilt
  ausdrücklich auch unter Windows, wo lokale Konfiguration und Produktivdaten
  denselben OS-Stammordner verwenden.
- Lokale Änderungen, abweichende Parent-ID, unabhängige Abstammung, ältere
  Archive und offenbar veraltete Backups desselben Geräts erzeugen eine
  Konfliktwarnung. Ein erzwungener Import benötigt nach der normalen
  Ersetzungsbestätigung eine zweite eindeutige Bestätigung. Es gibt keine
  automatische Zusammenführung.
- Die bestehenden lokalen `.db`-Sicherungen bleiben erhalten und verwenden
  nun ebenfalls die SQLite-Backup-API.
- Auf dem aktuellen Mac zeigt der Sollpfad noch als Symlink auf
  `~/Library/CloudStorage/OneDrive-ElektroSchweim/Dokumente/BueroCockpit_gemeinsame_Daten`.
  Die App blockiert diesen Zustand. Der Symlink und der OneDrive-Altbestand
  wurden nicht verändert; die bewusste lokale Einrichtung und der erste echte
  Import stehen noch aus.

## Lokaler Netzwerk-Sync

- Der lokale Netzwerk-Sync bleibt ausschließlich manuell und wird auf dem iPad
  durch `Jetzt synchronisieren` gestartet. Der normale Lauf verwendet
  `local-sync-delta-v1` und überträgt nach dem bestätigten Erstabgleich nur
  geänderte Objekte und Dateien.
- Der Desktop-Dienst startet weiterhin nur manuell und kündigt nur während dieses
  Laufs optional `_buerocockpit._tcp` per Bonjour an; die manuelle IP bleibt erhalten.
- Ein iPad wird zunächst mit stabiler Geräte-ID und lokalem Vertrauensnachweis
  vorgemerkt. Der Desktop speichert nur dessen SHA-256-Hash und verlangt eine
  ausdrückliche Freigabe; Freigaben sind widerrufbar. Ein Gerät kann nach
  Bestätigung vollständig aus den Desktop-Einstellungen gelöscht werden.
  Dabei werden nur lokale Freigabe und gerätespezifischer Checkpoint entfernt;
  Nutzdaten bleiben erhalten und eine erneute Kopplung benötigt einen
  Erstabgleich.
- Jedes Gerät besitzt einen lokalen bestätigten Checkpoint mit Serverrevision,
  Server- und Clientsequenz, API-Version, Zeitpunkt und Status. Der ausführliche
  Fingerprintstand liegt in
  `BueroCockpitLocal/local-network-sync-state.json`; ein Ack verschiebt ihn erst
  nach vollständiger iPad-Übernahme.
- `GET /local-sync/changes` liefert nur geänderte Aufträge, Kategorien,
  Monteure, Anhangsmetadaten und Dateien sowie unterstützte Tombstones.
  `GET /local-sync/snapshot` bleibt für Erstabgleich, verlorenen Checkpoint und
  alte iPad-Clients kompatibel. Die SQLite-Datenbank wird niemals übertragen.
- Das iPad prüft SHA-256 und Länge jeder Deltadatei, schreibt den neuen lokalen
  Stand über Staging und atomaren Verzeichnistausch und bestätigt ihn erst
  danach über `POST /local-sync/ack`. Fehler lassen Offline-Stand und alten
  Checkpoint erhalten.
- Authentisierte Uploads übernehmen versionierte Mobile-Inbox-Pakete mit
  `aufgabe.json`, Originalfotos, Vorschauen, markierten Fassungen, Skizzen und
  Dateien zunächst atomar nach `Sync/inbox`.
- Konfliktfreie Neuanlagen und Änderungen werden danach idempotent per Upsert
  gespeichert. Mobile Aufgaben-IDs bleiben stabil; Anhangs-IDs werden aus
  Paket-ID und relativem Pfad deterministisch gebildet. Erst der erfolgreiche
  fachliche Desktopstand wird am iPad als übertragen markiert.
- Basis-, Desktop- und iPad-Wert werden feldweise verglichen. Unabhängige
  Änderungen werden automatisch zusammengeführt; gleichzeitige Änderungen
  desselben Felds bleiben im mobilen Eingang und sind im bestehenden
  Prüfdialog manuell entscheidbar.
- Stabile IDs, deterministische Inhaltsfingerprints und Belege verhindern
  Duplikate. Abweichender Paketinhalt unter derselben ID wird vollständig unter
  `Sync/conflicts` erhalten und überschreibt keinen Desktopbestand.
- Pfade, Größen, SHA-256-Prüfsummen und grundlegende Dateisignaturen werden vor
  der Bestätigung geprüft. Unterbrochene oder unvollständige Pakete erzeugen
  keinen sichtbaren Teilimport; bereits atomar abgelegte Pakete werden nach einem
  Bestätigungsabbruch bei Wiederholung erkannt.
- Die iPad-App zeigt Ziel, konkrete Fortschrittsphase, neue/geänderte
  Empfangs- und Sendezahlen, Anhänge, Referenzdaten, übersprungene unveränderte
  Objekte, Konflikte und Fehler. Ein Leerlauf zeigt `Keine Änderungen
  vorhanden`. Lokale Originale werden auch nach Erfolg nicht automatisch
  gelöscht.
- Bestehende Desktopaufträge sind auf dem iPad offline für Notiz, stabile
  Kategorie-ID, Vorgangstyp/Status, Termin, Wiedervorlage samt Grund und
  Monteur bearbeitbar. Kundendaten und Betreff bleiben in dieser Stufe
  desktopgeführt.
- `local-sync-inbox-v2` trennt Paket-ID und Desktopvorgangs-ID und erhält
  Basisrevision sowie Basiswerte. Nach bestätigtem Upsert wird das lokale Paket
  als übertragen markiert, aber nicht gelöscht.
- Die zentrale Monteurliste wird im lokalen Sync-Paket vollständig mit stabilen
  IDs übertragen. Die Offline-Monteurauswahl enthält deshalb alle
  konfigurierten Monteure und zusätzlich tolerierte, nur in Altaufträgen
  vorkommende Namen.
- Hintergrundzusammenführung, automatischer Dienststart und stille
  Konfliktüberschreibungen bleiben ausgeschlossen. Eine sichtbare
  Reparaturfunktion und iPad-seitige Löschbefehle sind noch nicht implementiert.

## Übergang für bestehende Daten – Variante A

- Keine automatische oder massenhafte Migration von Produktivdaten.
- Keine stillen Schreibvorgänge beim Start, Laden oder Anzeigen.
- Neue und bewusst geänderte Vorgänge verwenden sofort die neue Logik.
- Unveränderte Altbestände dürfen technisch unverändert bleiben und werden
  tolerant gelesen.
- Alte Mehrfachzuordnungen dürfen nicht still gelöscht werden.
- Sobald ein bestehender Vorgang bewusst geändert und gespeichert wird, darf
  für seinen neuen Stand nur noch genau eine normale Kategorie fortgeschrieben
  werden.

## Weiterhin gültiger technischer Stand

- `AppPaths` unterstützt explizite temporäre Daten- und lokale
  Konfigurationsverzeichnisse für isolierte Tests.
- Tabellenlayouts bleiben über eine gemeinsame beziehungsweise
  kategoriebewusste Struktur lokal persistent; bestehende feste Layoutschlüssel
  dürfen nur tolerant weitergelesen und nicht migriert werden.
- Status-ComboBox, Workflowanzeige und Status-Badges verwenden
  `WorkflowStep` als gemeinsame Statusquelle.
- Papierkorb steht im festen Navigationsfuß direkt über Einstellungen.
- Speichern, Duplizieren, Löschen, Wiederherstellen, Archivieren, Material,
  Anhänge, Schreibtisch, Backup, Diagnose und manueller lokaler Sync-Dienst sind
  vorhandene Funktionen.
- Produktive Tests dürfen ausschließlich mit explizit isolierten Testpfaden
  erfolgen.

## Prüfstand und nachgelagerte Abnahme

- Der vollständige frühere Arbeitsstand wurde vor der lokalen
  Releasevorbereitung und erneut vor der Git-Konsolidierung als verifiziertes
  Git-Bundle, Quellarchiv, Binär-Patch und SHA-256-Inventar außerhalb des
  Repositorys gesichert. `codex/work` enthält den vollständigen
  Entwicklungsstand nun nachvollziehbar auf Basis des aktuellen
  Dokumentationsstands aus `origin/main`; der lokale Sicherungsbranch
  `backup/codex-work-2026-07-23` erhält zusätzlich den ursprünglichen
  RC-Zustandscommit.
- Version `0.4.23` liegt als lokaler, nicht veröffentlichter
  Windows-x64-Releasekandidat vor. Windows-Publish, portable ZIP,
  Velopack-Setup, Full-NuGet-Paket, Portable-Paket und Manifestdateien wurden
  frisch erzeugt und per SHA-256 geprüft.
- Der Velopack-`packId` lautet ab diesem Releasekandidaten
  `BueroCockpitApp`. Dadurch liegt die Programm- und Updatewurzel unter
  `%LOCALAPPDATA%\BueroCockpitApp` und der produktive Datenordner bleibt
  getrennt unter `%LOCALAPPDATA%\BueroCockpit`. Bestehende veröffentlichte
  Velopack-Installationen mit dem früheren `packId` benötigen deshalb einen
  einmaligen manuellen Installerwechsel; der alte Terminalserverstand besitzt
  noch keine Auto-Update-Funktion und wird ohnehin manuell abgelöst.
- Ein lokaler Update-Testkanal enthält eine synthetische Velopack-Basis
  `0.4.22` aus demselben geprüften Quellstand und den Ziel-Feed `0.4.23`.
  Dieser Kanal prüft ausschließlich den Update-Mechanismus und ist keine
  historische Binärkopie des veröffentlichten Release `v0.4.22`.
- Desktop-Build, macOS-ARM64-Build, Windows-x64-Build,
  Workflow-/Kategorie-/Netzwerk-Integrationstests, Backup-Austauschtests und
  iPad-Simulator-Build wurden nach der Konsolidierung auf `codex/work`
  erfolgreich wiederholt. Die erzeugten Archive enthalten keine Datenbank,
  Produktiv-, Test- oder PDB-Dateien.
- Ein realer Windows-Start, die Ablösung der alten Inno-Installation, die
  Datenbestandserhaltung, Verknüpfungen, Apps-&-Features-Eintrag und der
  lokale Velopack-Updateweg sind noch auf dem Terminalserver zu prüfen. Die
  Artefakte sind nicht codesigniert; SmartScreen kann warnen. Es wurde kein
  Merge nach `main`, kein Tag und kein GitHub-Release erstellt.
- Der frühere macOS-Fehler, bei dem ein korrekt gespeicherter Status
  `Erledigt` und seine normale Zielkategorie anschließend durch den
  Archivfilter ausgeblendet wurden, ist behoben. Der Fehler wurde im echten
  macOS-Bundle mit isolierten Pfaden als Zähler 0 und `0 Aufgaben`
  reproduziert. Nach der Korrektur zeigte derselbe Datenstand Zähler 1 und den
  Auftrag; nach vollständigem App-Neustart blieb er sichtbar. Ein
  automatisierter Regressionstest deckt Status, Abschlusszeit, stabile
  Zielkategorie, Sichtbarkeit, Archivabgrenzung und Repository-Neustart ab.
- Die isolierten Repository-, Workflow-, Legacy- und Snapshot-Exporttests sind
  erfolgreich. Das reale macOS-Bundle wurde mit isolierten Pfaden sichtbar
  bedient; Neuanlage, Statuswechsel, Haupt- und Unterkategorieauswahl,
  Vorgangs-Drag-and-drop, Löschschutz und Neustartpersistenz wurden erfolgreich
  geprüft.
- Zwei im sichtbaren Rundgang gefundene Auswahlfehler wurden behoben und erneut
  erfolgreich geprüft: initiale ComboBox-Ereignisse verändern keine
  Statuszuordnung, und ein noch neuer Vorgang bleibt beim programmgesteuerten
  Statuskategorienwechsel erhalten.
- Der native Kategoriezeilen-Drag ließ sich im letzten macOS-Lauf über die
  Bedienhilfe nicht zuverlässig auslösen. Der unveränderte Drag-and-drop-Pfad
  und die Parent-/ID-Persistenz wurden angrenzend geprüft; die reale Geste wird
  bei der Windows-Abnahme erneut bedient.
- Die Windows-spezifischen Bedienwege und der praktische Auto-Update-Weg werden
  nach dem veröffentlichten Release `v0.4.22` auf dem Firmenrechner geprüft.
  Diese nachgelagerte Abnahme darf nicht als bereits ausgeführter Test
  dokumentiert werden.
- Version `0.4.22` ist als vollständiger GitHub-/Velopack-Release veröffentlicht
  und enthält den Schutz vor Abstürzen durch nicht lesbare Cloud-Miniaturen.
  Windows-ZIP, Velopack-Setup, Full-NuGet-Paket und alle drei Manifestdateien
  wurden im Release als hochgeladen und größer als 0 Byte bestätigt.
- Der lokale Inbox-Speicher und der HTTP-Dienst wurden mit ausschließlich
  temporären Daten real automatisiert geprüft: fehlende/falsche/gültige Kopplung,
  neuer Upload, Originalfoto, mehrere Fotos, Skizze, Datei, Wiederholung,
  Prüfsummenfehler, unvollständiges JSON, Konflikt und Wiederaufnahme nach fehlendem
  Beleg. Der iOS-Simulator-Build ist erfolgreich.
- Der inkrementelle Store wurde zusätzlich mit 100 isolierten Aufgaben,
  gerätebezogenem Erstabgleich, falschem und richtigem Ack, Neustart, Leerlauf,
  Einzeländerung, geänderter Datei, Wiederholung nach Abbruch, Referenzdaten,
  Tombstones, mehreren Geräten und verlorenem Checkpoint geprüft. Im
  reproduzierbaren Test wurden von 100 vorhandenen Aufgaben genau 1 und von 1
  Anhang genau 1 übertragen; das Vollpaket hatte 2758 Byte, die Deltaantwort
  1213 Byte.
- Im isolierten HTTP-Loopback hatte der vollständige Erstabgleich 1357 Byte und
  benötigte 1,90 ms; die anschließend unveränderte Deltaantwort hatte 481 Byte
  und benötigte 2,86 ms. Diese Zeiten sind lokale Einzelmessungen und keine
  Aussage zur Geschwindigkeit eines realen WLANs.
- Ein isoliertes lokales Netzwerkpaket mit vier zentral konfigurierten
  Monteuren enthielt alle vier unterschiedlichen stabilen IDs. Der aktuelle
  signierte Gerätebuild wurde anschließend als getrennte Test-App mit eigener
  Bundle-ID auf dem physischen iPad Air 7 installiert. Über reales WLAN
  `192.168.178.52:53942` lud und las der echte iPad-Sync-Client einen isolierten
  Erstabgleich mit einem Auftrag und allen vier Monteuren, bestätigte
  `server-1` und erhielt beim zweiten Lauf 0 Aufträge, 0 Dateien sowie
  `Keine Änderungen vorhanden`. Test-App und Testdienst wurden danach
  entfernt. Die reguläre iPad-App und ihr Datencontainer blieben unverändert.
- Das Entfernen einer Gerätekopplung wurde isoliert geprüft: Freigabe und
  Checkpoint verschwinden, das Gerät erhält bei laufendem Dienst sofort HTTP
  403, andere Geräte-Checkpoints und bereits empfangene Inbox-Daten bleiben
  erhalten. Die sichtbare Bestätigungsbedienung in der Desktop-App ist noch
  Teil der nachgelagerten Windows-Abnahme.
- Die vollständige Firmen-Windows-Abnahme sowie physische iPad-Neuanlage,
  Rückänderung, Foto-/Dateiübertragung, sichtbare Konfliktentscheidung und
  echter Abbruch wurden in diesem Lauf nicht ausgeführt.
- Der neue Desktop-Snapshot-Endpunkt wurde ebenfalls mit temporären Daten
  geprüft: ohne Kopplung HTTP 403, mit freigegebenem Nachweis unveränderte
  versionierte Nutzlast. Die reale Snapshot-Erzeugung enthält Adresse, Monteur
  und Wiedervorlagegrund; der iPad-Simulator-Build ist erfolgreich.
- Die responsive Ablaufleiste wurde im echten macOS-Bundle in beiden Themes,
  beiden Vorgangstypen, allen Status und von breiter bis minimaler Fensterbreite
  sichtbar bedient. Alle Schritte blieben erreichbar und die Statuszuordnungen
  führten in die isoliert konfigurierten Zielkategorien.
- Der manuelle Sync wurde mit der echten iPad-App im iOS-Simulator und dem echten
  macOS-Bundle sichtbar durchlaufen: Vormerken, Desktop-Freigabe, Ordnerwahl,
  leerer Lauf, zwei Pakete mit drei Originalfotos sowie Markierung, Skizze und
  Dateien, idempotente Wiederholung, Konflikterhalt, Widerruf, Dienststopp und
  Neustartpersistenz waren erfolgreich. Die iPad-Quellen blieben bestehen; das
  Desktop-Dashboard zeigte zwei neue Aufträge, vier Fotoobjekte und eine Skizze.
- Dabei wurden drei reproduzierbare UI-/Persistenzfehler behoben und erneut real
  geprüft: Header-Sortierpersistenz, bewusste Desktop-Vormerkung erst nach
  `Diesen Desktop verwenden` und der zuvor nicht sichtbare Mobile-Inbox-Ordnerwähler.
- Die iPad-Hauptansicht und Sync-Einstellungen wurden auch im Simulator-Dark-Mode
  sichtbar geprüft. Das Xcode-Ziel ist iPad-only (`TARGETED_DEVICE_FAMILY = 2`);
  eine iPhone-Simulatorgröße ist für dieses Target nicht anwendbar.
- Auf dem physischen iPad Air 7 wurden vor dem ausdrücklich freigegebenen
  Ersetzen der vorhandenen App `Documents` und `Library` nach `/private/tmp`
  gesichert. Der signierte aktuelle Gerätebuild wurde installiert und vertraut.
- Das physische iPad erreichte den isolierten Desktop real per manueller
  LAN-Adresse `192.168.178.52:53941`, wurde vorgemerkt und freigegeben. Ein
  bewusst gestarteter Leer-Sync war erfolgreich; danach wurde der Dienst
  gestoppt und Port 53941 war geschlossen.
- Die physische Übertragung eines Entwurfs mit Foto/Skizze, Wiederholung und
  echter Verbindungsabbruch wurden auf ausdrücklichen Nutzerwunsch auf morgen
  verschoben. Diese Fälle dürfen nicht als bereits ausgeführt gelten.
- Der macOS-Absturz beim Öffnen von `Alle Vorgänge` mit einer in OneDrive
  formal vorhandenen, aber lokal nicht lesbaren Anhang-Miniatur ist behoben.
  Miniaturen werden vor der Übergabe an Skia vollständig in einen
  Memory-Stream gelesen; Timeout oder beschädigte Datei führen nur noch zu
  einer ausgelassenen Vorschau. Der Fehler wurde mit einer lesenden Kopie der
  produktiven Datenbank und isolierter lokaler Konfiguration vor der Änderung
  reproduziert und nach der Änderung im echten macOS-Bundle erneut geprüft.
- Die sichtbare Abnahme des neuen Wiedervorlagenlayouts wurde nicht als
  bestanden dokumentiert: Die Computer-Use-Appauflösung startete das alte
  macOS-Bundle außerhalb der isolierten Testumgebung. Der Prozess wurde sofort
  beendet. Die produktive Datenbank blieb nach read-only Prüfung unverändert;
  lediglich `buerocockpit.lock` im zentralen Ordner erhielt einen neuen
  Zeitstempel und wurde wegen des Verbots von Cloud-Dateiänderungen nicht
  eigenmächtig gelöscht.

## Quellen für verbindliche Entscheidungen

Dieser Projektstatus beschreibt den tatsächlich erreichten Stand, ist aber
keine zweite Architekturquelle. Dauerhafte Architekturentscheidungen stehen in
`docs/PROJEKTREGISTER.md`; Arbeits- und Leseregeln stehen in `AGENTS.md`.
Thematische Fach- und Releasequellen werden von dort gezielt verwiesen.
Historische Aufträge und Journale bleiben reine Verlaufsnachweise.
