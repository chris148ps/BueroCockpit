# Letzter Codex-Lauf

## Datum/Uhrzeit

2026-07-17 02:54 +0200

## Auftrag

Die reale UI- und Netzwerk-Abnahme des responsiven Workflow-Steppers, der bereinigten Sortierung und des manuellen gerichteten iPad-zu-Desktop-Syncs ausführen. Anschließend wurde der vollständige Windows-Auto-Update-Release ausdrücklich beauftragt; die noch ausstehende physische Foto-/Skizzenübertragung wurde auf ausdrücklichen Nutzerwunsch auf morgen verschoben.

## Ausgang und Sicherheit

- Arbeitsbranch `codex/work`, HEAD `6f75750721450cbf7a1b1cd08a132feac0ac983d`; vorhandene Änderungen blieben lokal und uncommittet.
- Regel- und Fachdateien wurden vor der Fortsetzung erneut auf Widersprüche geprüft. Die zuvor freigegebene Aktualisierung des iPad-Sync-Konzepts war konsistent mit Implementierung und aktuellem Auftrag.
- Desktop-Daten und lokale Konfiguration lagen ausschließlich unter `/private/tmp/BueroCockpit-Remaining-20260716-2045`; der iPad-Simulator verwendete ausschließlich seinen eigenen App-Container.
- Ein versehentlich sichtbar gewordener alter OneDrive-Zielpfad wurde nicht beschrieben. Zeitstempel von produktiver Datenbank und `live.bclive` blieben unverändert; danach wurde der temporäre Exportpfad explizit gesetzt.
- Der Desktop-Dienst wurde nur über die sichtbare Benutzeraktion gestartet und nach der Prüfung wieder gestoppt. Abschlussprüfung: kein Listener auf Port 53941.

## Reale UI-Abnahme und dabei behobene Fehler

- macOS-Desktop: Angebotsvorgang mit allen sieben und Direktauftrag mit allen vier Workflowstatus real bedient; alle Statuszuordnungen führten in die isoliert konfigurierten Zielkategorien.
- Stepper bei etwa 1290, 1000, 780 und minimal etwa 768 Pixeln sowie in hellem und dunklem Theme geprüft. Ganze Schritte blieben erreichbar, lange Beschriftungen brachen innerhalb des Schritts um, Zeilenanfänge hatten keinen führenden Verbinder und es entstand keine horizontale Stepper-Scrollleiste.
- Sortier-Dropdown enthielt genau `Uhrzeit`, `Name`, `Erstellt am`, `Wiedervorlage`, `Gesendet am`, `Geändert am`, `Manuell`; Status, Kunde, Kategorie, Ort, Termin und Techniker wurden jeweils auf- und absteigend über sichtbare Tabellenköpfe bedient.
- Reproduzierbarer Persistenzfehler behoben: Bei einer nur über den Tabellenkopf verfügbaren Sortierung lieferte die ComboBox kurz einen leeren Wert und setzte die Speicherung auf `Erstellt am` zurück. `SelectedSortField` ignoriert diesen absichtlichen leeren Auswahlzustand. `Kunde ↓` blieb nach echtem App-Neustart sichtbar erhalten.
- Reproduzierbarer iPad-Flussfehler behoben: Eine erfolgreiche Erreichbarkeitsprüfung galt fälschlich bereits als bewusstes Vormerken und blendete `Diesen Desktop verwenden` aus. Nur ein explizit vorgemerkter Status gilt jetzt als gespeichert.
- Reproduzierbarer iPad-UI-Fehler behoben: Der vorhandene Mobile-Inbox-Ordnerwähler war nicht in die Sync-Einstellungen eingebunden. Die Karte `Lokale mobile Eingänge` ist wieder sichtbar und zugänglich.
- iPad-Hauptansicht und Sync-Einstellungen wurden zusätzlich im Simulator-Dark-Mode sichtbar geprüft; Texte, Status, Schaltflächen und Karten blieben lesbar. Das Xcode-Ziel ist ausdrücklich iPad-only (`TARGETED_DEVICE_FAMILY = 2`), daher ist eine iPhone-Simulatorgröße für dieses Target nicht anwendbar.

## Reale manuelle Sync-Abnahme

