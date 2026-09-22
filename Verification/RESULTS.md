# Prefab refactor verification

Verified in Unity 6000.3.22f1 on September 22, 2026. The playable scene is `Assets/SinkLab/Scenes/SinkLab.unity`; its connected level prefab is `Assets/SinkLab/Prefabs/Levels/SinkLevel.prefab`.

| Check | Result | Evidence |
| --- | --- | --- |
| Mechanics, saved-scene and prefab contracts | 33 passed, 0 failed, 0 skipped | `contracts-result.json` |
| Production keyboard/mouse PlayMode tests | 6 passed, 0 failed, 0 skipped | `controls-result.json` |
| Complete saved-scene gameplay run | All 6 stains washed and all 8 scraps physically drained | `gameplay-audit.json` |
| Actual Game View | Inspected fresh layout and completion feedback | `gameview-start.png`, `gameview-complete.png` |

## Authoring structure

There are 17 reusable prefab assets. The level groups `Player`, `Sink`, `Mess`, and `Environment`. The nested `Sink` assembly groups its repeated parts beneath `Floor`, `Walls`, `Rim`, `Counter`, and `Cabinet`, with separate `Drain` and `Faucet` prefabs. `Mess` groups individually instanced food and stains. The room and lighting are separate assemblies.

The seven architecture tests verify asset existence, connected nested sources, grouping, missing scripts/references, standalone player dependencies, persisted level bindings, adding mess, exclusion of nested independent levels, and inheritance of common wall collider/player movement properties. Shared properties are checked against their original prefab source and must have no instance overrides.

Four reuse contracts cover translated/rotated drain geometry and rejection of another level's food/stains, with positive controls proving the owner's water still affects its own mess. The prior mechanics and saved-scene contracts continue to cover occlusion, water-off behavior, gradual washing, physical force, completion/reset, persistent wet friction, and matching colliders.

`SinkWorld` now coordinates the authored level and contains no primitive or material factory. Creating a level instantiates the existing level prefab without overwriting any source prefab. The README documents shared editing, intentional placement overrides and adding parts without code.

## Gameplay evidence

The editor-only audit used actual player movement, camera/nozzle aim, mode changes, pressure and water impulses. It never teleported food, added its own forces, directly washed stains or marked food as drained. The run completed in 83.32 simulated seconds at 2× time scale. This demonstrates completion through the existing mechanics; it is not a human difficulty or satisfaction measurement.

The scene was reset after verification and left stopped with eight food scraps and six stains ready for Play. `verified-assets.sha256` records the verified source, prefab, scene and material files.

## Preservation and logs

The original scene, including unsaved state at migration time, is preserved at `Assets/SinkLab/Scenes/Backups/BeforePrefabRefactor.unity`. Previous verification evidence is retained under `iterations/before-prefab-refactor`. The first refactor test run is retained as `iterations/prefab-fixture-first-run.json`; its two failures were corrected test fixtures (asset display name and prohibited reparenting of a nested prefab child), not gameplay failures.

The final console capture contains the existing Unity MCP plugin's startup error about spaces in the project path. It contains no gameplay exception. Bridge calls, all 39 tests and the complete gameplay run succeeded.
