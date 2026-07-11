#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
JOURNAL_DIR="$PROJECT_ROOT/docs/codex_journal"
LAST_RUN="$PROJECT_ROOT/docs/codex_last_run.md"
NEXT_TASK="$PROJECT_ROOT/docs/NEXT_TASK.md"
PROJECT_STATUS="$PROJECT_ROOT/docs/PROJEKTSTATUS.md"

usage() {
  cat <<'EOF'
Verwendung:
  ./scripts/update-codex-documentation.sh --input <bericht.md> --name <kurzer-name>
  ./scripts/update-codex-documentation.sh --input <bericht.md> --name <kurzer-name> --dry-run
  ./scripts/update-codex-documentation.sh --input <bericht.md> --name <kurzer-name> \
    --project-status-file <status.md>

Die Eingabedatei muss diese Überschriften enthalten:
  ## Letzter Auftrag
  ## Ziel
  ## Umsetzung
  ## Geänderte Dateien
  ## Tests
  ## Ergebnis
  ## Bekannte offene Punkte
  ## Nächste Aufgabe - Ziel
  ## Nächste Aufgabe - Geplante Schritte
  ## Nächste Aufgabe - Vermutlich betroffene Dateien
  ## Nächste Aufgabe - Nicht verändern

Der Runner schreibt ausschließlich Projektdokumentation. Er führt keine Git-
Schreibaktion, keinen Commit, Push, Tag, Release oder Versionsänderung aus.
Für Branch, Commit und Push folgt danach der separate Git-Helfer aus AGENTS.md.
EOF
}

die() {
  echo "FEHLER: $*" >&2
  exit 1
}

INPUT_FILE=""
SHORT_NAME=""
PROJECT_STATUS_FILE=""
DRY_RUN=false

while [ "$#" -gt 0 ]; do
  case "$1" in
    --input)
      [ "$#" -ge 2 ] || die "Wert für --input fehlt."
      INPUT_FILE="$2"
      shift 2
      ;;
    --name)
      [ "$#" -ge 2 ] || die "Wert für --name fehlt."
      SHORT_NAME="$2"
      shift 2
      ;;
    --project-status-file)
      [ "$#" -ge 2 ] || die "Wert für --project-status-file fehlt."
      PROJECT_STATUS_FILE="$2"
      shift 2
      ;;
    --dry-run)
      DRY_RUN=true
      shift
      ;;
    --help|-h)
      usage
      exit 0
      ;;
    *)
      die "Unbekanntes Argument: $1"
      ;;
  esac
done

[ -n "$INPUT_FILE" ] || die "--input ist erforderlich."
[ -n "$SHORT_NAME" ] || die "--name ist erforderlich."
[ -f "$INPUT_FILE" ] || die "Eingabedatei nicht gefunden: $INPUT_FILE"
[[ "$SHORT_NAME" =~ ^[a-zA-Z0-9._-]+$ ]] || die "--name darf nur Buchstaben, Zahlen, Punkt, Unterstrich und Bindestrich enthalten."

extract_section() {
  local heading="$1"
  awk -v wanted="$heading" '
    $0 == wanted { found=1; next }
    found && /^## / { exit }
    found {
      if (!started && $0 ~ /^[[:space:]]*$/) { next }
      started=1
      sub(/[[:space:]]+$/, "")
      print
    }
  ' "$INPUT_FILE"
}

require_section() {
  local heading="$1"
  local value
  value="$(extract_section "$heading")"
  [ -n "$value" ] || die "Pflichtabschnitt fehlt oder ist leer: $heading"
  printf '%s' "$value"
}

LAST_TASK="$(require_section '## Letzter Auftrag')"
GOAL="$(require_section '## Ziel')"
IMPLEMENTATION="$(require_section '## Umsetzung')"
CHANGED_FILES="$(require_section '## Geänderte Dateien')"
TESTS="$(require_section '## Tests')"
RESULT="$(require_section '## Ergebnis')"
OPEN_POINTS="$(require_section '## Bekannte offene Punkte')"
NEXT_GOAL="$(require_section '## Nächste Aufgabe - Ziel')"
NEXT_STEPS="$(require_section '## Nächste Aufgabe - Geplante Schritte')"
NEXT_FILES="$(require_section '## Nächste Aufgabe - Vermutlich betroffene Dateien')"
NEXT_PROTECTED="$(require_section '## Nächste Aufgabe - Nicht verändern')"

