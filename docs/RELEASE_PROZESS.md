# BüroCockpit – verbindlicher Release-Prozess

Diese Datei beschreibt den dauerhaft verbindlichen Release-Ablauf für BüroCockpit.
Sie ist vor jedem Release vollständig zu lesen.

## Ziel

Ein Release ist nur vollständig, wenn es auf GitHub veröffentlicht wurde und von einer bereits installierten Windows-Version über die eingebaute Auto-Update-Funktion gefunden und installiert werden kann.

## Zuständigkeit

- Release-, Git-, Tag-, Build- und Upload-Schritte werden direkt im Terminal ausgeführt.
- Für reine Release-Arbeiten wird kein Codex-Auftrag gestartet.
- Codex wird nur benötigt, wenn vor dem Release noch Codefehler oder größere technische Änderungen behoben werden müssen.

## Technische Grundlage

Die Auto-Update-Funktion verwendet Velopack und liest die Update-Artefakte aus dem GitHub Release.

Der zusätzliche Inno-Setup-Installer `publish/installer/BueroCockpitSetup.exe` ist für die Auto-Update-Funktion nicht erforderlich.

Für ein funktionierendes Windows-x64-Auto-Update müssen im GitHub Release mindestens diese Velopack-Dateien enthalten sein:

- `BueroCockpit-win-x64-Setup.exe`
- `BueroCockpit-<VERSION>-win-x64-full.nupkg`
- `RELEASES-win-x64`
- `releases.win-x64.json`
- `assets.win-x64.json`

Zusätzlich soll die portable Datei hochgeladen werden:

- `BueroCockpit-windows-x64.zip`

Optional kann auch `BueroCockpit-win-x64-Portable.zip` hochgeladen werden.

Wichtig: `BueroCockpit-win-x64-Setup.exe` ist das von Velopack erzeugte Setup und gehört zum Auto-Update-System. Es ist nicht mit dem optionalen Inno-Installer `BueroCockpitSetup.exe` zu verwechseln.

## Vorbereitung

### 1. Konsistenzprüfung vor jedem Release

Automatisch als erster Schritt und noch vor Versionsfestlegung, Build,
Artefakterzeugung, Commit, Tag oder Upload muss die Prüfung aus
`docs/CODEX_AUFTRAGSPRUEFUNG.md` durchgeführt und im Releaseprotokoll
festgehalten werden.

Mindestens zu prüfen sind:

- widersprüchliche Regel- und Fachdateien,
- veraltete Projektregeln,
- Dokumentation gegen den tatsächlichen Stand der App,
- dieser Releaseprozess gegen `AGENTS.md`,
- `docs/DESIGN_RICHTLINIEN.md` gegen die sichtbare Implementierung,
- offene Release-Blocker in `docs/PROJEKTSTATUS.md`,
- bei Vorgangs-, Workflow- oder Kategoriethemen die Fachlogik aus
  `docs/ARBEITSKATEGORIEN.md`.

Wird ein Widerspruch gefunden, ist der Release sofort zu stoppen. Es dürfen
noch keine Version geändert, keine Release-Artefakte erzeugt, keine
Release-Commits oder Tags erstellt und keine Dateien hochgeladen werden. Der
Nutzer entscheidet ausdrücklich, ob zuerst die Regeldateien oder die
Implementierung angepasst werden. Danach ist die vollständige
Konsistenzprüfung zu wiederholen.

### 2. Technische Vorbereitung

1. Arbeitsstand vollständig geprüft und auf `main` übernommen.
2. Arbeitsbaum sauber.
3. Konsistenzprüfung ohne offenen Widerspruch dokumentiert.
4. Nächste Version festlegen.
5. Version in `BueroCockpit.csproj` setzen:
   - `Version`
   - `AssemblyVersion`
   - `FileVersion`
6. Falls der Inno-Installer gepflegt wird, dessen Versionsangabe ebenfalls aktualisieren. Er blockiert den Velopack-Release jedoch nicht.

## Build und Paketierung

Aus dem Repository-Stamm:

