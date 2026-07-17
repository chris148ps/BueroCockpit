# Codex-Journal: Responsiver Stepper und manueller LAN-Sync

Datum: 2026-07-16 20:31 +0200  
Branch: `codex/work`  
Ausgang: `6f75750721450cbf7a1b1cd08a132feac0ac983d`

## Auftrag und Entscheidung

Der Workflow-Stepper sollte dezenter und ohne horizontales Herauslaufen werden, die Sortierauswahl sollte keine direkt per Tabellenkopf erreichbaren Duplikate mehr enthalten, und das vorhandene lokale Netzwerkgerüst sollte zu einem ersten echten manuellen iPad-Upload ausgebaut werden.

Die Vorprüfung fand einen aktuellen Dokumentationswiderspruch zur Kopplung. Nach ausdrücklicher Freigabe des Benutzers wurde das iPad-Konzept auf Bonjour/manuelle IP, Desktop-Freigabe und einen lokal gespeicherten widerrufbaren Nachweis ohne alten Pairing-Code korrigiert.

## Ergebnis

- Stepper-Schritte umbrechen als unteilbare Einheiten; Verbindungen enden am Zeilenende und beginnen nicht quer in einer neuen Zeile.
- Das Sortier-Dropdown bleibt wegen sieben eigenständiger Sortierungen erhalten; sieben direkt sortierbare Spaltenoptionen wurden entfernt.
- Der Desktop nimmt nur von ausdrücklich freigegebenen Geräten versionierte Mobile-Inbox-Pakete an.
- Neue Pakete werden geprüft und atomar nach `Sync/inbox` geschrieben. Identische Wiederholungen werden übersprungen; geänderte gleiche IDs landen ohne Überschreiben in `Sync/conflicts`.
- Das iPad startet den Datentransfer ausschließlich durch `Jetzt synchronisieren`, zeigt Phasen und Abschlusszahlen und löscht keine lokalen Originale.
- Die Richtung bleibt bewusst `iPad -> Desktop-Inbox`; Desktopdaten werden nicht automatisch zurückgeschrieben oder zusammengeführt.

## Verifikation

Erfolgreich: Standard-, `win-x64`- und `osx-arm64`-Desktop-Build, iOS-Simulator-Build und isolierte Workflow-/Sync-Integrationstests einschließlich echter Loopback-HTTP-Anfragen. Dabei wurde ein Chunked-Transferfehler reproduziert, durch eine tatsächlich begrenzte Request-Stream-Lesung behoben und erneut erfolgreich getestet. Ein paralleler .NET-Restore kollidierte einmal in `obj/project.assets.json`; die verbindlichen Runtime-Builds wurden anschließend nacheinander erfolgreich wiederholt.

Nicht ausgeführt: sichtbare Stepper-Abnahme, weil der Mac gesperrt war; reale Zwei-Geräte-/Bonjour-/Neustart-Abnahme mangels physischem iPad und zweitem Zielsystem. Diese Punkte stehen als genau eine nächste Abnahmeaufgabe in `NEXT_TASK.md`.

## Grenzen eingehalten

Keine Produktivdaten, Cloud-Dateien, Datenmigration, automatische Übertragung, Originalbereinigung, Version, Commit, Push, Merge, Tag oder Release.
