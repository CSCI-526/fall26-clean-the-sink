# Commission brief and handoff template

Copy into the task or `Docs/Commissions/<name>.md` for substantial work. Keep the
same information in a short message for a small fix; filling a file is optional.
Replace placeholders, remove irrelevant sections, and keep one source of truth.

## Brief

- **Requested outcome:** What the player or level author should experience after this change.
- **Authorization/alignment:** Concept discussion only / explicitly authorized implementation / specific unresolved design decision. Cite the user's decision; do not request approval already granted.
- **Scope and deferred work:** What this commission covers; what belongs to a later request.
- **Acceptance evidence:** Concrete behavior, authoring or visual checks that would show success or expose failure.
- **Relevant contracts/lessons:** IDs from PROJECT.md and affected LESSONS.md entries.
- **Current state:** Scene/prefab paths, existing diff, dirty Editor state, known baseline failures.
- **Ownership:** Files/subtasks per agent; one live Editor mutation owner.
- **Verification profile:** Applicable VERIFICATION.md row(s), tests and live inspections; why others are unnecessary.
- **Preservation:** Any state/asset backup needed for the specific operation, not a generic approval gate.

## Implementation notes

- Shared source prefabs and intentional instance overrides changed.
- References/ownership/physics/serialization implications.
- Unsettled decisions or new findings that change acceptance or scope.
- Recovery actions and their evidence, if relevant.

## Handoff

- **Result:** Concrete before/after behavior or authoring improvement.
- **Files/assets:** Paths the user or next agent should inspect.
- **Checks actually run:** Command/suite, completed result, evidence path and relevant version/revision.
- **Visible evidence:** Actual engine render and what was inspected, if appearance changed.
- **Not established:** Unrun checks, coverage gaps, intentional skips or remaining limits.
- **Final state:** Saved target, Play mode, temporary settings restored, any outstanding work.
- **Harness update:** Only a new user decision or reproduced recurring failure that warrants a changed check/rule.

## Acceptance review

1. Does the outcome match the user's request and its alignment boundary?
2. Can the author reuse/edit the affected parts through their prefab and parent structure?
3. Do saved assets and actual player interaction behave as claimed?
4. Are relevant failures ruled out by meaningful checks, including uncovered visual requirements?
5. Does the handoff distinguish current evidence from historical evidence and avoid overstating completion?