```bash
cd "$HOME/AppProjekte/BueroCockpit"
set -euo pipefail

VERSION="<VERSION>"

git status --short
test -z "$(git status --porcelain)"

git diff --check
dotnet build
dotnet build -r win-x64

./scripts/publish-windows.sh
./scripts/package-windows.sh
./scripts/package-velopack-windows.sh
```

## Pflichtprüfung der Auto-Update-Artefakte

```bash
VELOPACK="publish/velopack/win-x64"

test -s "publish/BueroCockpit-windows-x64.zip"
test -s "$VELOPACK/BueroCockpit-win-x64-Setup.exe"
test -s "$VELOPACK/BueroCockpit-${VERSION}-win-x64-full.nupkg"
test -s "$VELOPACK/RELEASES-win-x64"
test -s "$VELOPACK/releases.win-x64.json"
test -s "$VELOPACK/assets.win-x64.json"
```

Zusätzlich kontrollieren, dass die Manifestdateien tatsächlich die neue Version enthalten:

```bash
grep -RIn "$VERSION" \
  "$VELOPACK/RELEASES-win-x64" \
  "$VELOPACK/releases.win-x64.json" \
  "$VELOPACK/assets.win-x64.json"
```

Fehlt eine Pflichtdatei, ist der Release abzubrechen. Kein Tag und kein GitHub Release darf vorher erstellt werden.

## Veröffentlichung

Nach erfolgreicher Prüfung:

```bash
git add BueroCockpit.csproj installer/BueroCockpit.iss
git commit -m "Release $VERSION vorbereiten"
git push origin main

git tag -a "v$VERSION" -m "BüroCockpit $VERSION"
git push origin "v$VERSION"

VELOPACK="publish/velopack/win-x64"

gh release create "v$VERSION" \
  --title "BüroCockpit $VERSION" \
  --notes "<Release Notes>" \
  "publish/BueroCockpit-windows-x64.zip" \
  "$VELOPACK/BueroCockpit-win-x64-Setup.exe" \
  "$VELOPACK/BueroCockpit-${VERSION}-win-x64-full.nupkg" \
  "$VELOPACK/RELEASES-win-x64" \
  "$VELOPACK/releases.win-x64.json" \
  "$VELOPACK/assets.win-x64.json"
```

Falls einzelne Dateien nicht verändert wurden, dürfen sie nicht künstlich zum Commit hinzugefügt werden. Vor dem Commit immer `git status --short` prüfen.

## Abschlussprüfung

```bash
gh release view "v$VERSION"
git status --short --branch
```

Im GitHub Release müssen alle fünf Velopack-Pflichtdateien und das Windows-ZIP sichtbar und größer als 0 Byte sein.

Danach auf einem Windows-Rechner mit der vorherigen Version prüfen:

1. BüroCockpit starten.
2. Update-Prüfung ausführen.
3. Neue Version muss gefunden werden.
4. Update installieren.
5. App neu starten.
6. Angezeigte Version und Grundfunktionen prüfen.

Erst nach diesem Windows-Test gilt der Auto-Update-Weg als praktisch bestätigt.

## Verbindliche Regeln

- Kein Release ohne ausdrückliche Freigabe des Nutzers.
- Kein Release bei einem ungeklärten Widerspruch zwischen Regeln, Dokumentation, Design, Releaseprozess und tatsächlicher App.
- Die Konsistenzprüfung muss vor jeder Versionsänderung und Artefakterzeugung dokumentiert sein.
- Ein Auftrag „Release erstellen“ bedeutet immer vollständiges GitHub Release inklusive Auto-Update-Artefakten.
- Ein bloßes ZIP oder ein Git-Tag ist kein vollständiges Release.
- Der optionale Inno-Installer darf den Velopack-Release nicht unnötig blockieren.
- Keine alten Artefakte aus früheren Versionen wiederverwenden.
- Alle hochgeladenen Dateien müssen im aktuellen Lauf frisch erzeugt worden sein.
- Nach dem Upload immer `gh release view` ausführen.
- Release-Arbeiten möglichst direkt im Terminal erledigen, nicht per Codex.
