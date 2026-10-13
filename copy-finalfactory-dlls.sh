#!/usr/bin/env bash
# Copies the required Final Factory DLLs into Assets/FinalFactoryDlls.
# Reads the game install path from finalfactory.properties.
# Set that path with the "Modding > Set Final Factory Path..." menu in Unity,
# or by editing finalfactory.properties by hand.
set -euo pipefail

root="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
props="$root/finalfactory.properties"

if [[ ! -f "$props" ]]; then
  echo "Could not find $props. Run 'Modding > Set Final Factory Path...' in Unity first, or create it with a line like: FinalFactoryDir=/path/to/FinalFactory" >&2
  exit 1
fi

dir="$(grep -E '^[[:space:]]*FinalFactoryDir[[:space:]]*=' "$props" | head -n1 | sed -E 's/^[^=]*=[[:space:]]*//;s/[[:space:]]*$//')"

if [[ -z "${dir:-}" ]]; then
  echo "FinalFactoryDir is not set in $props" >&2
  exit 1
fi

# Accept the install root on Windows/Linux (contains finalfactory_Data/Managed), the install root on macOS
# (contains finalfactory.app, whose DLLs are in Contents/Resources/Data/Managed), the .app itself, or the
# Managed folder itself. Same list as Tools/lib/unity-env.sh.
managed=""
for candidate in "$dir/finalfactory_Data/Managed" "$dir" "$dir"/*.app/Contents/Resources/Data/Managed \
  "$dir/Contents/Resources/Data/Managed"; do
  if [[ -f "$candidate/FFCore.dll" ]]; then managed="$candidate"; break; fi
done
if [[ -z "$managed" ]]; then
  echo "Could not find FFCore.dll under '$dir'. Point FinalFactoryDir at your Final Factory install folder: the one containing finalfactory_Data (Windows, Linux) or finalfactory.app (macOS)." >&2
  exit 1
fi

dest="$root/Assets/FinalFactoryDlls"
mkdir -p "$dest"

# The game's own assemblies: every mod needs them.
dlls=(FFCore.dll FFSystems.dll FFComponents.dll FFTechnology.dll FFNetcode.dll FFSpaghetti.dll)
# What FFSpaghetti.dll and FFCore.dll load in turn (the game's input, configuration and UI plugins, and the .NET metadata
# reader the mod loader uses). A mod's build links every assembly it can reach and stops at the first one it cannot find
# (w1227: 0.50.0.103 added FFInput, FFConfiguration and System.Reflection.Metadata, and the template stopped building).
# Not every game version has every one, so a missing one is skipped with a note.
extras=(FFInput.dll FFConfiguration.dll FFTests.dll GraphAndChart.dll Graphy.dll QFSW.QC.dll FMODUnity.dll Unity.Physics.Custom.dll
        System.Collections.Immutable.dll System.Reflection.Metadata.dll System.Reflection.MetadataLoadContext.dll)
for dll in "${dlls[@]}"; do
  if [[ ! -f "$managed/$dll" ]]; then
    echo "Missing '$dll' in '$managed'" >&2
    exit 1
  fi
  cp -f "$managed/$dll" "$dest/$dll"
  echo "Copied $dll"
done
count=${#dlls[@]}
for dll in "${extras[@]}"; do
  if [[ -f "$managed/$dll" ]]; then
    cp -f "$managed/$dll" "$dest/$dll"
    echo "Copied $dll"
    count=$((count + 1))
  else
    echo "Note: '$dll' is not in '$managed' (this game version does not have it); skipped"
  fi
done

echo "Done. Copied $count DLLs to $dest"
