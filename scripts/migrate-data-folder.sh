#!/usr/bin/env bash
set -euo pipefail

DRY_RUN=0
INCLUDE_ICLOUD_ARCHIVE=0

for arg in "$@"; do
  case "$arg" in
    --dry-run)
      DRY_RUN=1
      ;;
    --include-icloud-archive)
      INCLUDE_ICLOUD_ARCHIVE=1
      ;;
    -h|--help)
      cat <<'USAGE'
Usage: scripts/migrate-data-folder.sh [--dry-run] [--include-icloud-archive]

Migriert den zentralen BueroCockpit-Datenordner von
BueroCockpit_iPad_Bearbeitung nach BueroCockpit_Daten.

Optionen:
  --dry-run                  Nur anzeigen, nichts aendern.
  --include-icloud-archive   Alten iCloud-Testordner in Archiv/Alt_iCloud kopieren.
USAGE
      exit 0
      ;;
    *)
      echo "Unbekannter Parameter: $arg" >&2
      exit 2
      ;;
  esac
done

timestamp="$(date '+%Y%m%d-%H%M%S')"
home_dir="$HOME"
documents_dir="$home_dir/Library/CloudStorage/OneDrive-ElektroSchweim/Dokumente"
old_data_dir="$documents_dir/BueroCockpit_iPad_Bearbeitung"
new_data_dir="$documents_dir/BueroCockpit_Daten"
sync_root="$new_data_dir/Sync"
icloud_dir="$home_dir/Library/Mobile Documents/com~apple~CloudDocs/BueroCockpit_iPad_Live"
dirk_entry_name="mobile-20260629-100654-dirk-kröger"
dirk_source="$icloud_dir/mobile-inbox/$dirk_entry_name"
dirk_import_target="$new_data_dir/Import/iCloud_mobile-inbox/$dirk_entry_name"
settings_path="$home_dir/Library/Application Support/BueroCockpitLocal/settings.local.json"

did_archive_icloud="nein"
did_copy_dirk="nein"
did_update_settings="nein"
renamed_old_to_new=0

say() {
  printf '%s\n' "$*"
}

run() {
  if [[ "$DRY_RUN" -eq 1 ]]; then
    printf '[dry-run] '
    printf '%q ' "$@"
    printf '\n'
  else
    "$@"
  fi
}

copy_tree_if_missing() {
  local source="$1"
  local target="$2"
  if [[ ! -e "$source" ]]; then
    return
  fi
  if [[ -e "$target" ]]; then
    say "Behalte vorhanden: $target"
    return
  fi
  run mkdir -p "$(dirname "$target")"
  run cp -a "$source" "$target"
}

write_hint_file() {
  local target="$new_data_dir/HINWEIS.txt"
  if [[ -e "$target" ]]; then
    return
  fi

  if [[ "$DRY_RUN" -eq 1 ]]; then
    say "[dry-run] HINWEIS.txt wuerde angelegt: $target"
    return
  fi

  cat > "$target" <<'HINWEIS'
BüroCockpit zentraler Datenordner

Dieser Ordner ist die zentrale OneDrive-Datenquelle fuer BüroCockpit.

Aktiver Arbeitsordner:
BueroCockpit_Daten/

Aktiver SyncRoot:
BueroCockpit_Daten/Sync/

Nicht als aktive Datenquelle verwenden:
- BueroCockpit_iPad_Bearbeitung
- BueroCockpit_iPad_Live
- Sync/Sync

AppProjekte und GitHub enthalten nur Quellcode/Releases, keine produktiven Daten.
HINWEIS
}

update_local_settings() {
  local new_value="$new_data_dir/"
  run mkdir -p "$(dirname "$settings_path")"

  if [[ -f "$settings_path" ]]; then
    run cp -p "$settings_path" "$settings_path.backup_$timestamp"
  fi

  if [[ "$DRY_RUN" -eq 1 ]]; then
    say "[dry-run] OneDriveEditDirectory wuerde gesetzt auf: $new_value"
    did_update_settings="dry-run"
    return
  fi

  SETTINGS_PATH="$settings_path" NEW_VALUE="$new_value" python3 - <<'PY'
import json
import os
from pathlib import Path

settings_path = Path(os.environ["SETTINGS_PATH"])
new_value = os.environ["NEW_VALUE"]

if settings_path.exists():
    with settings_path.open("r", encoding="utf-8") as handle:
        data = json.load(handle)
else:
    data = {}

data["OneDriveEditDirectory"] = new_value
with settings_path.open("w", encoding="utf-8") as handle:
    json.dump(data, handle, indent=2, ensure_ascii=False)
    handle.write("\n")
PY
  did_update_settings="ja"
}

say "BüroCockpit Datenordner-Migration"
say "Dry-Run: $([[ "$DRY_RUN" -eq 1 ]] && echo ja || echo nein)"
say "Alter Ordner: $old_data_dir"
say "Neuer Ordner: $new_data_dir"
say

if pgrep -if "BueroCockpit|BüroCockpit|dotnet.*BueroCockpit" >/dev/null 2>&1; then
  say "WARNUNG: BüroCockpit scheint zu laufen. Bitte App beenden, bevor die echte Migration ausgefuehrt wird."
  if [[ "$DRY_RUN" -eq 0 ]]; then
    exit 1
  fi
