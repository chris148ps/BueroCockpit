#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
LAST_RUN="$PROJECT_ROOT/docs/codex_last_run.md"

usage() {
  cat <<'EOF'
Verwendung:
  ./scripts/publish-codex-work.sh \
    --name kurzer-name \
    --description "kurze Beschreibung" \
    --include <datei> [--include <datei> ...]

Optional:
  --remote origin
  --dry-run

Der Helfer erzeugt ausschließlich einen Branch codex/work-YYYY-MM-DD-name,
committet nur ausdrücklich angegebene Pfade und pusht ausschließlich diesen
Branch. main, Tags, Releases und Versionsdateien werden nicht verändert.
EOF
}

die() {
  echo "FEHLER: $*" >&2
  exit 1
}

SHORT_NAME=""
DESCRIPTION=""
REMOTE="origin"
DRY_RUN=false
INCLUDE_PATHS=()

while [ "$#" -gt 0 ]; do
  case "$1" in
    --name)
      [ "$#" -ge 2 ] || die "Wert für --name fehlt."
      SHORT_NAME="$2"
      shift 2
      ;;
    --description)
      [ "$#" -ge 2 ] || die "Wert für --description fehlt."
      DESCRIPTION="$2"
      shift 2
      ;;
    --include)
      [ "$#" -ge 2 ] || die "Wert für --include fehlt."
      INCLUDE_PATHS+=("$2")
      shift 2
      ;;
    --remote)
      [ "$#" -ge 2 ] || die "Wert für --remote fehlt."
      REMOTE="$2"
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

[ -n "$SHORT_NAME" ] || die "--name ist erforderlich."
[ -n "$DESCRIPTION" ] || die "--description ist erforderlich."
[ "${#INCLUDE_PATHS[@]}" -gt 0 ] || die "Mindestens ein --include ist erforderlich."
[[ "$SHORT_NAME" =~ ^[a-zA-Z0-9._-]+$ ]] || die "--name enthält ungültige Zeichen."
[[ "$DESCRIPTION" != *$'\n'* ]] || die "--description darf keine Zeilenumbrüche enthalten."

cd "$PROJECT_ROOT"

CURRENT_BRANCH="$(git branch --show-current)"
DATE_STAMP="$(date '+%Y-%m-%d')"
TARGET_BRANCH="codex/work-${DATE_STAMP}-${SHORT_NAME}"
COMMIT_MESSAGE="Codex: $DESCRIPTION"

case "$TARGET_BRANCH" in
  codex/work-*) ;;
  *) die "Interner Fehler: Zielbranch verletzt das codex/work-Schema." ;;
esac

for path in "${INCLUDE_PATHS[@]}"; do
  [ -e "$path" ] || die "Angegebener Pfad nicht gefunden: $path"
done

if ! git diff --cached --quiet; then
  die "Es gibt bereits vorgemerkte Änderungen. Diese müssen zuerst geklärt werden, damit nichts Fremdes mitcommittet wird."
fi

if [ "$DRY_RUN" = true ]; then
  cat <<EOF
DRY-RUN: Keine Branch-, Commit- oder Push-Aktion ausgeführt.
Aktueller Branch: $CURRENT_BRANCH
Zielbranch: $TARGET_BRANCH
Commit: $COMMIT_MESSAGE
Remote: $REMOTE
Ausgewählte Pfade:
$(printf '%s\n' "${INCLUDE_PATHS[@]}")
EOF
  exit 0
fi

if git show-ref --verify --quiet "refs/heads/$TARGET_BRANCH"; then
  die "Lokaler Zielbranch existiert bereits: $TARGET_BRANCH"
fi

git switch -c "$TARGET_BRANCH"
[ "$(git branch --show-current)" = "$TARGET_BRANCH" ] || die "Branchwechsel nicht bestätigt."

git add -- "${INCLUDE_PATHS[@]}"
if git diff --cached --quiet; then
  die "Die ausgewählten Pfade enthalten keine neuen Änderungen."
fi

git commit -m "$COMMIT_MESSAGE"
WORK_COMMIT="$(git rev-parse HEAD)"

PUSH_SUCCESS="Nein"
if git push -u "$REMOTE" "$TARGET_BRANCH"; then
  PUSH_SUCCESS="Ja"
else
  echo "WARNUNG: Der Arbeitscommit wurde erstellt, aber der Push ist fehlgeschlagen." >&2
fi

update_last_run_field() {
  local heading="$1"
  local value="$2"
  local temp_file
  temp_file="$(mktemp "${TMPDIR:-/tmp}/buerocockpit-last-run.XXXXXX")"
  awk -v heading="## $heading" -v value="$value" '
    $0 == heading { print; in_field=1; found=1; next }
    in_field && /^## / { print value; print ""; print; in_field=0; next }
    in_field { next }
    { print }
    END {
      if (in_field) { print value }
      if (!found) { print ""; print heading; print ""; print value }
    }
  ' "$LAST_RUN" > "$temp_file"
  mv "$temp_file" "$LAST_RUN"
}

[ -f "$LAST_RUN" ] || die "Fehlende Dokumentation: $LAST_RUN"
update_last_run_field "Branch" "$TARGET_BRANCH"
update_last_run_field "Commit" "$WORK_COMMIT"
update_last_run_field "Push erfolgreich" "$PUSH_SUCCESS"

git add -- "$LAST_RUN"
if ! git diff --cached --quiet; then
  git commit -m "Codex: Dokumentationsmetadaten aktualisieren"
  METADATA_COMMIT="$(git rev-parse HEAD)"
  if [ "$PUSH_SUCCESS" = "Ja" ]; then
    if ! git push "$REMOTE" "$TARGET_BRANCH"; then
      PUSH_SUCCESS="Nein"
      update_last_run_field "Push erfolgreich" "$PUSH_SUCCESS"
      git add -- "$LAST_RUN"
      git commit -m "Codex: Pushstatus dokumentieren"
      METADATA_COMMIT="$(git rev-parse HEAD)"
      echo "WARNUNG: Der Metadaten-Push ist fehlgeschlagen." >&2
    fi
  fi
else
  METADATA_COMMIT="$WORK_COMMIT"
fi

case "$(git branch --show-current)" in
  codex/work-*) ;;
  *) die "Sicherheitsabbruch: Es darf nur ein codex/work-* Branch veröffentlicht werden." ;
esac

echo "Codex-Arbeitsstand veröffentlicht:"
echo "- Branch: $TARGET_BRANCH"
echo "- Arbeitscommit: $WORK_COMMIT"
echo "- Metadatencommit: $METADATA_COMMIT"
echo "- Push erfolgreich: $PUSH_SUCCESS"
echo "- Aktueller Branch: $(git branch --show-current)"
echo "- Letzter Commit: $(git log -1 --oneline)"
git status --short

if [ "$PUSH_SUCCESS" != "Ja" ]; then
  exit 1
fi
