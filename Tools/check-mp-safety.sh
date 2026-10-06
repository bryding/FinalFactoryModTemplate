#!/usr/bin/env bash
# Flags code patterns that break Final Factory's deterministic lockstep multiplayer (see CLAUDE.md,
# "Multiplayer and determinism"). A quick text scan, not a proof: an ERROR is almost always a real desync; a
# WARN needs a look; a clean run does not prove the mod is safe.
#
#   Tools/check-mp-safety.sh [folder]     # default: Assets/Scripts. Exit 1 if any ERROR.
#
# Each line is judged by the kind of code it is in: a system in a Fixed group (simulation, on the heartbeat),
# a system in a Controller group (every rendered frame: presentation only), a MonoBehaviour (UI), or other.
set -uo pipefail
root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
dir="${1:-$root/Assets/Scripts}"

find "$dir" -name '*.cs' -not -path '*/Editor/*' | sort | while IFS= read -r file; do
  awk -v file="${file#$root/}" '
    function report(level, msg) {
      printf "%-5s %s:%d  %s\n", level, file, FNR, msg
    }
    # Track what kind of code we are in: the most recent [UpdateInGroup] or MonoBehaviour declaration.
    /UpdateInGroup\(typeof\(FFFixed/      { mode = "fixed" }
    /UpdateInGroup\(typeof\(FFController/ { mode = "controller"; report("WARN", "Controller group: runs every rendered frame. Presentation only (UI, rings, colours); never change simulation state here.") }
    /class [A-Za-z0-9_]+[[:space:]]*:[[:space:]]*MonoBehaviour/ { mode = "mono" }
    /^[[:space:]]*(\/\/|\*|\/\/\/)/ { next }

    /GetRandomForEntity/ { report("ERROR", "RandomSystem.GetRandomForEntity seeds from the Entity handle, which differs between peers. Seed from a tile or a stable id (GetRandomForTileAndSimulationTime, GetRandomForStableHashAndSimulationTime).") }
    /\.DestroyEntity\(/ { report("ERROR", "Delete by adding DeletionMarker, never DestroyEntity.") }
    /UnityEngine\.Random|[^.A-Za-z]Random\.(Range|value|insideUnitCircle|onUnitSphere)|new System\.Random/ { report("WARN", "Unity/System randomness differs per peer: never let it reach simulation state.") }
    /Order(First|Last)[[:space:]]*=[[:space:]]*true/ { report("WARN", "OrderFirst/OrderLast: only to run before a game system that is itself OrderFirst (e.g. KnnSystem); say why in a comment.") }

    mode == "fixed" && /(^|[^A-Za-z])Input\.|Mouse\.current|Keyboard\.current/ { report("ERROR", "Input in a simulation system: each peer has different input. Send player actions as network operations.") }
    mode == "fixed" && /SystemAPI\.Time|World\.Time|UnityEngine\.Time|[^A-Za-z.]Time\.(deltaTime|time|unscaledTime)|ElapsedGameTime|WallClockElapsedTime/ { report("ERROR", "Frame or wall-clock time in simulation. Use FFTimeData.deltaTime and SimulationElapsedTime.") }
    mode == "fixed" && /MePlayer|HoverSelectionState|SelectedEntity|HoveredEntity/ { report("ERROR", "Local player, hover or selection in simulation: they differ per peer.") }
    mode == "fixed" && /LocalToWorld/ { report("WARN", "LocalToWorld is presentation cadence; in simulation read LocalTransform or the building grid tile.") }
    mode == "fixed" && /ScheduleParallel/ { report("WARN", "ScheduleParallel: fine only if no two entities write the same value; shared totals need Schedule.") }
    mode == "fixed" && /\.ToEntityArray\(/ { report("WARN", "Query order differs between peers. If the order decides anything (first, nearest, last writer), sort by a stable key (Placeable.CenterTile).") }

    mode == "controller" && /InventoryHelper\.|Cb\.(AddComponent|RemoveComponent|Instantiate|DestroyEntity|AddBuffer)|CommandBuffer\.(AddComponent|RemoveComponent|Instantiate)/ { report("ERROR", "A per-frame (Controller) system changing inventories or entity structure: that is simulation, and it must run on the heartbeat in a Fixed group.") }
    mode == "controller" && /(^|[^A-Za-z])Input\./ { report("WARN", "Input in a per-frame system: fine for presentation or for sending a player action, never for changing simulation state.") }

    mode == "mono" && /(Ecs|EntityManager)\.(SetComponent|SetComponentData|AddComponent|AddAndSetComponent|RemoveComponent|SetSingleton|DestroyEntity)/ { report("WARN", "A MonoBehaviour writing the world: in multiplayer that change exists on this machine only. UI should read state and send a player action.") }
  ' "$file"
done > "${TMPDIR:-/tmp}/check-mp-safety.$$"

out="${TMPDIR:-/tmp}/check-mp-safety.$$"
cat "$out"
errors=$(grep -c '^ERROR' "$out")
warns=$(grep -c '^WARN' "$out")
rm -f "$out"
echo "check-mp-safety: $errors error(s), $warns warning(s) in ${dir#$root/}"
[[ $errors -eq 0 ]]
