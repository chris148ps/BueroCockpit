# Codex-Journal: Reale UI- und Simulator-Abnahme

Datum: 2026-07-17 02:54 +0200  
Branch: `codex/work`  
Ausgang: `6f75750721450cbf7a1b1cd08a132feac0ac983d`

## Auftrag

Die nach dem Implementierungslauf verbliebenen realen Bedien- und Sync-Prüfungen sollten mit ausdrücklich erlaubten Regeländerungen jetzt ausgeführt werden. Produktivdaten, automatische Dienste, bidirektionaler Sync, Release und Git-Veröffentlichung blieben ausgeschlossen.

## Sichtbare Desktop-Abnahme

Der Workflow-Stepper wurde im echten macOS-Bundle bei breiten, normalen, schmalen und minimalen Fensterbreiten sowie in hellem und dunklem Theme bedient. Angebotsvorgang und Direktauftrag wurden durch alle jeweiligen Status geschaltet; alle isolierten Statuszuordnungen waren korrekt. Lange Beschriftungen brachen innerhalb vollständiger Schritte um, ohne horizontale Stepper-Scrollleiste oder falsche Verbinder an Zeilenanfängen.

Das reduzierte Sortier-Dropdown und die sichtbaren Tabellenkopf-Sortierungen wurden vollständig bedient. Ein realer Neustart deckte auf, dass ein leerer ComboBox-Zustand eine nur per Kopf gewählte Sortierung auf `Erstellt am` zurücksetzte. Der Setter ignoriert diesen absichtlichen Leerzustand nun; `Kunde ↓` blieb beim Neustart erhalten.

## Sichtbare Sync-Abnahme

Die echte iPad-App lief im iOS-Simulator, der Desktop als echtes macOS-Bundle mit isolierter Daten- und lokaler Konfiguration. Bonjour war auf dem Test-Mac nicht verfügbar, daher wurde der vorgesehene manuelle Fallback `127.0.0.1:53941` verwendet.

Zwei weitere reproduzierbare UI-Fehler wurden gefunden und behoben: Eine bloße erfolgreiche Erreichbarkeitsprüfung galt fälschlich bereits als bewusstes Vormerken, und der vorhandene Mobile-Inbox-Ordnerwähler war nicht sichtbar in die Sync-Einstellungen eingebunden. Nach Neubau und Neuinstallation waren `Desktop prüfen`, `Diesen Desktop verwenden`, Desktop-Freigabe, Ordnerwahl und Neustartpersistenz korrekt.

Hauptansicht und Sync-Einstellungen blieben im Simulator-Dark-Mode lesbar und bedienbar. Das Projektziel ist mit `TARGETED_DEVICE_FAMILY = 2` ausdrücklich iPad-only; eine iPhone-Simulatorgröße war deshalb für dieses Target nicht anwendbar.

Zwei isolierte Entwürfe mit drei Originalfotos, Vorschauen, einer Markierung, einer Skizze samt `.pkdrawing` und zwei Dateien wurden bewusst mit `Jetzt synchronisieren` übertragen. Die Desktop-Inbox enthielt genau zwei Einträge und zwei Belege; die Test-PNGs waren bytegleich und die iPad-Quellordner blieben erhalten. Eine identische Wiederholung wurde zweimal übersprungen. Geänderter Inhalt unter derselben ID landete separat in `Sync/conflicts`, ohne den ersten Inhalt zu überschreiben. Widerruf und gestoppter Dienst wurden am iPad klar abgelehnt; der Widerruf blieb nach Desktop-Neustart erhalten.

## Sicherheitsabschluss

Alle Desktopdaten lagen unter `/private/tmp/BueroCockpit-Remaining-20260716-2045`. Ein alter sichtbarer OneDrive-Pfad wurde nicht beschrieben; die kontrollierten produktiven Zeitstempel blieben unverändert. Der Sync-Dienst wurde beendet und Port 53941 hatte abschließend keinen Listener.

## Verifikation

Erfolgreich: Workflow-/Kategorie-Integrationstests, Standard-.NET-Build, `win-x64`, `osx-arm64`, iOS-Simulator-Build und `git diff --check`.

Nicht ausgeführt: physischer Bonjour-Fund und echter Funkabbruch während einer großen Fotoübertragung. Das gekoppelte physische iPad wurde am Ende wieder verfügbar, enthielt aber bereits `BüroCockpit` mit derselben Bundle-ID. Diese Installation wurde ohne ausdrückliche Freigabe zum Ersetzen und möglichen Berühren lokaler App-Daten nicht überschrieben. Die physische Abnahme bildet genau die eine nächste Aufgabe.

## Git

Keine Version, kein Commit, kein Push, kein Merge, kein Tag und kein Release.
