# Verification by change scope

Choose checks that can falsify the change's acceptance criteria. Passing all existing
tests does not prove a new visual or architectural requirement. Additional suites
are justified by affected behavior, not by habit.

## Verification matrix

| Commission scope | Checks before claiming completion |
| --- | --- |
| Documentation / harness instructions | Offline harness check; links and examples reviewed; no Unity suite needed |
| Source code, including changes covered by other rows | Maintainer review in CODING_CONVENTIONS.md, formatting/diff check, and checks appropriate to the affected behavior; compile changed C# in Unity. Formatting-only work does not require new tests |
| Harness or CLI tooling | Offline check; harness-tool tests; negative/error-path checks; live preflight only if live routing changed. For discovery fixes, prove descriptor preservation and check idle/reload/test transitions relevant to the failure |
| Appearance-only material or mesh | Live source/instance inspection, save/reload or reimport check, fresh rendered view inspected. Add relevant contracts if references/colliders/geometry behavior also changed |
| Prefab hierarchy / ownership / serialization | EditMode assembly, connected-source and override inspection, saved-scene check, translated/rotated or duplicate-level check when affected; inspect scene/Game view |
| Water, food, drain, completion or reset | Relevant EditMode contracts and a real saved-scene playthrough through production mechanics; PlayMode controls if interaction changed |
| Camera, movement, input or nozzle | PlayMode controls, relevant occlusion/collision contracts, actual viewpoint/nozzle check and playable saved-scene demonstration |
| Scene/layout or physics tuning | Relevant saved-scene contracts plus a real playthrough; rendering and reachability of intended targets |
| Build/export request | Relevant implementation checks plus an actual build for the requested target and launch validation where available; editor playability is not an exported build |

Code readability is an acceptance requirement. Before completion, inspect the
changed methods using [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md). A successful
compile, formatter, or offline integrity check does not establish understandable
names, sensible responsibilities, or clear control flow. Do not add tests solely
to enforce whitespace or mirror method structure.

## Executable tooling checks

```sh
./Tools/harness check --json
python3 -m unittest discover -s Tools/tests -v
# Use host access in Codex for live calls:
./Tools/harness preflight --json
```

`check` is static integrity only. `preflight` is current Editor readiness only. Neither
runs the game, changes assets, or establishes that a commission is complete.

## Existing Unity tests

| Suite | What it establishes | Current historical result |
| --- | --- | --- |
| `SinkLab.EditModeTests` | Water-off behavior, force/washing, occlusion with positive controls, completion/reset, circular drain geometry and radius overrides, saved rounded geometry, level scope, friction, shape/collider match, prefab inheritance | 41 passed on 2026-09-27; see Verification/Integration/RESULTS.md |
| `SinkLab.PlayModeTests` | Actual input injection, E/F/R, real nozzle washing, native hard/gentle wall and floor contacts, wet materials, moved-sink buoyancy/surface/vortex, drain outflow and reset | 16 passed on 2026-09-27; see Verification/Integration/RESULTS.md |

These totals are snapshots. Use the current discovered suite and actual results,
not a target of exactly 33 or 39 forever. A changed example layout may require
intentional fixture updates; a fixture must not rebuild a broken saved asset.

Discover the commands first. The currently installed Pipeline supports:

```sh
./Tools/unity command run_tests --mode editor --filter SinkLab.EditModeTests --filter_type assembly --async_tests true --format json
./Tools/unity command test_status --format json
# Select only when input/play behavior is affected:
./Tools/unity command run_tests --mode playmode --filter SinkLab.PlayModeTests --filter_type assembly --async_tests true --format json
```

Run one test session at a time. Poll completion at a reasonable interval and keep the
user informed. Redirect each response to a commission-specific JSON file when evidence
is needed. `test_status` currently returns JSON encoded inside `data.result`; validate
both layers:

```sh
./Tools/harness result Verification/<commission>/tests-result.json --tests --json
```

`--tests` requires a completed, nonempty run with every reported test passing and
no failures, skips or inconclusive results. An intentional skip must be reported
as a limitation and justified for the task; it does not become a green pass.
Without `--tests`, result validation checks command success only.

## Checks not fully covered by today's automated tests

- **Drain ring:** inspect that `DrainRim.prefab` still contains one continuous hollow
  mesh, not a collection of lips. Saved-geometry tests check its renderer count and
  nested source, but do not establish visual quality. Verify an open center, closed
  circumference, material and absence of unintended colliders. Inspect a close-up.
- **Every nested part's source:** existing architecture tests cover selected assets
  and groups, not every Floor/Rim/Counter/Cabinet/Drain/Faucet source connection.
  Inspect the affected source chain and overrides live.
- **Assembly transforms and players:** reuse checks cover translation/yaw and level
  ownership, not arbitrary scale/tilt or multiple simultaneous players. Keep unit
  assembly/group scale as the current default and verify any requested expansion.
- **Subjective satisfaction:** an automated clear proves mechanical reachability,
  not that pressure, resistance, audio or cleanup feels satisfying to a human.
- **Visual/physics agreement:** the opening now shares one circular-aperture mesh
  between its renderer and non-convex collider. The decorative rim stays fixed.
  If changing the aperture, keep the floor, collision and collection bounds aligned;
  test corners/depth in local space and inspect shrinking and reset states.

When a future commission changes these areas, add a meaningful regression where
it can catch a repeat failure. Do not add tautological tests that merely mirror a
mesh generator or assert the test fixture's own repairs.

## Credible gameplay evidence

For a playthrough claim, use the actual saved scene, player/camera/nozzle, modes and
pressure. Do not teleport food, inject separate forces, directly wash stains, mark
items drained, or bypass obstacles. Unit tests can control state to isolate a
contract; they must not be presented as a successful playthrough.

`Assets/SinkLab/Runtime/SinkGameplayAudit.cs` is an existing editor-only playthrough
driver. It uses production mechanics. Its past run used 2× time scale; record such
conditions and do not present simulated duration as a human completion time.

For visible changes, capture the actual Scene/Game view using a discovered Pipeline
screenshot command and inspect it. Game camera renders may omit IMGUI; if judging
the HUD, use a capture method that actually includes that overlay. Never substitute
a conceptual mockup for an engine result.

## Evidence and final state

Each verification report should state the commission, date, scene/prefab, relevant
versions, changes verified, exact checks and outcome, captured images, remaining
limits, and final Editor state. Record a revision or scoped file hashes when helpful.
A report from a previous commission is baseline evidence only. If files changed
after verification, repeat affected checks rather than relabeling old evidence.

Tests must isolate their physics/input/scene state, preserve the user's work and
restore global state even on setup failure. Empty worlds cannot prove cleanup.
Pair negative occlusion/ownership tests with positive controls so a system doing
nothing cannot pass by accident.
