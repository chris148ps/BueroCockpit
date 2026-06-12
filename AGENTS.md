# BüroCockpit – Arbeitsregeln für KI/Agenten

## Grundregel: Möglichst ohne Codex arbeiten

Innerhalb dieses Projekts soll möglichst viel ohne Codex bzw. ohne großen Agentenlauf umgesetzt werden.

Bevor Codex genutzt wird, soll zuerst geprüft werden, ob die Aufgabe sicher mit folgenden Mitteln erledigt werden kann:

- Terminalbefehle
- gezielte Suche mit grep/sed
- kleine Python-/Shell-Patch-Skripte
- einzelne überschaubare Dateiänderungen
- Build-/Git-Prüfung direkt im Terminal

Codex soll nur genutzt werden, wenn es ohne Codex nicht sinnvoll oder nicht zuverlässig machbar ist.

## Codex nur bei Bedarf verwenden

Codex darf verwendet werden bei:

- größeren Architekturänderungen
- riskanten Datenmigrationen
- Änderungen über viele zusammenhängende Dateien
- komplexen UI-Umbauten
- schwer nachvollziehbaren Fehlern
- Änderungen, bei denen vorher viel Codekontext verstanden werden muss

Bei kleinen Änderungen ist Codex nicht zu verwenden.

## Arbeitsweise ohne Codex

Vor jeder Änderung immer ausführen:

    cd "$HOME/AppProjekte/BueroCockpit"
    git pull origin main
    git status --short
    dotnet build

Vor einem Patch immer zuerst die genaue Code-Stelle suchen und anzeigen.

Nach jeder Änderung immer ausführen:

    dotnet build
    git diff --check
    git status --short

Erst nach erfolgreicher Prüfung committen und pushen:

    git add <geänderte Dateien>
    git commit -m "<kurze Beschreibung>"
    git push origin main
    git status --short

## Keine Echtdaten ins Git

Niemals folgende Daten committen:

- AppData
- Datenbanken
- Anhänge
- Backups
- publish-Ordner
- echte Kundendaten
- Testdaten aus produktiven Datenordnern

## Codex-Aufträge

Wenn Codex doch nötig ist:

- nur ein Thema pro Auftrag
- keine Mischaufgaben
- zuerst relevante Dateien prüfen lassen
- minimal-invasiv ändern
- keine Datenbankmigration ohne ausdrücklichen Grund
- nachher Build, git diff --check, git status prüfen
- nur pushen, wenn origin auf das korrekte GitHub-Repo zeigt
