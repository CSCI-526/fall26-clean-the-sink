# Failure lessons and their checks

These are observed project failures or explicit user corrections. They explain
why the harness has rules; they are not a catalog of hypothetical restrictions.

| Failure / symptom | Cause or lesson | Prevention / check | Evidence |
| --- | --- | --- | --- |
| Parts were difficult to reuse and scale | A code-generated assembly was acting as the authoring source | Author individual prefabs, compose a level, keep `SinkWorld` a coordinator; inspect source inheritance | User prefab request; [refactor report](../../Verification/RESULTS.md) |
| Repeated pieces cluttered the hierarchy | No meaningful parent/assembly ownership | Group repeated pieces; move/edit via parent and keep level-owned references local | User parenting correction; [architecture tests](../../Assets/SinkLab/Tests/EditMode/PrefabArchitectureTests.cs) |
| Drain rim was many separate lip objects | Piece placement substituted for one coherent shape | One hollow annular mesh in `DrainRim.prefab`; direct geometry/source inspection and close-up | User rim correction; [ring report](../../Verification/DrainRim/RESULTS.md) |
| A visible stain could not be sprayed from one side | Camera saw the target but the held nozzle struck the rim | Verify both camera and nozzle paths; real-control nozzle regression | [prototype findings](../../Verification/iterations/before-prefab-refactor/RESULTS.md); `VisibleStainCanBeWashedFromLeftSide_WithTheRealHeldNozzle` |
| Food at drain corners was mishandled | Square physical aperture and circular collection check disagreed | Match capture shape/depth to the physical opening in local coordinates; test boundaries | [prototype findings](../../Verification/iterations/before-prefab-refactor/RESULTS.md); drain contracts |
| Sphere food did not match collision | A nonuniform sphere mesh still used a spherical collider | Use matching geometry/colliders; current spheres use uniform scale | Saved-scene contracts and prototype findings |
| Food moved in memory but stuck after save/reload | Wrong material extension/importer lost low-friction settings | Native `.physicMaterial` asset, reload and inspect shared material, exercise real saved basin | [failed friction evidence](../../Verification/iterations/saved-friction-regression-failure.json); saved-scene contracts |
| Reuse risked affecting another level | Scene-global registries/coordinates need level ownership filtering and local frames | Filter through owning `SinkWorld`, bind per level, test moved/rotated and independent levels with positive controls | [refactor report](../../Verification/RESULTS.md) |
| Test fixtures failed during prefab refactor | Asset display name assumptions and prohibited reparenting of a nested prefab child | Test real prefab assets; respect prefab lifecycle; distinguish fixture errors from gameplay bugs | [first refactor run](../../Verification/iterations/prefab-fixture-first-run.json) |
| CLI reported no Editor despite a listening server | Sandboxed PID inspection returned `EPERM`; CLI deleted the live descriptor | Use `Tools/unity` with host access; verify restricted invocation is blocked before raw CLI | [controlled reproduction](../../Verification/CliReliability/reproduction.json) |
| Restart temporarily fixed CLI, then loss recurred | Restart restored the file but did not remove the destructive caller | Prove the deletion trigger and fix invocation; no speculative heartbeat/package patch | [CLI reliability report](../../Verification/CliReliability/RESULTS.md) |
| First evaluation after reload timed out | Observed 5-second main-thread timeout; descriptor stayed intact, ready/read retry succeeded | Separate timeout from discovery loss; inspect effects before retrying mutations | [CLI reliability report](../../Verification/CliReliability/RESULTS.md) |
| Passing tests could imply more than they prove | Ring topology and some source chains have no dedicated regression; historical results differ by scope | Explicit coverage gaps, visual checks, scoped current evidence and honest test counts | [verification matrix](VERIFICATION.md) |

## Diagnoses that must not become folklore

The missing discovery file was initially suspected to be a heartbeat issue. A
controlled probe showed deletion depended on restricted process inspection;
a one-hour-old descriptor was preserved outside the sandbox. The fix therefore
changed the invocation route, not Pipeline's heartbeat implementation.

The old Unity MCP bridge was removed during the CLI migration. Its path-with-spaces
startup warning in an older report is historical, not a current dependency or a
reason to reinstall it. See [migration report](../../Verification/unity-cli-migration/RESULTS.md).

The ring refactor preserved a square physical aperture. A circular decorative ring
is not evidence that collection geometry or collision changed. Historical prefab
counts (17 then 18) and test totals reflect different revisions, not contradictory
requirements.

## Add a lesson

Record: observed symptom → demonstrated cause → narrow fix → prevention/check →
evidence → current scope/version. Distinguish a hypothesis from a reproduced cause.
Keep failed checks that explain the lesson, and retire rules when their cause is
actually removed. Do not make every historical workaround mandatory forever.
