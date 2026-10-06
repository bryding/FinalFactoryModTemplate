#!/usr/bin/env bash
# Shared set-up for the Tools/ scripts: finds the Unity editor this project uses (its bundled .NET runtime and
# Roslyn C# compiler) and the game's Managed folder. Source it; it sets:
#   ROOT       the project root
#   UNITY_DATA the editor's data folder (has DotNetSdkRoslyn/ and NetCoreRuntime/)
#   DOTNET     the editor's dotnet executable
#   CSC        the editor's csc.dll
#   NETFX      the .NET shared framework folder (System.*.dll) next to DOTNET
#   MANAGED    the game's Managed folder (from finalfactory.properties), or empty if not found
# and defines native_path (a path the Windows dotnet understands, when running under Git Bash).
#
# Override the editor with UNITY_EDITOR_DATA=/path/to/Editor/Data (Windows, Linux) or
# /path/to/Unity.app/Contents (macOS).

ROOT="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

native_path() {
  if command -v cygpath >/dev/null 2>&1; then cygpath -w "$1"; else printf '%s' "$1"; fi
}

unity_version="$(sed -n 's/^m_EditorVersion: //p' "$ROOT/ProjectSettings/ProjectVersion.txt" | tr -d '\r')"

UNITY_DATA=""
for candidate in \
  "${UNITY_EDITOR_DATA:-}" \
  "/c/Program Files/Unity/Hub/Editor/$unity_version/Editor/Data" \
  "/Applications/Unity/Hub/Editor/$unity_version/Unity.app/Contents" \
  "$HOME/Unity/Hub/Editor/$unity_version/Editor/Data"; do
  if [[ -n "$candidate" && -f "$candidate/DotNetSdkRoslyn/csc.dll" ]]; then UNITY_DATA="$candidate"; break; fi
done
if [[ -z "$UNITY_DATA" ]]; then
  echo "Could not find Unity $unity_version (looked for DotNetSdkRoslyn/csc.dll under the usual Unity Hub folders)." >&2
  echo "Install it with Unity Hub, or set UNITY_EDITOR_DATA to its Editor/Data (Windows, Linux) or Unity.app/Contents (macOS) folder." >&2
  exit 1
fi

DOTNET="$UNITY_DATA/NetCoreRuntime/dotnet"
[[ -f "$DOTNET.exe" ]] && DOTNET="$DOTNET.exe"
CSC="$UNITY_DATA/DotNetSdkRoslyn/csc.dll"
NETFX="$(ls -d "$UNITY_DATA"/NetCoreRuntime/shared/Microsoft.NETCore.App/*/ 2>/dev/null | sort -V | tail -n1)"
NETFX="${NETFX%/}"
if [[ ! -f "$DOTNET" || -z "$NETFX" ]]; then
  echo "Unity $unity_version at $UNITY_DATA has no bundled .NET runtime (NetCoreRuntime/)." >&2
  exit 1
fi

MANAGED=""
props="$ROOT/finalfactory.properties"
if [[ -f "$props" ]]; then
  game_dir="$(grep -E '^[[:space:]]*FinalFactoryDir[[:space:]]*=' "$props" | head -n1 | sed -E 's/^[^=]*=[[:space:]]*//;s/[[:space:]]*$//' | tr -d '\r')"
  if [[ -n "$game_dir" ]]; then
    # Windows paths from the properties file ("C:/...") become Git Bash paths ("/c/...").
    if command -v cygpath >/dev/null 2>&1; then game_dir="$(cygpath -u "$game_dir")"; fi
    for candidate in "$game_dir/finalfactory_Data/Managed" "$game_dir" "$game_dir"/*.app/Contents/Resources/Data/Managed; do
      if [[ -f "$candidate/FFCore.dll" ]]; then MANAGED="$candidate"; break; fi
    done
  fi
fi

# Compiles C# sources with the editor's Roslyn. Usage: run_csc <response file>
run_csc() {
  "$DOTNET" "$(native_path "$CSC")" "@$(native_path "$1")"
}