DATE_TIME="$(date '+%Y-%m-%d %H:%M %z')"
FILE_STAMP="$(date '+%Y-%m-%d_%H-%M')"
JOURNAL_FILE="$JOURNAL_DIR/${FILE_STAMP}_${SHORT_NAME}.md"

if [ -e "$JOURNAL_FILE" ]; then
  die "Journaldatei existiert bereits; Historie wird nicht überschrieben: $JOURNAL_FILE"
fi

if [ -n "$PROJECT_STATUS_FILE" ]; then
  [ -f "$PROJECT_STATUS_FILE" ] || die "Statusdatei nicht gefunden: $PROJECT_STATUS_FILE"
fi

cd "$PROJECT_ROOT"

if [ "$DRY_RUN" = true ]; then
  cat <<EOF
DRY-RUN: Keine Datei wird geändert.
Journal: $JOURNAL_FILE
Letzter Lauf: $LAST_RUN
Nächste Aufgabe: $NEXT_TASK
Projektstatus: ${PROJECT_STATUS_FILE:-unverändert}
EOF
  exit 0
fi

mkdir -p "$JOURNAL_DIR"
TMP_DIR="$(mktemp -d "${TMPDIR:-/tmp}/buerocockpit-codex-docs.XXXXXX")"
trap 'rm -rf "$TMP_DIR"' EXIT

write_journal() {
  local status_text="$1"
  cat > "$TMP_DIR/journal.md" <<EOF
# Codex-Journal: $LAST_TASK

## Ziel

$GOAL

## Umsetzung

$IMPLEMENTATION

## Geänderte Dateien

$CHANGED_FILES

## Tests

$TESTS

## Ergebnis

$RESULT

## Bekannte offene Punkte

$OPEN_POINTS

## Aktueller Git-Status

\`\`\`text
$status_text
\`\`\`
EOF
}

write_next_task() {
  cat > "$TMP_DIR/next_task.md" <<EOF
# Nächste Aufgabe

## Ziel

$NEXT_GOAL

## Geplante Schritte

$NEXT_STEPS

## Vermutlich betroffene Dateien

$NEXT_FILES

## Bereiche, die nicht verändert werden dürfen

$NEXT_PROTECTED
EOF
}

write_last_run() {
  local status_text="$1"
  cat > "$TMP_DIR/last_run.md" <<EOF
# Letzter Codex-Lauf

## Datum/Uhrzeit

$DATE_TIME

## Letzter Auftrag

$LAST_TASK

## Zusammenfassung

$RESULT

## Geänderte Dateien

$CHANGED_FILES

## Tests

$TESTS

## Git-Status

\`\`\`text
$status_text
\`\`\` 

## Branch

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Commit

Wird nach dem Dokumentationslauf durch den Git-Helfer ergänzt.

## Push erfolgreich

Nein – der reine Dokumentationslauf führt keinen Push aus.

## Offene Punkte

$OPEN_POINTS

## Empfohlener nächster Schritt

$NEXT_GOAL

$NEXT_STEPS
EOF
}

# Journal und NEXT_TASK werden kollisionsgeschützt als neue Dateien vorbereitet.
INITIAL_STATUS="$(git status --short)"
write_journal "$INITIAL_STATUS"
write_next_task

if [ -n "$PROJECT_STATUS_FILE" ]; then
  cp "$PROJECT_STATUS_FILE" "$TMP_DIR/project_status.md"
fi

mv "$TMP_DIR/journal.md" "$JOURNAL_FILE"
mv "$TMP_DIR/next_task.md" "$NEXT_TASK"
if [ -n "$PROJECT_STATUS_FILE" ]; then
  mv "$TMP_DIR/project_status.md" "$PROJECT_STATUS"
fi

# Der Laufstatus wird nach den Dokumentationsänderungen ermittelt und in beide
# Laufprotokolle geschrieben. Last-Run darf laut Projektregeln überschrieben werden.
FINAL_STATUS="$(git status --short)"
write_journal "$FINAL_STATUS"
write_last_run "$FINAL_STATUS"
mv "$TMP_DIR/journal.md" "$JOURNAL_FILE"
mv "$TMP_DIR/last_run.md" "$LAST_RUN"

echo "Dokumentation aktualisiert:"
echo "- Journal: $JOURNAL_FILE"
echo "- Letzter Lauf: $LAST_RUN"
echo "- Nächste Aufgabe: $NEXT_TASK"
if [ -n "$PROJECT_STATUS_FILE" ]; then
  echo "- Projektstatus: $PROJECT_STATUS"
else
  echo "- Projektstatus: unverändert"
fi
echo "Keine Git-Schreibaktionen ausgeführt."
