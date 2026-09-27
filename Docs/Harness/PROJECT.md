# Project contract and decision authority

## User requirements

These come from the user's instructions in this task history. They remain in force
unless the user changes them. They are not inferred from incidental code values.

| ID | Requirement | Acceptance meaning |
| --- | --- | --- |
| U1 | Start from first principles and align the vision before realizing a new concept in the engine when alignment is requested | Restate the experience, player action, feedback and scope; stop at the requested discussion boundary |
| U2 | Satisfaction comes from using a kitchen faucet to remove food and stains without touching the mess by hand | Water is the player's cleaning tool; success should visibly follow their spraying decisions |
| U3 | First-person/FPS character, camera and controls looking into a large sink | Aiming, movement and faucet handling work through the actual player controls |
| U4 | The current scope is one sink-cleaning prototype, including its shrinking drain and standing water; a larger loop is deferred | Preserve the accepted drain/water mechanics; do not add progression, economy, scoring, a countdown display or a wider game loop without a new request |
| U5 | Individual parts—player, sink, sink wall, stain and food waste—must be reusable prefabs composed into a level | Authoring a new arrangement does not require recreating code-owned primitive objects |
| U6 | Repeated pieces need sensible parents rather than a flat pile of unrelated objects | Move/edit an assembly through its parent; repeated pieces have clear ownership |
| U7 | The drain rim must be a hollow circle rather than many manually arranged drain lips | One continuous annular mesh in a reusable prefab; its center is open |
| U8 | The CLI must be usable consistently; preserve lessons in a harness for later commissions | Use the guarded route, diagnose from evidence, and retain checks that catch known failures |
| U9 | Human engineers must be able to return to the code, understand it, and work with the agent | New and modified methods follow CODING_CONVENTIONS.md and pass its maintainer review; compressed code is existing debt, not a precedent |
| U10 | The class prototype must be silent | Keep `WaterVisuals.enableWaterAudio` off for the submission; retain the optional procedural sound for later use |
| U11 | Leave failure permanence unchanged during this integration | The Fail indicator follows current overflow/sealed states; water draining can clear overflow, and an unused F charge can reopen a sealed drain |

U1 is not a requirement to ask permission before every small fix. The user's explicit
implementation/refactor/fix request authorizes that work. Reconfirm only a genuinely
unsettled design decision, the requested alignment boundary, or an action that needs
new authorization.

## Engineering contracts derived from the accepted implementation

These protect the intended behavior. A commission may intentionally revise one,
but the revised behavior must be explicit and verified.

- **Honest physical progress.** Food moves through water-driven rigidbody motion and
  is collected only after entering the actual drain opening below the floor. Do not
  add hidden collection shortcuts to make a demonstration finish.
- **Occlusion from both viewpoints.** Camera visibility alone is insufficient: the
  held nozzle's water path must also reach the surface. Water cannot wash or push
  through intervening walls/rims in either mode.
- **Completion and reset.** Both food and stains must be finished. Missing/deleted
  objective references do not count as success. Reset restores physical state,
  visuals, progress, water and player state, not merely the counter.
- **Current failure behavior.** While the sink is dirty, overflow or a sealed drain
  displays Fail and prevents spraying. This is not a latched terminal outcome.
  Preserve the recovery behavior in U11 unless a later request changes it.
- **Contact response.** Wall rebound comes from actual collider contact and the
  wall physics material. Spray has no special corner range, occlusion bypass or
  invisible corner-escape force. The existing rising-water shuffle is separate
  and remains part of the standing-water mechanic.
- **Saved assets are the deliverable.** Mesh/collider shape, friction and references
  must survive saving/reloading, not just look right in memory.
- **Prefab ownership.** A reusable part contains its internal references. Level-owned
  links (player/HUD world, hose anchor, drain) are bound by that level. Queries and
  effects exclude other independent levels, including a nested one.
- **Scalable authoring.** `SinkWorld` coordinates authored content. A level builder
  instantiates existing source prefabs and does not regenerate or overwrite shared
  geometry. One-time migration tools are guarded against a second destructive run.
  Static basin geometry stays authored in prefabs during play; water visuals and
  the changing drain aperture may create their own runtime resources.
- **Inheritance.** Shared behavior and geometry belong in source prefabs. Level
  placement/color/mass differences can be intentional overrides; reusable differences
  can be prefab variants. Unpacking or overriding everything defeats reuse.
