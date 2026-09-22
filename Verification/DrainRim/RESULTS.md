# Continuous drain rim

Replaced 24 individual drain lip GameObjects with one nested `DrainRim.prefab` containing one mesh renderer and one mesh filter. The native mesh asset is `Assets/SinkLab/Meshes/DrainRim.asset`; its annular band has an open center, a continuous 96-section circumference and lightly beveled edges.

The change was applied through the live Unity Editor using Unity CLI / Pipeline. The level inherits the nested prefab automatically. The existing chrome material, rim height and footprint are retained. The rim remains collider-free, as the original lips were; the basin aperture and drain collection physics were not changed.

Validation:

- All 33 existing EditMode mechanics, saved-scene and prefab contracts passed (`contracts-result.json`).
- Live hierarchy confirms exactly one rim renderer and no remaining lip objects (`live-inheritance.json`).
- The actual scene close-up was visually inspected (`ring-closeup.png`).
- The saved original drain prefab is retained as `before-Drain.prefab` in this directory.

The source snippet used for the one-time replacement is `Tools/Geometry/ReplaceDrainRim.cs`. It is outside Assets and does not execute during gameplay. The user edits the prefab/mesh normally; the script refuses to recreate an existing ring.

After the successful test run, Pipeline lost its CLI discovery file again. An optional selection refresh could not run; the saved prefab change, live inheritance inspection and rendered close-up had already succeeded. The diagnostic is retained in `post-test-cli-discovery.json`.
