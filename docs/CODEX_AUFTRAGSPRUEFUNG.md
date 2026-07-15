# BüroCockpit – Prüfung vor jedem Codex-Auftrag

Diese Datei ist vor jedem Codex-Auftrag verbindlich zu beachten.

## Ziel

Vor dem Start eines Codex-Auftrags muss geprüft werden, ob der geplante Auftrag mit bestehenden Projekt-, Fach-, Sicherheits-, Test-, Design-, Release- oder Sync-Regeln vereinbar ist.

## Pflichtprüfung

Vor jedem Codex-Auftrag sind zu lesen und auf Widersprüche zu prüfen:

- `AGENTS.md`
- `docs/CODEX_PROJEKTREGELN.md`
- alle nach `AGENTS.md` für das Thema relevanten Regel- und Fachdateien
- zusätzlich alle Dokumente, die die betroffene Fachlogik ausdrücklich beschreiben

Geprüft werden müssen insbesondere:

- gegensätzliche fachliche Aussagen
- ausdrückliche Verbote oder Sperren
- veraltete Zielbilder
- widersprüchliche Daten- oder Workflowregeln
- abweichende Sicherheits-, Test-, Release- oder Branchvorgaben

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

## Änderung von Regeldateien

Nach Freigabe dürfen nur die konkret betroffenen Aussagen angepasst werden. Andere Projektregeln bleiben unverändert.

Wenn mehrere Regeldateien dieselbe Fachlogik enthalten, müssen sie konsistent geändert werden. Veraltete Gegenregeln dürfen nicht stehen bleiben.

## Dokumentation im Auftrag

Jeder größere Codex-Auftrag muss kurz festhalten:

- welche Regeldateien vorab geprüft wurden,
- ob Widersprüche gefunden wurden,
- welche freigegebenen Regeländerungen vor dem eigentlichen Auftrag erfolgt sind.

Testergebnisse oder Freigaben dürfen nicht erfunden werden.
