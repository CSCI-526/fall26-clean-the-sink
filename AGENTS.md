# Project agent harness

Read [Docs/Harness/README.md](Docs/Harness/README.md) at task start. It routes to
only the instructions needed for the commission. User instructions take precedence
over this project's defaults; do not turn historical implementation details into
new product requirements.

## Start and scope

- Read the request, current diff, and [project contract](Docs/Harness/PROJECT.md).
  Preserve unrelated work and identify the actual scene/prefab before changing it.
- For concept/alignment-only requests, speak back the experience and stop before
  engine implementation until the user confirms. Explicit fixes/implementation
  requests authorize their bounded work; do not restart an already-settled approval loop.
- State the outcome, acceptance checks, and relevant verification profile. Use the
  [commission template](Docs/Harness/COMMISSION.md) for substantial work; a short
  equivalent in the task is enough for a small fix.
- Use `./Tools/harness check` for offline project integrity. Use
  [WORKFLOW.md](Docs/Harness/WORKFLOW.md) for execution and recovery.

## Unity access — mandatory

- Run every Unity CLI invocation through `./Tools/unity` with
  `sandbox_permissions: "require_escalated"` in Codex, for **host process inspection
  and access to the local Editor server**. This includes status and discovery.
- Never retry the raw `unity` executable inside the sandbox. The installed CLI can
  mistake `EPERM` for a dead Editor and delete its discovery file. The launcher
  refuses restricted execution before starting the CLI; do not bypass that guard.
- Before editing scenes, GameObjects, prefabs, or assets, run
  `./Tools/harness preflight --json` (also with host access). It checks `unity status`
  through the launcher and the current project's `editor_status`.
- Discover commands with `./Tools/unity command --format json`; command names and
  parameters come from the connected Editor. Drive a reachable Editor live.
  Never hand-edit `.unity`, `.prefab`, or `.asset` YAML while it is reachable.
- If an Editor is running but unreachable, diagnose with
  `./Tools/unity pipeline list --format json`; rule out Safe Mode before fallback.
  Do not fabricate discovery credentials, patch package caches speculatively,
  discard unsaved work, or kill Editors by name.
- After a domain reload, allow transient unavailability and check `editor_status`
  is ready. A timeout does not prove a mutation was cancelled: inspect its effects
  before retrying. Read-only checks may be retried after readiness.

## Authoring and evidence — mandatory

- Author reusable player, sink, wall, stain and food prefabs; compose them beneath
  meaningful level/assembly parents. Keep shared edits in their source prefabs.
  Preserve nested connections and use intentional overrides/variants.
- Keep the drain rim one continuous hollow mesh in its reusable prefab. Do not
  recreate a collection of manually arranged lip objects. The decorative rim and
  the physical drain opening are distinct; validate both when either changes.
- Keep gameplay ownership local to a level, and bind reusable parts through that
  level. Avoid rebuilding authored content at runtime or globally affecting other levels.
- Select checks from [VERIFICATION.md](Docs/Harness/VERIFICATION.md). Test the saved
  deliverable and production controls where relevant; do not repair fixtures in a
  way that conceals broken saved assets or bypass water mechanics to claim a playthrough.
- Parse outer and nested command success. `./Tools/harness result FILE --tests`
  rejects failed, incomplete, empty, skipped or inconsistent test reports.
  Historical passing evidence is not a fresh run or proof of visual quality.
- Inspect actual rendered results for visible changes. Report what changed, what
  was tested, remaining limits, and the verified final state. Do not call the work
  complete with required checks unresolved.

## Human-readable source — mandatory

- Read [CODING_CONVENTIONS.md](Docs/Harness/CODING_CONVENTIONS.md) before source
  changes. Engineers must be able to understand and maintain the code without this chat.
- Write normally formatted, descriptive code: one statement per line, clear names,
  braced control flow, and methods with coherent responsibilities. Do not compress
  code to save lines or tokens, or copy the style of existing compressed methods.
- Apply these conventions to new and modified methods. Keep cleanup within the task;
  do not turn a small fix into an unrelated project-wide refactor.
- Preserve Unity serialization, references, lifecycle order and gameplay behavior
  during readability cleanup. Separate intentional behavior changes in the review.
- Review the changed code as a returning engineer before handoff. Formatting tools
  and passing tests do not replace the readability review in CODING_CONVENTIONS.md.

## Working together

Use descriptive branch names without a `codex/` prefix, as requested by the user.
Follow any exact branch name the user supplies.

Assign independent agents bounded files/subtasks. One owner mutates the live
Editor at a time; other agents can review/read independently. Do not overwrite
another agent's or the user's changes. Parallelize safe reads; serialize Editor
mutations, reloads and dependent checks. Keep meaningful progress updates during
long work, and ask only for missing decisions or access that prevents progress.
