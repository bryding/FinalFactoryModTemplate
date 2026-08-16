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

# Accept either the install root (contains finalfactory_Data/Managed) or the Managed folder itself.
managed=""
for candidate in "$dir/finalfactory_Data/Managed" "$dir"; do
  if [[ -f "$candidate/FFCore.dll" ]]; then managed="$candidate"; break; fi
done
if [[ -z "$managed" ]]; then
  echo "Could not find FFCore.dll under '$dir'. Point FinalFactoryDir at your Final Factory install folder (the one containing finalfactory_Data)." >&2
  exit 1
fi

dest="$root/Assets/FinalFactoryDlls"
mkdir -p "$dest"

dlls=(FFCore.dll FFSystems.dll FFComponents.dll FFTechnology.dll FFNetcode.dll)
for dll in "${dlls[@]}"; do
  if [[ ! -f "$managed/$dll" ]]; then
    echo "Missing '$dll' in '$managed'" >&2
    exit 1
  fi
  cp -f "$managed/$dll" "$dest/$dll"
  echo "Copied $dll"
done

echo "Done. Copied ${#dlls[@]} DLLs to $dest"
