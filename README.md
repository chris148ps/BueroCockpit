# BueroCockpit

BueroCockpit ist eine lokale Büro-Aufgabenverwaltung mit Avalonia/.NET. Die App verwaltet Aufgaben, Kategorien, Material, Anhänge, Vorschau, Dashboard, lokale Backups und eine vorbereitete OneDrive-Ordner-Bearbeitung für Anhänge.

## Entwicklung auf macOS

Voraussetzung ist ein installiertes .NET SDK, passend zum Projektziel `net10.0`.

```bash
dotnet build
dotnet run
```

Die Entwicklung erfolgt plattformneutral. App-Daten werden über .NET-AppData-Pfade ermittelt, nicht über fest verdrahtete macOS- oder Windows-Pfade.

## Windows-Version erstellen

Vom Projektroot aus:

```bash
./scripts/publish-windows.sh
./scripts/package-windows.sh
```

Das Publish-Skript erzeugt self-contained Windows-Releases für x64 und ARM64:

```text
publish/windows-x64/
publish/windows-arm64/
```

Das Package-Skript erzeugt die passenden ZIP-Dateien:

```text
publish/BueroCockpit-windows-x64.zip
publish/BueroCockpit-windows-arm64.zip
```

## Release vorbereiten

Ein Release kann vorbereitet werden mit:

```bash
./scripts/release.sh 0.2.0
```

Das Skript prüft die Version, verlangt einen sauberen Git-Arbeitsbaum, setzt die Projektversion, baut die App, erstellt Windows-x64- und Windows-ARM64-Pakete und gibt die manuellen GitHub-Release-Befehle aus.

Es erstellt bewusst keine Git-Tags, pusht nichts und veröffentlicht keinen GitHub Release. Releases sollen nur aus getesteten Ständen erstellt werden.

Ein Auto-Update ist aktuell nicht enthalten. Das wird später erst mit einem Update-Framework wie Velopack ergänzt.

## Installation auf Windows

1. ZIP-Datei auf den Windows-Rechner kopieren.
2. ZIP-Datei in einen lokalen Programmordner entpacken.
3. `BueroCockpit.exe` starten.

Ein klassischer Installer ist in diesem Schritt noch nicht enthalten.

## Daten und Backups

Der Programmcode liegt in GitHub. Echtdaten gehören nicht nach GitHub.

Lokal gespeichert werden insbesondere:

- Datenbank `buerocockpit.db`
- Aufgaben-Anhänge und Thumbnails
- lokale Backups
- lokale Einstellungen wie der gewählte OneDrive-Bearbeitungsordner

Diese Daten liegen im AppData-Ordner der App und werden von Git ignoriert.

## OneDrive-/iPad-Bearbeitung

Der OneDrive-Bearbeitungsordner wird in der App unter `Einstellungen` ausgewählt. Es gibt keine Cloud-API und keine Microsoft-Graph-Anbindung; die App arbeitet nur mit einem frei gewählten lokalen Ordner.
