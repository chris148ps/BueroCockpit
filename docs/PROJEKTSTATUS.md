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
- Navigation, Zähler, Suche, Übersicht und Detail verwenden die aktuelle
  Kategorie-ID; Status- und Kategorie-Badges bleiben getrennt und zeigen beim
  Kategorie-Badge den Pfad.
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

## Lokaler Netzwerk-Sync

- Der erste echte lokale Netzwerk-Sync ist gerichtet `iPad -> Desktop` und wird
  ausschließlich auf dem iPad durch `Jetzt synchronisieren` gestartet.
- Der Desktop-Dienst startet weiterhin nur manuell und kündigt nur während dieses
  Laufs optional `_buerocockpit._tcp` per Bonjour an; die manuelle IP bleibt erhalten.
- Ein iPad wird zunächst mit stabiler Geräte-ID und lokalem Vertrauensnachweis
  vorgemerkt. Der Desktop speichert nur dessen SHA-256-Hash und verlangt eine
  ausdrückliche Freigabe; Freigaben sind widerrufbar.
- Authentisierte Uploads übernehmen versionierte Mobile-Inbox-Pakete mit
  `aufgabe.json`, Originalfotos, Vorschauen, markierten Fassungen, Skizzen und
  Dateien zunächst atomar nach `Sync/inbox` und niemals direkt in die Datenbank.
- Stabile IDs, deterministische Inhaltsfingerprints und Belege verhindern
  Duplikate. Abweichender Inhalt unter derselben ID wird vollständig unter
  `Sync/conflicts` erhalten und überschreibt keinen Desktopbestand.
- Pfade, Größen, SHA-256-Prüfsummen und grundlegende Dateisignaturen werden vor
  der Bestätigung geprüft. Unterbrochene oder unvollständige Pakete erzeugen
  keinen sichtbaren Teilimport; bereits atomar abgelegte Pakete werden nach einem
  Bestätigungsabbruch bei Wiederholung erkannt.
- Die iPad-App zeigt Ziel, konkrete Fortschrittsphase, Abschlusszahlen und letzten
  erfolgreichen Zeitpunkt. Lokale Originale werden in dieser Stufe auch nach
  Erfolg nicht automatisch gelöscht.
- Desktop -> iPad über das lokale Netzwerk, automatische Bidirektionalität und
  direkter Produktivimport bleiben ausdrücklich nicht implementiert.

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

## Verbindliche Projektentscheidungen

- `docs/ARBEITSKATEGORIEN.md` ist die Fachquelle für Vorgangstyp,
  Workflowstatus, benutzerdefinierte Kategorien und Statuszuordnungen.
- Historische Journal-Einträge beschreiben frühere Stände und setzen das neue
  Zielbild nicht außer Kraft.
- Vor jedem Codex-Auftrag und jedem Release ist die Konsistenzprüfung aus
  `docs/CODEX_AUFTRAGSPRUEFUNG.md` Pflicht.
- Jeder ungeklärte Widerspruch zwischen Regeln, Dokumentation, Design und App
  stoppt einen Release.
- Kein Release, Tag, Versionswechsel oder Eingriff in Produktivdaten ohne
  ausdrückliche Freigabe.
