# BüroCockpit Übergabe – iPad iCloud Sync Stand 25.06.2026

## Aktueller Git-Stand

Aktuelle gesicherte Commits:

- `82c334a iPad iCloud Sync Auswahl und Migration korrigieren`
- `b82f5e1 Desktop iCloud Live Datei Zielordner vorbereiten`

Beide Commits sind auf `origin/main` gepusht.

Kein Release wurde erstellt.

## Aktueller Funktionsstand

### Desktop

BüroCockpit Desktop ist vorbereitet für eine frei wählbare iPad-Live-Datei.

In den Einstellungen gibt es den Bereich:

- iPad-Live-Datei
- Zielordner frei wählbar
- vollständiger Pfad zu `live.bclive` sichtbar
- letzter erfolgreicher Export sichtbar
- letzte Fehlermeldung sichtbar
- Button „Jetzt Live-Datei schreiben“
- empfohlener iCloud-Ordner kann geprüft werden

Die Desktop-App ist provider-neutral. Es gibt keine harte iCloud-, Google-Drive- oder OneDrive-Abhängigkeit.

Später soll auf dem Firmenrechner folgender Ordner genutzt werden:

`iCloud Drive\BueroCockpit_iPad_Live`

Die Desktop-App schreibt dort:

`live.bclive`

### iPad

Die iPad-App hat jetzt eine Sync-Quellen-Auswahl:

- Manuelle Datei
- iCloud Drive
- Google Drive Direktlink
- WebDAV / NAS
- Dropbox
- OneDrive / Microsoft Graph

Aktiv nutzbar:

- Manuelle Datei
- iCloud Drive
- Google Drive Direktlink

Vorbereitet/Platzhalter:

- WebDAV / NAS
- Dropbox
- OneDrive / Microsoft Graph

iCloud Drive funktioniert aktuell so:

1. iCloud Drive auswählen
2. `live.bclive` über Dateien-App auswählen
3. App speichert den Security-Scoped Bookmark
4. Datei wird lokal als `current.bclive` übernommen
5. Daten werden geladen
6. „Aus iCloud Drive aktualisieren“ lädt dieselbe Datei erneut, ohne erneute Dateiauswahl
7. Wenn der Zugriff ungültig wird, öffnet die App als Fallback die Dateiauswahl

Der alte Legacy-Startbildschirm mit:

- Live-Datei importieren
- Live-Ordner auswählen
- metadata.json auswählen

erscheint nicht mehr automatisch als Hauptbildschirm.

## Morgen auf dem Firmenrechner

### 1. iCloud für Windows installieren

Auf dem Firmenrechner iCloud für Windows installieren und anmelden.

Danach aktivieren:

- iCloud Drive

### 2. Ordner anlegen

In iCloud Drive folgenden Ordner anlegen:

`BueroCockpit_iPad_Live`

### 3. BüroCockpit Desktop konfigurieren

BüroCockpit starten.

Einstellungen öffnen.

Bereich „iPad-Live-Datei“ öffnen.

Zielordner auswählen:

`iCloud Drive\BueroCockpit_iPad_Live`

Dann Button drücken:

„Jetzt Live-Datei schreiben“

Erwartung:

Im Zielordner liegt danach:

`live.bclive`

### 4. iPad verbinden

Auf dem iPad:

1. BüroCockpit iPad-App öffnen
2. Sync-Einstellungen öffnen
3. iCloud Drive auswählen
4. Datei auswählen:

`iCloud Drive/BueroCockpit_iPad_Live/live.bclive`

5. Daten laden lassen
6. Button testen:

„Aus iCloud Drive aktualisieren“

Erwartung:

Die Datei wird ohne neue Dateiauswahl erneut geladen.

## Nicht tun

- Kein Release erstellen, bevor der Windows-/iCloud-Test abgeschlossen ist.
- Keine Microsoft-Graph-Umsetzung, solange Entra nicht zugänglich ist.
- Kein Google Drive für den Firmenrechner einplanen.
- Keine Polling-/Watcher-/Timer-Logik einbauen.
- Keine Originalanhänge in `live.bclive` aufnehmen.

## Prüfstand

Bisher erfolgreich geprüft:

- `dotnet build`
- iPad `xcodebuild ... CODE_SIGNING_ALLOWED=NO build`
- `git diff --check`
- Grep auf LiveReload/FileSystemWatcher/Polling/Timer ohne neue aktive Treffer
