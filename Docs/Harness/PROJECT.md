# Project contract and decision authority

## User requirements

These come from the user's instructions in this task history. They remain in force
unless the user changes them. They are not inferred from incidental code values.

| ID | Requirement | Acceptance meaning |
| --- | --- | --- |
| U1 | Start from first principles and align the vision before realizing a new concept in the engine when alignment is requested | Restate the experience, player action, feedback and scope; stop at the requested discussion boundary |
| U2 | Satisfaction comes from using a kitchen faucet to remove food and stains without touching the mess by hand | Water is the player's cleaning tool; success should visibly follow their spraying decisions |
| U3 | First-person/FPS character, camera and controls looking into a large sink | Aiming, movement and faucet handling work through the actual player controls |
| U4 | The current scope is the sink-cleaning interaction; ultimate goals and a larger loop are deferred | Do not add progression, economy, scoring, timers or a wider game loop without a new request |
| U5 | Individual parts—player, sink, sink wall, stain and food waste—must be reusable prefabs composed into a level | Authoring a new arrangement does not require recreating code-owned primitive objects |
| U6 | Repeated pieces need sensible parents rather than a flat pile of unrelated objects | Move/edit an assembly through its parent; repeated pieces have clear ownership |
| U7 | The drain rim must be a hollow circle rather than many manually arranged drain lips | One continuous annular mesh in a reusable prefab; its center is open |
| U8 | The CLI must be usable consistently; preserve lessons in a harness for later commissions | Use the guarded route, diagnose from evidence, and retain checks that catch known failures |

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
- **Saved assets are the deliverable.** Mesh/collider shape, friction and references
  must survive saving/reloading, not just look right in memory.
- **Prefab ownership.** A reusable part contains its internal references. Level-owned
  links (player/HUD world, hose anchor, drain) are bound by that level. Queries and
  effects exclude other independent levels, including a nested one.
- **Scalable authoring.** `SinkWorld` coordinates authored content. A level builder
  instantiates existing source prefabs and does not regenerate or overwrite shared
  geometry. One-time migration tools are guarded against a second destructive run.
- **Inheritance.** Shared behavior and geometry belong in source prefabs. Level
  placement/color/mass differences can be intentional overrides; reusable differences
  can be prefab variants. Unpacking or overriding everything defeats reuse.
- **Matching geometry.** Collider shape must match the visible food. Drain collection
  bounds must match the physical aperture in the sink's local frame after moving or
  rotating the assembly. A decorative mesh does not silently redefine physics.

## Current implementation map — facts, not permanent constraints

| Item | Current location / value |
| --- | --- |
| Unity version | Read `ProjectSettings/ProjectVersion.txt`; currently 6000.3.22f1 |
| Pipeline version | Read `Packages/manifest.json`; currently 0.7.0-exp.1 |
| CLI version at last investigation | 1.0.0-beta.5; query the installed CLI for fresh diagnostics |
| Playable scene | `Assets/SinkLab/Scenes/SinkLab.unity` |
| Level asset | `Assets/SinkLab/Prefabs/Levels/SinkLevel.prefab` |
| Reusable parts | `Assets/SinkLab/Prefabs/{Player,Sink,Mess,Environment}`; currently 18 prefabs |
| Level coordinator | `Assets/SinkLab/Runtime/SinkWorld.cs` |
| Sink bindings | `Assets/SinkLab/Runtime/SinkAssembly.cs` |
| Continuous ring | `Assets/SinkLab/Prefabs/Sink/Parts/DrainRim.prefab` and `Assets/SinkLab/Meshes/DrainRim.asset` |
| Wet friction asset | `Assets/SinkLab/Materials/Wet food.physicMaterial` |
| Example layout | 8 scraps, 6 stains, four walls; example-scene tests currently assert some counts |
| Drain physics | Square basin aperture and square collection bounds; the new round rim is decorative and collider-free |
| Water model | Raycasts and surface impulses, with particles/lines and generated audio; no full fluid simulation |

Do not freeze the example counts, ring tessellation, dimensions, colors, exact tuning,
number of prefabs, square aperture or current test totals as universal design rules.
If a requested change touches them, update the corresponding example-scene tests and
geometry together while retaining the user requirement and physical intent.

## Expected hierarchy

```text
Sink Level
  Player                        (reusable player prefab)
  Sink                          (assembly prefab)
    Floor / Walls / Rim          (grouped repeated parts)
    Counter / Cabinet            (grouped repeated parts)
    Drain                       (prefab, containing its one continuous ring)
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

Reuse is currently verified under translation and yaw. Unit assembly/group scale
is the current authoring default; arbitrary scaling, tilt, and multiple simultaneous
players are not established by the existing tests. Verify and extend those contracts
if a commission introduces that behavior.
