#!/usr/bin/env bash
# Build a real Unreal Android APK only when the proprietary engine and cooked
# user-owned avatar assets are available. Never re-label another APK as Unreal.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
project_dir="${BIGIMONG_UNREAL_PROJECT_DIR:-$repo_root/unreal/Bigimong}"
output_dir="${BIGIMONG_APK_OUTPUT_DIR:-$repo_root/build/Android}"
project="$project_dir/Bigimong.uproject"

if [[ ! -f "$project" ]]; then
  echo "Missing Unreal project: $project" >&2
  exit 2
fi
if [[ -z "${BIGIMONG_UNREAL_ROOT:-}" || ! -x "$BIGIMONG_UNREAL_ROOT/Engine/Build/BatchFiles/RunUAT.sh" ]]; then
  echo "Unreal Engine is required. Set BIGIMONG_UNREAL_ROOT to the installed UE 5.8 directory with RunUAT.sh." >&2
  exit 2
fi

male_assets="$project_dir/Content/Varco/Male"
for asset in SM_MaleBody SK_MaleFace; do
  if [[ ! -s "$male_assets/$asset.uasset" ]]; then
    echo "Unreal Editor import is required: $male_assets/$asset.uasset" >&2
    exit 3
  fi
done
for kind in Eye Hair; do
  for ((index = 0; index < 15; index++)); do
    printf -v name 'SM_Male%s_%02d' "$kind" "$index"
    if [[ ! -s "$male_assets/$name.uasset" ]]; then
      echo "Unreal Editor import is required: $male_assets/$name.uasset" >&2
      exit 3
    fi
  done
done

mkdir -p "$output_dir"
archive_dir="$(mktemp -d "$output_dir/.unreal-archive-XXXXXXXX")"
trap 'rm -rf -- "$archive_dir"' EXIT
export BIGIMONG_APK_ARCHIVE_DIR="$archive_dir"

"$BIGIMONG_UNREAL_ROOT/Engine/Build/BatchFiles/RunUAT.sh" BuildCookRun \
  "-project=$project" -noP4 -target=Bigimong -platform=Android \
  -clientconfig=Development -build -cook -stage -pak -package -archive \
  "-archivedirectory=$archive_dir" -unattended -utf8output

mapfile -d '' apks < <(find "$archive_dir" -type f -name '*.apk' -print0)
if [[ ${#apks[@]} -ne 1 ]]; then
  echo "Expected exactly one Unreal Android APK, found ${#apks[@]}." >&2
  exit 4
fi

python3 - "${apks[0]}" <<'PY'
import sys
import zipfile

try:
    with zipfile.ZipFile(sys.argv[1]) as apk:
        names = apk.namelist()
        if "AndroidManifest.xml" not in names:
            raise ValueError("AndroidManifest.xml missing")
        if not any(name.startswith("lib/arm64-v8a/") and name.endswith(".so")
                   for name in names):
            raise ValueError("arm64 native library missing")
        corrupt = apk.testzip()
        if corrupt:
            raise ValueError(f"Corrupt APK member: {corrupt}")
except (OSError, ValueError, zipfile.BadZipFile) as error:
    sys.exit(f"Invalid Unreal Android APK: {error}")
PY

final="$output_dir/Bigimong-Unreal-MalePreview-debug.apk"
cp -- "${apks[0]}" "$final"
sha256sum "$final" > "$final.sha256"
echo "Built and inspected: $final"
