# SinkLab agent harness

This is the entry point for future commissions on this project. It turns the
user's decisions, existing contracts, and verified failure lessons into a repeatable
workflow. It is project-local: it does not install a global skill or alter other projects.

## Read the relevant layer

| File | Purpose | Read when |
| --- | --- | --- |
| [Root AGENTS.md](../../AGENTS.md) | Rules automatically visible to repository agents | Every commission |
| [PROJECT.md](PROJECT.md) | Experience, architecture, decision authority, and current facts | Before planning changes |
| [CODING_CONVENTIONS.md](CODING_CONVENTIONS.md) | Human-readable source, Unity refactor safety, and maintainer review | Before source changes and during review |
| [WORKFLOW.md](WORKFLOW.md) | Scope, live Editor access, preservation, collaboration, recovery | Before implementation; on tool failure |
| [VERIFICATION.md](VERIFICATION.md) | Checks by change type and what constitutes evidence | Before implementation and before handoff |
| [LESSONS.md](LESSONS.md) | Past failures, symptoms, prevention and evidence | When touching the affected area |
| [COMMISSION.md](COMMISSION.md) | Reusable brief and handoff template | Substantial commissions; adapt for small fixes |

## What is enforced

| Layer | Enforcement | Limit |
| --- | --- | --- |
| CLI access | [Tools/unity](../../Tools/unity) rejects the observed restricted-process environment before invoking Unity | Direct raw CLI use bypasses it; host access remains required |
| Project integrity | `./Tools/harness check --json` checks file/metadata/config consistency offline | Does not load prefabs or test Unity behavior |
| Editor readiness | `./Tools/harness preflight --json` checks discovery, project identity and Editor readiness via the guarded launcher | Readiness is a snapshot, not permission to overwrite unsaved work |
| Result integrity | `./Tools/harness result report.json --tests --json` validates completion and nested success | Does not establish freshness, correct test selection or subjective quality |
| Game behavior | Existing EditMode/PlayMode contracts plus scoped live inspection/playthrough | Coverage gaps remain explicit in VERIFICATION.md |
| Code readability | AGENTS.md requirements, C# editor formatting defaults, and the maintainer review in CODING_CONVENTIONS.md | EditorConfig support varies; the offline check does not judge readability or enforce formatting |
| Intent and authoring quality | User alignment, reviewer judgment and visible evidence | Cannot be proved by a passing static check |

The harness does not automatically mutate a scene, run all tests, restart Unity,
publish work, or accept its own visual result. A static pass is not a commission pass.

## Normal use

From the project root:

```sh
./Tools/harness check --json
# In Codex, invoke the next commands with require_escalated for host process access.
./Tools/harness preflight --json
./Tools/unity command --format json
```

Choose the smallest verification profile covering the actual change. Validate a
saved test response with:

```sh
./Tools/harness result Verification/<commission>/tests-result.json --tests --json
```

The offline check covers core project files and metadata beneath `Assets/SinkLab`,
including missing/orphan metadata, duplicate GUIDs, expected assemblies, required
core prefabs, and the known physics-material extension mistake. It does not freeze
the total prefab count or tuning values.

The tools require Python 3; the live commands additionally require the installed
Unity CLI and this project's Pipeline package. Run harness-tool tests with
`python3 -m unittest discover -s Tools/tests -v`.

## Keep the harness useful

A new rule needs a reason: an explicit user decision, a reproducible failure, or a
clear project invariant. Record its authority, affected scope, and a check that
would catch recurrence. Prefer strengthening an existing check over adding another
page of prohibitions. Remove obsolete recovery steps when the underlying tool changes.

Latest user decisions may change the project contract. Update affected docs and
contracts together; do not silently weaken a test just to obtain green results.
Historical reports stay historical. Requirements travel with their rationale,
not with a magic test count or a fixed scene layout.

For a different Unity commission, reuse the workflow/template and adapt PROJECT.md,
expected asset paths and tests. Do not copy SinkLab's food counts or geometry as
universal requirements.