fi

if [[ ! -d "$documents_dir" ]]; then
  say "FEHLER: OneDrive-Dokumente-Ordner nicht gefunden: $documents_dir" >&2
  exit 1
fi

if [[ -d "$old_data_dir" && ! -e "$new_data_dir" ]]; then
  say "Neuer Ordner existiert nicht. Alter Ordner wird in neuen Namen umbenannt."
  run mv "$old_data_dir" "$new_data_dir"
  renamed_old_to_new=1
elif [[ -d "$new_data_dir" ]]; then
  say "Neuer Ordner existiert bereits. Es wird nichts ueberschrieben."
  run mkdir -p "$new_data_dir/Archiv/Manuelle_Sicherungen"
  if [[ -d "$old_data_dir" ]]; then
    archive_target="$new_data_dir/Archiv/Manuelle_Sicherungen/BueroCockpit_iPad_Bearbeitung_$timestamp"
    say "Alter Ordner bleibt erhalten und wird als Sicherheitskopie archiviert: $archive_target"
    copy_tree_if_missing "$old_data_dir" "$archive_target"
  fi
elif [[ -d "$old_data_dir" ]]; then
  say "Neuer Ordner fehlt, alter Ordner wurde aber erkannt. Abbruch wegen unerwartetem Zustand."
  exit 1
else
  say "Alter Ordner nicht gefunden. Neuer Ordner wird angelegt."
  run mkdir -p "$new_data_dir"
fi

run mkdir -p "$sync_root/live" "$sync_root/inbox" "$sync_root/processed" "$sync_root/snapshots" "$sync_root/conflicts"
run mkdir -p "$new_data_dir/Import" "$new_data_dir/Archiv/Alt_iCloud" "$new_data_dir/Archiv/Alt_Testreste" "$new_data_dir/Archiv/Manuelle_Sicherungen"

if [[ "$renamed_old_to_new" -eq 0 && -d "$old_data_dir/Sync" ]]; then
  copy_tree_if_missing "$old_data_dir/Sync/live.bclive" "$sync_root/live.bclive"
  copy_tree_if_missing "$old_data_dir/Sync/live/tasks.json" "$sync_root/live/tasks.json"
  copy_tree_if_missing "$old_data_dir/Sync/live/categories.json" "$sync_root/live/categories.json"
  copy_tree_if_missing "$old_data_dir/Sync/live/metadata.json" "$sync_root/live/metadata.json"
  copy_tree_if_missing "$old_data_dir/Sync/snapshots/latest.bcsnapshot" "$sync_root/snapshots/latest.bcsnapshot"
fi

if [[ -d "$dirk_source" ]]; then
  if [[ -d "$sync_root/inbox/$dirk_entry_name" || -d "$sync_root/processed/$dirk_entry_name" ]]; then
    say "Dirk-Kroeger-Mobile-Eingang ist bereits in Sync/inbox oder Sync/processed vorhanden."
    did_copy_dirk="bereits vorhanden"
  elif [[ -d "$dirk_import_target" ]]; then
    say "Dirk-Kroeger-Mobile-Eingang ist bereits im Import vorhanden: $dirk_import_target"
    did_copy_dirk="bereits im Import"
  else
    say "Dirk-Kroeger-Mobile-Eingang wird kontrolliert in Import kopiert: $dirk_import_target"
    copy_tree_if_missing "$dirk_source" "$dirk_import_target"
    did_copy_dirk="ja"
  fi
else
  say "Dirk-Kroeger-Mobile-Eingang nicht gefunden: $dirk_source"
fi

if [[ "$INCLUDE_ICLOUD_ARCHIVE" -eq 1 ]]; then
  if [[ -d "$icloud_dir" ]]; then
    icloud_archive_target="$new_data_dir/Archiv/Alt_iCloud/BueroCockpit_iPad_Live_$timestamp"
    say "Alter iCloud-Testordner wird archiviert: $icloud_archive_target"
    copy_tree_if_missing "$icloud_dir" "$icloud_archive_target"
    did_archive_icloud="ja"
  else
    say "Alter iCloud-Testordner nicht gefunden: $icloud_dir"
  fi
else
  say "iCloud-Altbestand wird nicht archiviert. Fuer Archivkopie --include-icloud-archive verwenden."
fi

write_hint_file
update_local_settings

say
say "Migration abgeschlossen."
say "Aktiver Arbeitsordner: $new_data_dir/"
say "Aktiver SyncRoot: $sync_root/"
say "iCloud-Daten archiviert: $did_archive_icloud"
say "Dirk-Kroeger-Eingang: $did_copy_dirk"
say "Lokale Settings aktualisiert: $did_update_settings"
if [[ -e "$old_data_dir" ]]; then
  say "Alter Ordner existiert noch: ja ($old_data_dir)"
else
  say "Alter Ordner existiert noch: nein"
fi
say
say "Naechste Pruefbefehle:"
say "  dotnet build"
say "  xcodebuild -project iPad/BueroCockpitSnapshotReader/BueroCockpitSnapshotReader.xcodeproj -scheme BueroCockpitSnapshotReader -destination 'generic/platform=iOS Simulator' CODE_SIGNING_ALLOWED=NO build"
say "  git diff --check"
say "  git status --short"
