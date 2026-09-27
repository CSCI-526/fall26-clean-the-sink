# Project harness verification

Verified 2026-09-22. This commission added project instructions and read-only tooling; no game source, prefab, scene or package changes were made.

## Deliverables

- Root `AGENTS.md` routes future commissions and preserves the mandatory guarded Unity CLI workflow.
- `Docs/Harness/` contains the entry point, user/engineering contract, workflow and recovery, scoped verification matrix, proven failure lessons, and reusable commission template.
- `Tools/harness` supplies offline integrity checks, read-only live readiness, and saved CLI/test-result validation. `Tools/unity` remains the sole CLI invocation path and was not changed.
- `Tools/tests/test_harness.py` exercises false-success/error cases and metadata failures. README links the harness; `.gitignore` excludes generated Python caches.

## Checks executed

| Check | Outcome | Evidence |
| --- | --- | --- |
| Harness tool regression tests | 11 passed | `tool-tests.txt` |
| Real project offline integrity | Passed | `static-check.json` |
| Live preflight through guarded host route | Passed | `live-preflight.json` |
| Sandboxed preflight | Correctly refused with exit 77; descriptor preserved byte for byte | `sandbox-guard.json` |
| Historical successful Pipeline test response | Accepted structurally | `Verification/CliReliability/tests-result.json` used as input, not a fresh gameplay run |
| Historical failed Pipeline command | Correctly rejected with exit 1 | `Verification/CliReliability/final-command.json` used as a negative input |
| Instruction Markdown links | All links in root AGENTS and six harness documents resolve | Local link check and independent document review |
| Source whitespace/diff check | Passed | `git diff --check` |

## Deliberate limits

No Unity gameplay suites were rerun: this commission changes instructions and Python tooling, not game behavior. The live preflight checks readiness, not subjective feel, scene correctness, or a build. Result validation does not establish freshness or appropriate test selection.

The harness explicitly records coverage gaps for ring topology, some nested prefab source chains, arbitrary transforms/multiple players, and subjective satisfaction. Those require scoped live checks or future meaningful regressions when the affected area changes. Current test counts, food/stain counts, geometry dimensions and prerelease tool versions are labeled as snapshots rather than permanent design rules.

The harness is project-local. It does not install global instructions, set up recurring jobs, or override the user's subsequent decisions. It adds no blanket approval gate to an already-authorized commission.