- **Matching geometry.** Collider shape must match the visible food. Drain collection
  bounds must match the physical aperture in the sink's local frame after moving or
  rotating the assembly. The fixed decorative rim is separate from the circular
  opening, whose renderer and one non-convex MeshCollider share the same mesh.
- **Water coordinates.** Local basin depth determines water volume and capacity.
  Physics comparisons and vortex placement use the transformed world surface
  height, so translating or yaw-rotating a unit-scale sink preserves their alignment.

## Current implementation map — facts, not permanent constraints

| Item | Current location / value |
| --- | --- |
| Unity version | Read `ProjectSettings/ProjectVersion.txt`; currently 6000.3.22f1 |
| Pipeline version | Read `Packages/manifest.json`; currently 0.7.0-exp.1 |
| CLI version at last investigation | 1.0.0-beta.5; query the installed CLI for fresh diagnostics |
| Playable scene | `Assets/SinkLab/Scenes/SinkLab.unity` |
| Level asset | `Assets/SinkLab/Prefabs/Levels/SinkLevel.prefab` |
| Reusable parts | `Assets/SinkLab/Prefabs/{Player,Sink,Mess,Environment}` |
| Level coordinator | `Assets/SinkLab/Runtime/SinkWorld.cs` |
| Sink bindings | `Assets/SinkLab/Runtime/SinkAssembly.cs` |
| Continuous ring | `Assets/SinkLab/Prefabs/Sink/Parts/DrainRim.prefab` and `Assets/SinkLab/Meshes/DrainRim.asset` |
| Rounded basin | Nested `Sink/Parts/RoundedBasinCorners.prefab`; saved meshes under `Assets/SinkLab/Meshes/RoundedBasin` |
| Circular opening | `Assets/SinkLab/Meshes/DrainOpening.asset`, referenced by the renderer and static non-convex MeshCollider in `Drain.prefab` |
| Wet friction asset | `Assets/SinkLab/Materials/Wet food.physicMaterial` |
| Wall rebound | `Assets/SinkLab/Materials/Basin wall rebound.physicMaterial`; bounciness 0.4, Maximum combination; global bounce threshold remains 2 m/s |
| Example layout | 8 scraps, 6 stains, four walls; example-scene tests currently assert some counts |
| Drain behavior | Circular collection below the physical opening; time/scrap shrink, E plug, one F full-open charge, R reset; runtime meshes change the opening while preserving the saved source mesh |
| Water model | Raycast jet and standing-water level, buoyancy, wet friction, rising-water shuffle and drain vortex; particles/lines provide visuals; no full fluid simulation |
| Audio | Default-off `WaterVisuals.enableWaterAudio`; silent class submission |
| Geometry authoring | `Assets/SinkLab/Editor/SinkGeometryAuthoring.cs`; static geometry is saved before play, not rebuilt by `SinkWorld.Awake` |

Do not freeze the example counts, ring tessellation, dimensions, colors, exact tuning,
number of prefabs, exact aperture dimensions or current test totals as universal design rules.
If a requested change touches them, update the corresponding example-scene tests and
geometry together while retaining the user requirement and physical intent.

## Expected hierarchy

```text
Sink Level
  Player                        (reusable player prefab)
  Sink                          (assembly prefab)
    Floor / Walls / Rim          (grouped repeated parts)
    Counter / Cabinet            (grouped repeated parts)
    Rounded corners             (nested reusable prefab)
    Drain                       (prefab with fixed continuous rim, circular opening and plug)
    Faucet                      (prefab)
  Mess
    Food                        (individual food prefab instances)
    Stains                      (individual stain prefab instances)
  Environment
    Room / Lighting             (reusable assemblies)
```

`Sink/Rim` is the large sink-top border; `Sink/Drain/Rim` is the small round drain
rim. Resolve the target before editing either. Prefer one active player for an
ordinary level; level isolation is supported but multiple player input ownership is
not a new gameplay promise.

Reuse targets translation and yaw at unit assembly/group scale. Water surface,
buoyancy and drain behavior have corresponding tests; consult fresh test results
before claiming that a revision passes them. Arbitrary scaling, tilt, and multiple
simultaneous players are outside the established authoring scope. Verify and extend
those contracts if a commission introduces that behavior.
