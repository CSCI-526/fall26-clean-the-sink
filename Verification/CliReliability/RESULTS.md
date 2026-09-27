# Unity CLI connection reliability

Verified on 2026-09-22 with Unity CLI 1.0.0-beta.5, Pipeline 0.7.0-exp.1, and Unity Editor 6000.3.22f1.

## Confirmed cause

Codex's restricted shell cannot inspect the running Unity Editor process: both a signal-zero PID probe and `ps` return permission errors. With host permissions, the same PID is alive and identified as Unity. The installed CLI treats the restricted process check as a dead instance and deletes `Library/Pipeline/.unity-pipeline-port` during discovery. Pipeline remains listening, but the CLI loses the file needed to locate and authenticate to it.

A disposable project descriptor reproduced the deletion without using the live descriptor or a real credential. With a fresh timestamp and the same live PID, a sandboxed CLI call deleted the disposable descriptor. The equivalent host-authorized call preserved it and reached the expected connection attempt against the deliberately unused test port. A descriptor with a one-hour-old heartbeat was also preserved under host access. See `reproduction.json`.

The project test code does not delete the descriptor. Pipeline's request-driven heartbeat initially looked suspicious, but the controlled experiment identified process-inspection permissions as the deletion trigger. No speculative Pipeline heartbeat patch was applied.

## Fix

- `Tools/unity` checks process visibility before executing the installed Unity CLI. A restricted invocation exits 77 without starting the CLI or touching discovery files. It forwards arguments directly with `os.execv` and preserves CLI exits and signals.
- Root `AGENTS.md` requires every project CLI invocation to use this launcher with `sandbox_permissions: "require_escalated"` for host process inspection and local Editor access.
- `README.md` documents the checked launcher and recovery reason.

This fixes project invocation and prevents the observed destructive sandbox fallback. It does not patch the installed Unity CLI binary. A raw sandboxed CLI invocation can still reproduce the upstream defect; do not bypass the launcher. Normal transient unavailability during a domain reload is expected.

## Validation

- Restricted launcher call: refused with exit 77, live descriptor preserved byte for byte (`sandbox-guard.json`).
- Host launcher call: Editor ready on port 7800.
- Same 33 EditMode tests that preceded the reported outage: all passed; connection remained ready afterward (`tests-result.json`).
- Recompile command: returned `up_to_date`; no compile was needed (`recompile-start.json`).
- Actual script-domain reload requested through the live Editor: approximately 29 seconds in the Editor log; the same Editor PID returned ready without a manual server restart (`reload-start.json`, `after-reload-status.json`).
- No-request idle interval: 137.8 seconds; descriptor remained present, the same server was still running, and the next status call returned ready (`idle-measurement.json`, `after-idle-status.json`).
- The first read-only evaluation after the reload and idle interval hit Pipeline's 5,000 ms main-thread timeout (`final-command.json`). The descriptor remained present, a subsequent `editor_status` reported ready, and one retry of that read-only evaluation succeeded in under a second without restarting the server (`final-command-retry.json`). The scene was saved and Play mode was off. This was a transient command timeout, not another discovery-file deletion.

No game source, scene, prefab, mesh, or Pipeline package changes were made for this connection fix. Existing rim-task edits were preserved. No connection tokens are included in these artifacts.