- Gerät: iPad-App im Simulator `iPad Pro 11-inch (M5)`, iOS 26.4; Desktop-App als echtes macOS-Bundle mit isolierten Pfaden.
- Bonjour war auf dem Test-Mac nicht verfügbar. Der dokumentierte manuelle Fallback `127.0.0.1:53941` wurde real benutzt.
- `Desktop prüfen`, `Diesen Desktop verwenden`, Desktop-Vormerkung, ausdrückliche Freigabe, Ordnerwahl und ein leerer Sync wurden sichtbar erfolgreich bedient. Ein App-Neustart erhielt Desktop-Vormerkung und Ordnerberechtigung.
- Zwei isolierte mobile Entwürfe mit drei Originalfotos, Vorschauen, einer markierten Fassung, einer PNG-Skizze samt `.pkdrawing` und zwei Dateien wurden mit `Jetzt synchronisieren` übertragen. iPad-Abschluss: `2` Entwürfe, `3` Fotos, `0` übersprungen.
- Desktop-Ablage: genau zwei Inbox-Einträge und zwei Belege. Alle übertragenen Test-PNGs waren per SHA-256 bytegleich mit den Quellen; die iPad-Quellordner blieben bestehen. Das Desktop-Dashboard zeigte `2` neue Aufträge, `4` Fotoobjekte einschließlich Markierung und `1` Skizze.
- Identische Wiederholung: `0` übertragen, `0` Fotos, `2` übersprungen und keine Duplikate.
- Geänderter Inhalt unter derselben stabilen ID: `1` fehlgeschlagen; separater Konfliktordner vorhanden, weiterhin genau zwei Inbox-Einträge und kein Überschreiben.
- Desktop-Freigabe widerrufen: iPad zeigte klar `Die Freigabe dieses iPads wurde am Desktop widerrufen.` Der Widerruf blieb nach Desktop-Neustart erhalten.
- Dienst gestoppt: kein Listener auf Port 53941; iPad meldete die abgelehnte Verbindung und verwies auf Dienst und Gerätefreigabe.
- Vor dem Ersetzen der vorhandenen physischen iPad-App wurden `Documents` und `Library` lesend nach `/private/tmp/BueroCockpit-iPad-Air-7-backup-20260717-0310` gesichert. Nach ausdrücklicher Freigabe wurde der signierte Gerätebuild erfolgreich installiert und das Entwicklerprofil am iPad vertraut.
- Das physische iPad erreichte den isolierten Desktop real über `192.168.178.52:53941`, wurde vorgemerkt und am Desktop freigegeben. Ein bewusst gestarteter Leer-Sync wurde erfolgreich mit `0` übertragenen, `0` übersprungenen und `0` fehlgeschlagenen Objekten gespeichert. Der Dienst wurde danach gestoppt; Port 53941 hatte keinen Listener.
- Die physische Übertragung eines Entwurfs mit Foto/Skizze, Wiederholung und echter Verbindungsabbruch wurden auf ausdrücklichen Nutzerwunsch auf morgen verschoben und sind nicht als bestanden dokumentiert.

## Builds und Tests

- `dotnet run --project tests/BueroCockpit.WorkflowTests/BueroCockpit.WorkflowTests.csproj`: erfolgreich.
- `dotnet build`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r win-x64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `dotnet build -r osx-arm64`: erfolgreich, 0 Warnungen, 0 Fehler.
- `xcodebuild -project iPad/BueroCockpitSnapshotReader/BueroCockpitSnapshotReader.xcodeproj -scheme BueroCockpitSnapshotReader -sdk iphonesimulator -destination 'generic/platform=iOS Simulator' -derivedDataPath /private/tmp/BueroCockpitSnapshotReaderDerivedData CODE_SIGNING_ALLOWED=NO build`: erfolgreich.
- Signierter Gerätebuild für das physische iPad Air 7 mit automatischer Entwicklungsbereitstellung: erfolgreich; Installation und realer manueller Leer-Sync erfolgreich.
- `./scripts/release.sh 0.4.21`: erfolgreich; frische Windows-x64- und Velopack-Artefakte erzeugt.
- `./scripts/check-release-artifacts.sh` sowie explizite Größen-, Versions- und SHA-256-Prüfung aller Auto-Update-Pflichtartefakte: erfolgreich. Der optionale Inno-Installer ist nicht vorhanden.
- `git diff --check`: erfolgreich.

## Git

- Branch: `codex/work`
- HEAD: `6f75750721450cbf7a1b1cd08a132feac0ac983d`
- Der Funktionsstand wird nach erfolgreicher Release-Vorprüfung veröffentlicht und anschließend als Version `0.4.21` vollständig für Windows-Auto-Update freigegeben.

## Branch

codex/work

## Commit

537b51bcb30d29052dae5ae942648b0466af5eda

## Push erfolgreich

Ja
