# BüroCockpit

BüroCockpit ist eine lokale Büro-Aufgabenverwaltung mit Avalonia/.NET. Die App verwaltet Aufgaben, Kategorien, Material, Anhänge, Vorschau, Dashboard, lokale Backups und eine vorbereitete OneDrive-Ordner-Bearbeitung für Anhänge.

Der sichtbare App-Name ist `BüroCockpit`. Technische Namen wie Projektordner, Repository, Namespace und EXE bleiben `BueroCockpit`.

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

## Auto-Update vorbereiten

Die Auto-Update-Grundlage ist mit Velopack vorbereitet. Die App initialisiert Velopack beim Start und zeigt unter `Einstellungen` den Update-Status an.

Der bestehende Inno-Setup-Installer bleibt vorerst für manuelle Installationen erhalten, bis Velopack vollständig getestet ist.

Geplanter späterer Release-Ablauf:

```bash
./scripts/release.sh 0.2.0
dotnet tool install -g vpk
./scripts/publish-windows.sh
./scripts/package-velopack-windows.sh
```

Das Velopack-Skript erzeugt lokale Windows-Update-Artefakte unter:

```text
publish/velopack/win-x64
publish/velopack/win-arm64
```

Diese Dateien sind lokale Release-Artefakte und werden nicht ins Git eingecheckt. Auto-Update wird erst aktiv, wenn diese Artefakte an einen GitHub Release oder einen anderen Update-Ort veröffentlicht werden.

Auto-Update über GitHub Releases funktioniert ohne Zusatzlogik nur sauber, wenn die Release-Dateien für die App erreichbar sind. Für private Repositories braucht man später eine sichere Zugriffslösung oder einen anderen Download-Ort.

Echtdaten liegen im AppData-Ordner und werden durch Updates nicht überschrieben.

## Lokaler Auto-Update-Test

Der lokale Update-Test ist ein reiner Testablauf. Es wird kein GitHub Release erstellt, es werden keine Tags gesetzt und es wird nichts veröffentlicht.

Ziel: Eine installierte Version `0.1.0` lokal gegen eine vorbereitete Version `0.2.0` testen.

Testbereich vorbereiten:

```bash
./scripts/prepare-local-update-test.sh
```

Das Skript legt an:

```text
publish/local-update-test/initial
publish/local-update-test/update
publish/local-update-test/feed
```

Grundablauf:

1. Version `0.1.0` bauen:
   ```bash
   ./scripts/publish-windows.sh
   ./scripts/package-velopack-windows.sh
   ```
2. Die erzeugten Velopack-Artefakte aus `publish/velopack/win-x64` nach `publish/local-update-test/initial` kopieren und diese Version installieren/starten.
3. Testweise in `BueroCockpit.csproj` die Version auf `0.2.0` setzen.
4. Erneut bauen:
   ```bash
   ./scripts/publish-windows.sh
   ./scripts/package-velopack-windows.sh
   ```
5. Die neuen Velopack-Artefakte aus `publish/velopack/win-x64` nach `publish/local-update-test/feed` kopieren.
6. In BüroCockpit unter `Einstellungen` den lokalen Update-Kanal auf den Feed-Ordner setzen, z. B. `publish/local-update-test/feed`.
7. `Nach Updates suchen` ausführen und das gefundene Update testen.
8. Prüfen, dass AppData, Datenbank, Anhänge und Backups erhalten bleiben.
9. Nach dem Test die Projektversion wieder auf den echten Stand zurücksetzen.

Dieser Ablauf ist nicht für produktive Updateverteilung gedacht.

## Windows-Installer erstellen

Zuerst die Windows-Publish-Dateien erzeugen:

```bash
./scripts/publish-windows.sh
```

Danach auf einem Windows-Rechner Inno Setup 6 installieren und im Projektordner ausführen:

```powershell
powershell -ExecutionPolicy Bypass -File scripts/build-installer-windows.ps1
```

Das Ergebnis liegt unter:

```text
publish/installer/BueroCockpitSetup.exe
```

Der Installer enthält Programmdateien für x64 und ARM64 und installiert automatisch die passende Variante. Echtdaten, Datenbank, Anhänge, Backups und lokale Einstellungen liegen im AppData-Ordner und werden nicht in den Installer gepackt.

Auto-Update ist im Installer noch nicht enthalten. Später kann Velopack oder ein anderes Update-System ergänzt werden.

Das App-Icon liegt unter `Assets/BueroCockpit.ico` und wird für die Windows-EXE sowie den Inno-Setup-Installer verwendet.

## Installation auf Windows per ZIP

1. ZIP-Datei auf den Windows-Rechner kopieren.
2. ZIP-Datei in einen lokalen Programmordner entpacken.
3. `BueroCockpit.exe` starten.

## Installation auf Windows per Installer

1. `BueroCockpitSetup.exe` auf den Windows-Rechner kopieren.
2. Setup starten.
3. Optional die Desktop-Verknüpfung auswählen.

Der Installer legt eine Startmenü-Verknüpfung und einen Deinstaller an.

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
