#!/usr/bin/env bash
# Regenerates Documentation/API (the public, mod-facing API of the game) from the game DLLs in
# Assets/FinalFactoryDlls, so the reference matches the game version you build against.
#
#   Tools/generate-api-reference.sh                      # signatures from your DLLs, summaries from doc-comments.json
#   Tools/generate-api-reference.sh --source <Assets/Scripts of the game>   # game team: also refresh the summaries
#
# Needs: the Unity editor this project uses (for its bundled .NET and C# compiler), the copied game DLLs, and
# finalfactory.properties pointing at the game (its Managed folder supplies the Unity DLLs the signatures use).
# On Windows run it from Git Bash. What gets published, and why, is in Documentation/API/README.md.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/unity-env.sh"

source_dir=""
game_version=""
while [[ $# -gt 0 ]]; do
  case "$1" in
    --source) source_dir="$2"; shift 2;;
    --game-version) game_version="$2"; shift 2;;
    *) echo "Unknown argument: $1" >&2; exit 2;;
  esac
done

dlls="$ROOT/Assets/FinalFactoryDlls"
if [[ ! -f "$dlls/FFCore.dll" ]]; then
  echo "No game DLLs in $dlls. Run copy-finalfactory-dlls first." >&2
  exit 1
fi
if [[ -z "$MANAGED" ]]; then
  echo "Could not find the game's Managed folder from finalfactory.properties (FinalFactoryDir)." >&2
  exit 1
fi

bin="$ROOT/Tools/.cache/api-reference"
mkdir -p "$bin"

# Build the generator with the editor's compiler, against the editor's .NET and Roslyn.
rsp="$bin/build.rsp"
{
  echo "-nologo -target:exe -langversion:latest -nullable:disable -nostdlib+ -out:\"$(native_path "$bin/ApiReference.dll")\""
  for f in "$NETFX"/System.*.dll "$NETFX/netstandard.dll" "$NETFX/mscorlib.dll"; do
    case "$(basename "$f")" in
      System.Private.Uri.dll|System.Private.Xml*|System.Private.DataContract*|*Native*) ;;
      *) echo "-r:\"$(native_path "$f")\"";;
    esac
  done
  echo "-r:\"$(native_path "$UNITY_DATA/DotNetSdkRoslyn/Microsoft.CodeAnalysis.dll")\""
  echo "-r:\"$(native_path "$UNITY_DATA/DotNetSdkRoslyn/Microsoft.CodeAnalysis.CSharp.dll")\""
  echo "\"$(native_path "$ROOT/Tools/ApiReference/ApiReference.cs")\""
} > "$rsp"
run_csc "$rsp"
cp -f "$UNITY_DATA/DotNetSdkRoslyn/Microsoft.CodeAnalysis.dll" "$UNITY_DATA/DotNetSdkRoslyn/Microsoft.CodeAnalysis.CSharp.dll" "$bin/"
cat > "$bin/ApiReference.runtimeconfig.json" <<'JSON'
{ "runtimeOptions": { "tfm": "net6.0", "framework": { "name": "Microsoft.NETCore.App", "version": "6.0.0" }, "rollForward": "Major" } }
JSON

args=(--dlls "$(native_path "$dlls")" --refs "$(native_path "$MANAGED")"
      --config "$(native_path "$ROOT/Tools/ApiReference/api-reference.json")"
      --out "$(native_path "$ROOT/Documentation/API")")
[[ -n "$source_dir" ]] && args+=(--source "$(native_path "$source_dir")")
[[ -n "$game_version" ]] && args+=(--game-version "$game_version")
"$DOTNET" "$(native_path "$bin/ApiReference.dll")" "${args[@]}"
