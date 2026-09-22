# Prototype verification

Verified in Unity 6000.3.22f1 on September 19, 2026. The deliverable is `Assets/SinkLab/Scenes/SinkLab.unity`.

| Check | Result | Evidence |
| --- | --- | --- |
| Adversarial EditMode contracts | 22 passed, 0 failed, 0 skipped | `contracts-result.json` |
| Production keyboard/mouse PlayMode tests | 6 passed, 0 failed, 0 skipped | `controls-result.json` |
| Complete saved-scene gameplay run | All 6 stains washed and all 8 scraps physically drained | `gameplay-audit.json` |
| Actual Game View and completion feedback | Inspected; counts, controls and completion readable | `gameview-start.png`, `gameview-complete.png` |

## What the evidence establishes

The gameplay driver uses the production collision-based player movement, camera aim, nozzle, spray modes and pressure. It does not teleport food, apply its own forces, call stain washing, or mark food as drained. It completed the saved scene in 83.13 simulated seconds at 2× time scale. This is an automated completion test, not a measurement of human difficulty or satisfaction.

The contracts check water-off behavior, physical impulses, gradual washing, walls blocking both spray modes with positive controls, valid drain location/height and square corners, completion requiring both kinds of mess, invalid washing inputs, and reset of physics and progress. Three contracts load the actual scene from disk to check persistent wet friction, matching sphere meshes/colliders, and movement of a settled scrap on the real basin under normal water force. The six control tests inject mouse/keyboard events into the actual input system, including cursor release/recapture, reset, movement/collision, aiming, modes/pressure, and washing through the held nozzle from the left side.

## Problems caught and corrected

- The nozzle could strike the rim while the camera saw a stain. Its position was corrected and a real-camera/nozzle regression added.
- The square floor opening and circular drain check disagreed at the corners. Collection now matches the opening and requires the food to fall below the floor.
- Nonuniform sphere meshes did not match their spherical colliders. Sphere food now uses uniform scale.
- The saved wet-food material used the wrong asset extension and lost its low-friction settings. It now uses a native `.physicMaterial` asset, shared by every scrap, with immediate reload validation and saved-scene regression tests.

Earlier failed audit evidence is retained under `iterations/`; the files at this directory's root contain the final results. `verified-assets.sha256` records the exact verified prototype files.

## Limits and editor log notes

Water is approximated with raycasts and surface impulses; food motion uses Unity rigidbody physics. This scope does not include a full fluid simulation, broader progression, or an exported standalone build. The prototype is ready to play in the Unity Editor.

The final log capture (`final-console.json`) contains no gameplay exception. It does contain the existing Unity MCP plugin's startup error about spaces in the project path, plus editor warnings about material display names differing from their numbered filenames. The bridge calls, all tests, scene rendering, and complete gameplay run succeeded with those messages present.

The scene was restored to a fresh dirty sink after verification. Open the scene and press Play; the controls are also listed in the project README and in the Game View.
