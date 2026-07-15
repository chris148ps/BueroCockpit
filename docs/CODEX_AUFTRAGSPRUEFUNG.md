# BüroCockpit – Prüfung vor jedem Codex-Auftrag und Release

Diese Datei ist vor jedem Codex-Auftrag und zusätzlich vor jedem Release verbindlich zu beachten.

## Ziel

Vor dem Start eines Codex-Auftrags muss geprüft werden, ob der geplante Auftrag mit bestehenden Projekt-, Fach-, Sicherheits-, Test-, Design-, Release- oder Sync-Regeln vereinbar ist. Vor einem Release muss zusätzlich bestätigt werden, dass die Dokumentation der tatsächlichen App entspricht und der Releaseprozess weiterhin mit `AGENTS.md`, den Designrichtlinien und der Implementierung übereinstimmt.

## Pflichtprüfung

Vor jedem Codex-Auftrag und Release ist diese Prüfung automatisch als erster
Arbeitsschritt auszuführen. Zu lesen und auf Widersprüche zu prüfen sind:

- `AGENTS.md`
- `docs/CODEX_PROJEKTREGELN.md`
- alle nach `AGENTS.md` für das Thema relevanten Regel- und Fachdateien
- zusätzlich alle Dokumente, die die betroffene Fachlogik ausdrücklich beschreiben
- bei Vorgangs-, Workflow- oder Kategoriethemen `docs/ARBEITSKATEGORIEN.md`
- bei jedem Release zusätzlich `docs/TESTRICHTLINIEN.md`, `docs/DESIGN_RICHTLINIEN.md`, `docs/PROJEKTSTATUS.md` und `docs/RELEASE_PROZESS.md`

Geprüft werden müssen insbesondere:

- gegensätzliche fachliche Aussagen
- ausdrückliche Verbote oder Sperren
- veraltete Zielbilder
- widersprüchliche Daten- oder Workflowregeln
- abweichende Sicherheits-, Test-, Release- oder Branchvorgaben
- Unterschiede zwischen dokumentiertem Projektstand und tatsächlicher App
- Unterschiede zwischen Designrichtlinien und sichtbarer Implementierung
- Unterschiede zwischen `AGENTS.md` und dem beschriebenen Releaseablauf

## Verbindliche Fachprüfung für Vorgänge

Bei Vorgangs-, Workflow-, Kategorie-, Navigations-, Filter-, Import-, Export-
oder Sync-Arbeiten muss ausdrücklich geprüft werden, ob der Auftrag die
Fachlogik aus `docs/ARBEITSKATEGORIEN.md` einhält:

- genau ein Vorgangstyp,
- genau ein Workflowstatus,
- genau eine daraus abgeleitete sichtbare Arbeitskategorie,
- Kennzeichnungen getrennt von Arbeitskategorien,
- keine automatische Migration unveränderter Produktivdaten nach Variante A.

## Verhalten bei Widerspruch

Steht der geplante Auftrag im Widerspruch zu einer bestehenden Regel, darf der eigentliche Codex-Auftrag nicht gestartet werden.

Stattdessen ist dem Nutzer konkret mitzuteilen:

1. welche Datei betroffen ist,
2. welche bestehende Aussage widerspricht,
3. welcher Teil des neuen Auftrags davon betroffen ist,
4. welche Regeländerung notwendig wäre,
5. welche fachlichen oder technischen Auswirkungen die Änderung hätte.

Danach ist ausdrücklich nachzufragen, ob die betroffene Regeldatei angepasst werden soll.

Ohne diese Freigabe darf weder die widersprechende Regel geändert noch der eigentliche Codex-Auftrag gestartet werden.

## Verhalten vor einem Release

Wird vor einem Release ein Widerspruch, eine veraltete Regel oder eine
Abweichung zwischen Dokumentation und App gefunden, ist der Release sofort zu
stoppen. Insbesondere dürfen noch keine Versionsänderung, Release-Artefakte,
Commits, Tags oder Uploads erzeugt werden.

Dem Nutzer sind Datei, widersprechende Aussage, tatsächlicher App-Stand und
Auswirkung zu nennen. Der Nutzer entscheidet anschließend ausdrücklich, ob
zuerst die Regeldateien oder die Implementierung angepasst werden. Erst nach
erneuter erfolgreicher Konsistenzprüfung darf der Release fortgesetzt werden.

## Änderung von Regeldateien

Nach Freigabe dürfen nur die konkret betroffenen Aussagen angepasst werden. Andere Projektregeln bleiben unverändert.

Wenn mehrere Regeldateien dieselbe Fachlogik enthalten, müssen sie konsistent geändert werden. Veraltete Gegenregeln dürfen nicht stehen bleiben.

## Dokumentation im Auftrag

Jeder größere Codex-Auftrag muss kurz festhalten:

- welche Regeldateien vorab geprüft wurden,
- ob Widersprüche gefunden wurden,
- welche freigegebenen Regeländerungen vor dem eigentlichen Auftrag erfolgt sind.

Jeder Release muss zusätzlich festhalten:

- welche Regel- und Fachdateien geprüft wurden,
- ob Dokumentation und tatsächliche App übereinstimmen,
- ob Releaseprozess, `AGENTS.md` und Designrichtlinien konsistent sind,
- dass kein ungeklärter Widerspruch mehr besteht.

Testergebnisse oder Freigaben dürfen nicht erfunden werden.
