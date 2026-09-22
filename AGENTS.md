# Unity CLI access

Run Unity CLI from the project root through `./Tools/unity`, with
`sandbox_permissions: "require_escalated"`. The reason is **host process inspection
and access to the local Unity Editor server**. This applies to status, discovery,
commands, and other CLI invocations.

Never run the raw `unity` executable inside the sandbox, including as a retry.
Restricted process checks can return `EPERM` for a live Editor; the installed CLI
then incorrectly deletes the Editor's discovery file. The wrapper checks process
visibility before starting the CLI and refuses unsafe execution. It does not read
or recreate authentication tokens.

Follow the Unity CLI workflow supplied by the user:

- Run `./Tools/unity status --format json` before editing scenes, GameObjects,
  prefabs, or assets.
- Discover the connected Editor's commands using
  `./Tools/unity command --format json`.
- Drive a reachable Editor with live commands; do not hand-edit Unity asset YAML.
- If the Editor is running but unreachable, use
  `./Tools/unity pipeline list --format json` to check Safe Mode and diagnose the
  connection before choosing a recovery action.
- Parse JSON results using the `success` field, including nested command results.
- After a script-domain reload, allow temporary unavailability and verify
  `./Tools/unity command editor_status --format json` reports ready before
  continuing. A command timeout does not prove a mutation was cancelled; inspect
  its effects before retrying. Read-only checks may be retried after readiness.

This routing fix requires no game changes or Pipeline package modifications.
