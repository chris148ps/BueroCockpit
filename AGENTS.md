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


## Konkrete Codex-Modell-Empfehlung vor jeder Codex-Aufgabe

Bevor eine Aufgabe an Codex übergeben wird, muss ChatGPT innerhalb dieses Projekts immer eine konkrete Codex-Modell-Empfehlung geben.

Die Empfehlung muss vor dem eigentlichen Codex-Auftrag stehen.

ChatGPT darf dabei nicht nur allgemeine Begriffe wie „mittleres Modell“, „sparsames Modell“ oder „starkes Modell“ verwenden, wenn konkrete Modellnamen bekannt oder aus dem Codex-Menü ersichtlich sind.

Die Empfehlung muss dieses Format haben:

    Codex-Modell-Empfehlung:
    Empfohlenes Modell: <konkreter Modellname>
    Sparsame Alternative: <konkreter Modellname oder „ohne Codex per Terminal/Patch“>
    Nicht nötig: <konkreter Modellname oder Modellklasse, die für diese Aufgabe unnötig viel Limit verbrauchen würde>
    Begründung: <kurze technische Begründung>

Die Empfehlung soll immer das Modell wählen, das die Aufgabe zuverlässig und sauber erledigen kann und dabei möglichst wenig Limit verbraucht.

Grundregeln:

- Wenn die Aufgabe ohne Codex sicher per Terminal, Suche oder kleinem Patch-Skript erledigt werden kann, soll ChatGPT zuerst „ohne Codex per Terminal/Patch“ empfehlen.
- Für kleine, klar begrenzte Codeänderungen soll ein sparsames Codex-Modell empfohlen werden.
- Für mittlere Änderungen über mehrere zusammenhängende Dateien soll ein ausgewogenes Codex-Modell empfohlen werden.
- Für große Architekturänderungen, Datenbankmigrationen, komplexe UI-Umbauten, schwer nachvollziehbare Fehler oder riskante Änderungen soll ein starkes Thinking-/Reasoning-Modell empfohlen werden.
- Ältere oder sparsamere Codex-Modelle dürfen ausdrücklich empfohlen werden, wenn sie für die Aufgabe ausreichen.
- Das stärkste verfügbare Modell soll nur empfohlen werden, wenn es für Qualität, Sicherheit oder Fehlervermeidung wirklich sinnvoll ist.
- ChatGPT soll nicht automatisch das stärkste Modell empfehlen, sondern immer Nutzen, Risiko, Dateiumfang und Limitverbrauch abwägen.
- Die Modell-Empfehlung muss auch dann gegeben werden, wenn der Nutzer nur kurz schreibt: „mach mit Codex“, „weiter mit Codex“, „Codex Auftrag“, „gib mir den Codex Auftrag“ oder ähnlich.
- Zusätzlich soll ChatGPT immer den passenden Codex-Startbefehl mit Modellparameter angeben, damit nicht vorher manuell /model eingegeben werden muss.
- Wenn die verfügbaren Modellnamen im lokalen Codex-Menü abweichen, soll ChatGPT den Nutzer auffordern, einmal die sichtbaren Modellnamen aus dem Codex-Modellmenü zu nennen, und danach nur noch diese konkreten Namen verwenden.

Beispiel:

    Codex-Modell-Empfehlung:
    Empfohlenes Modell: gpt-5-codex
    Sparsame Alternative: gpt-5-codex-mini
    Nicht nötig: gpt-5.5-thinking
    Begründung: Die Aufgabe betrifft mehrere zusammenhängende Dateien, aber keine Datenmigration und keine Architekturänderung.

Passender Startbefehl:

    cd "$HOME/AppProjekte/BueroCockpit"
    codex -m gpt-5-codex

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
