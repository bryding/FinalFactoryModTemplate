#!/usr/bin/env bash
# Compiles the mod's runtime code (everything under Assets/ except Editor folders) WITHOUT opening Unity, with
# the editor's own C# compiler and the Entities source generators, against the game DLLs the mod's asmdef
# references and the Unity/package DLLs the game ships. A few seconds instead of an editor start, so an agent
# (or you) can check every edit.
#
#   Tools/compile-check.sh            # exit 0 and "OK" when it compiles; otherwise the compiler's errors
#
# It proves the C# compiles against the current game API. It does not check asmdef wiring (a package
# reference missing from FFMod.asmdef), Burst compilation, or behaviour: build in Unity and test in the game
# for those. On Windows run it from Git Bash.
set -euo pipefail
source "$(dirname "${BASH_SOURCE[0]}")/lib/unity-env.sh"

dlls="$ROOT/Assets/FinalFactoryDlls"
if [[ ! -f "$dlls/FFCore.dll" ]]; then
  echo "No game DLLs in $dlls. Run copy-finalfactory-dlls first." >&2
  exit 1
fi
if [[ -z "$MANAGED" ]]; then
  echo "Could not find the game's Managed folder from finalfactory.properties (FinalFactoryDir)." >&2
  exit 1
fi

cache="$ROOT/Tools/.cache"
mkdir -p "$cache/compile-check"

# Entities source generators (IJobEntity, SystemAPI, ...): from the project's package cache once Unity has
# opened it, otherwise downloaded once from Unity's package registry at the version Packages/manifest.json pins.
generators="$(ls -d "$ROOT"/Library/PackageCache/com.unity.entities@*/Unity.Entities/SourceGenerators 2>/dev/null | head -n1 || true)"
if [[ -z "$generators" ]]; then
  entities_version="$(sed -n 's/.*"com.unity.entities": *"\([^"]*\)".*/\1/p' "$ROOT/Packages/manifest.json" | head -n1)"
  generators="$cache/entities-$entities_version/package/Unity.Entities/SourceGenerators"
  if [[ ! -d "$generators" ]]; then
    echo "Downloading com.unity.entities $entities_version (for its source generators) ..."
    tgz="$cache/entities-$entities_version.tgz"
    curl -fsSL -o "$tgz" "https://download.packages.unity.com/com.unity.entities/-/com.unity.entities-$entities_version.tgz"
    mkdir -p "$cache/entities-$entities_version"
    tar -xzf "$tgz" -C "$cache/entities-$entities_version"
  fi
fi

# The game DLLs this assembly references, as FFMod.asmdef lists them.
asmdef="$ROOT/Assets/Scripts/FFMod.asmdef"
mapfile -t game_dlls < <(tr -d '\r' < "$asmdef" | sed -n '/"precompiledReferences"/,/]/p' | grep -o '"[^"]*\.dll"' | tr -d '"')

rsp="$cache/compile-check/FFMod.rsp"
{
  echo "-nologo -target:library -langversion:9.0 -nostdlib+ -deterministic -out:\"$(native_path "$cache/compile-check/FFMod.dll")\""
  echo "-define:UNITY_6000_0_OR_NEWER;UNITY_2022_3_OR_NEWER;UNITY_STANDALONE;ENABLE_MONO"
  # Warnings Unity's own build of a mod shows too; errors are what matter here.
  echo "-nowarn:CS0618,CS0414,CS0169,CS0649,CS0219,CS0162,CS0067,CS0108,CS0114,CS0282,CS1701,CS1702"
  for dll in "${game_dlls[@]}"; do
    if [[ ! -f "$dlls/$dll" ]]; then echo "FFMod.asmdef references $dll but it is not in $dlls (run copy-finalfactory-dlls)." >&2; exit 1; fi
    echo "-r:\"$(native_path "$dlls/$dll")\""
  done
  # The package assemblies the asmdef references ("GUID:..." or by name), as the game ships them, so a package
  # missing from FFMod.asmdef fails here as it would in Unity.
  while read -r ref; do
    [[ -z "$ref" ]] && continue
    name="$ref"
    if [[ "$ref" == GUID:* ]]; then
      name="$(awk -F'\t' -v g="${ref#GUID:}" '$1 == g { print $2 }' "$ROOT/Tools/lib/package-assemblies.tsv")"
      if [[ -z "$name" ]]; then echo "warning: FFMod.asmdef references $ref, which Tools/lib/package-assemblies.tsv does not know; skipped." >&2; continue; fi
    fi
    if [[ -f "$MANAGED/$name.dll" ]]; then
      echo "-r:\"$(native_path "$MANAGED/$name.dll")\""
    else
      echo "warning: $name (referenced by FFMod.asmdef) is not in the game's Managed folder; skipped." >&2
    fi
  done < <(tr -d '\r' < "$asmdef" | sed -n '/"references"/,/]/p' | grep -o '"[^"]*"' | tr -d '"' | grep -v '^references$')
  # The engine and .NET, which every assembly gets.
  for f in "$MANAGED"/UnityEngine.dll "$MANAGED"/UnityEngine.*Module.dll "$MANAGED"/System*.dll "$MANAGED"/mscorlib.dll "$MANAGED"/netstandard.dll; do
    [[ -f "$f" ]] && echo "-r:\"$(native_path "$f")\""
  done
  for g in "$generators"/*.dll; do
    case "$(basename "$g")" in *CodeFixes*) continue;; esac
    echo "-analyzer:\"$(native_path "$g")\""
  done
  find "$ROOT/Assets" -name '*.cs' -not -path '*/Editor/*' -not -path '*/TextMesh Pro/*' | sort | while read -r source; do
    echo "\"$(native_path "$source")\""
  done
} > "$rsp"

status=0
output="$(run_csc "$rsp" 2>&1)" || status=$?
errors="$(printf '%s\n' "$output" | grep -E 'error [A-Z]+[0-9]+' || true)"
if [[ $status -ne 0 && -z "$errors" ]]; then errors="$output"; fi
if [[ -n "$errors" ]]; then
  root_native="$(native_path "$ROOT")"
  printf '%s\n' "$errors" | sed "s#${root_native//\\/\\\\}[\\/]##"
  if printf '%s\n' "$errors" | grep -q 'error CS0012'; then
    echo "hint: CS0012 means a type you use comes from a package assembly FFMod.asmdef does not reference. Add it to \"references\" in Assets/Scripts/FFMod.asmdef (its GUID is in Tools/lib/package-assemblies.tsv)." >&2
  fi
  echo "FAILED: $(printf '%s\n' "$errors" | wc -l | tr -d ' ') error(s)" >&2
  exit 1
fi
echo "OK: Assets/ compiles against the game DLLs in Assets/FinalFactoryDlls (Entities generators: ${generators#$ROOT/})."
