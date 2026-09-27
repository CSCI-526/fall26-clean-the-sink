# Sink integration verification

Date: 2026-09-27. Branch: `sink-integration`.
Unity: 6000.3.22f1. Scene: `Assets/SinkLab/Scenes/SinkLab.unity`.

## Scope and decisions

Integrated local `main` (`fa77df4`) into local `zhanli` (`9fa0cd4`) in merge
commit `078c702`; neither original branch was moved. Remote freshness was not
verified. Implementation changes remain reviewable in the working tree.

- Retained main's one continuous hollow drain rim.
- Saved the shallow rounded basin, circular opening and plug into reusable prefabs.
  Renderer and non-convex collider share the opening mesh during shrink and reset.
- Removed the jet's extra corner range/escape force and its occlusion exception.
  Hard contact rebounds through a wall physics material (0.4, Maximum);
  the production bounce threshold remains 2 m/s. Floors stay non-bouncy.
- Kept rising-water buoyancy/shuffle and drain pull. Physics and visible water
  use the same transformed surface height for translated/yaw-rotated unit-scale sinks.
- Kept the user's deferred failure behavior: overflow can clear after draining,
  and an unused full-open charge can recover a sealed drain.
- Procedural water audio is default-off. Water visuals remain active.
- Added human readability requirements and EditorConfig. Changed source follows
  normal braced control flow and focused methods. Legacy untouched code still has
  readability debt; this is not a repository-wide refactor.
- Recorded the user's no-`codex/` branch-naming preference in root AGENTS.md.

## Verification scope

EditMode covers saved prefab connections, mesh/collider references, aperture/capture
agreement, actual dropping through the hole, blocked corner spray with positive
controls, and smaller starting-radius overrides without changing the fixed floor
footprint or persistent mesh assets.

PlayMode covers real injected controls including E/F/R, hard and gentle contacts
with saved dry/wet materials, non-bouncy floor contacts, moved-sink water/vortex
alignment, real buoyancy and wet-friction assignment, full-open outflow, and reset.
Contact fixtures isolate native collision response; these are not full gameplay
playthroughs or proof of subjective feel.

The saved-scene audit uses the real movement, camera, nozzle, spray and available
drain tools. It never teleports food, adds separate forces, directly washes stains,
or marks food drained. Its simulated duration is not a human completion time.

The initial EditMode run passed 39/40: the hierarchy assertion expected the old
layout. The revised contract now requires the nested rounded-corner prefab.
The next run passed 40/40, and the initial PlayMode run passed 16/16.
Those runs precede final readability, reuse and surface-normal corrections.

## Final evidence

- Compilation completed without errors (`final-compilation.json`).
- EditMode: **41/41 passed**, no skips (`edit-tests-result.json`).
- PlayMode: **16/16 passed**, no skips (`play-tests-result.json`).
- Offline integrity and whitespace checks passed (`static-check.json`).
- Fresh explicit camera render inspected: `rendered-saved-scene.png`. Rounded
  surfaces shade cleanly; the circular drain rim remains one continuous mesh.
  The built-in stopped Game view capture was cached, so it was not used as final
  visual evidence. Camera rendering does not include IMGUI.
- Live default audio check: **0 AudioSources**, `enableWaterAudio=false`
  (`gameplay-start.json`).

The new radius-override test initially had fixture-only failures: body transforms
needed explicit synchronization, and native lifecycle callbacks cannot be driven
with SendMessage in EditMode. The final fixture uses the project's established
scene-preservation setup and directly invokes the same initialization methods.
Its geometry, shrink/reset and persistent-asset assertions were retained.

- Saved-scene gameplay audit: **passed**, all 8 scraps and 6 stains cleared,
  **86.804 simulated seconds at 2× time scale** (`gameplay-audit.json`).
  The successful run did not need the optional E/F recovery routines; separate
  injected-control and water tests verify those tools.
- The actual completed render was inspected (`04-complete.png`). The final live
  state confirms completion, zero audio sources, one rim renderer, shared opening
  renderer/collider mesh, and zero transient basin authoring components
  (`gameplay-final-state.json`).
- Runtime console ground truth during the successful audit: no compile failure,
  0 current Editor errors and 0 warnings (`gameplay-console.json`). The retained
  console buffer also contains historical setup/test failures; it was not cleared.
- Final Editor state: stopped, not compiling, `SinkLab.unity` loaded and clean,
  time scale restored to 1 (`final-editor-state.json`).

Scoped saved-file hashes are recorded in `verified-assets.sha256`.

## Limits

This prototype does not establish arbitrary sink scale/tilt or simultaneous-player
support. Native collision tests establish conditional rebound at hard impact; a
human playtest is still needed to judge preferred bounce strength and difficulty.
No standalone player build, push, or merge into the original main branch was requested.
