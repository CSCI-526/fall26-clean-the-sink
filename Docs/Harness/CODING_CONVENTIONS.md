# Human-readable code

The user requires code that human engineers can return to, understand, and maintain
with the agent. Treat readability as part of correctness for delivery. Existing
compressed code is technical debt, not a style to copy. Never optimize source for
fewer lines, tokens, or tool calls at the expense of a maintainer.

## Scope

Apply these rules to new and modified methods in project-owned code, including
editor tools and test/audit code. Expand a compressed method when changing it, but
keep cleanup local to the task. Do not reformat unrelated files or generated/vendor
code. For other languages, use their normal readable conventions.

## C# formatting and names

- Use four-space indentation, braces on separate lines, and normal spacing around
  operators and after commas. Put control-flow bodies in braces, including short
  `if`, `else`, and loop bodies.
- Use one executable statement per line and one field or local declaration per
  line. A conventional `for` header is fine. Do not pack initialization, loops,
  conditions, state changes, or cleanup onto one line.
- Wrap long expressions and argument lists at meaningful boundaries; aim for about
  120 columns. Separate logical stages with blank lines. Simple expression-bodied
  properties or methods are fine when they express one clear operation.
- Use PascalCase for types, methods and properties; camelCase for parameters and
  locals; and `_camelCase` for new private instance fields. Preserve existing public
  API and serialized field names unless a deliberate migration is part of the task.
- Prefer names that identify purpose, such as `streamRenderer`, `impactPoint`,
  `pressureWidthScale`, or `lastProgressTime`. Short names are appropriate only in
  small conventional loop/math scopes. Include units or coordinate space when
  ambiguous, such as `timeoutSeconds`, `localDrainCenter`, or `worldSurfaceHeight`.
- Name non-obvious tuning values and thresholds. Explain their units and purpose;
  keep related settings together. Avoid nested ternaries for branching behavior.

The root [.editorconfig](../../.editorconfig) supplies supported C# editors with
formatting defaults. It does not rename fields, automatically reformat the project,
or prove readability. Use a formatter on the relevant changes when available and
review its diff; do not install a formatter solely for a small task.

## Responsibilities and explanation

- Give each method one coherent responsibility. Unity lifecycle methods should
  make execution order easy to scan; move substantial setup/update stages into
  clearly named methods. Avoid arbitrary line-count limits and unnecessary layers.
- Keep coroutine yields, state transitions, early exits, timeouts and failure paths
  understandable. Do not hide consequential work behind generic helper names.
- Keep each class focused on a clear role. When adding a Unity component, use a
  matching script filename. Split unrelated responsibilities when the task warrants
  it; do not create abstractions just to make methods shorter.
- Prefer explicit, readable steps for stateful gameplay/physics code. Use LINQ,
  lambdas or compact expressions when they improve clarity, not to conceal effects.
- Comments explain why: design decisions, ownership, coordinate frames, physical
  assumptions and invariants. Do not narrate obvious assignments. Add concise API
  documentation where the contract, side effects or usage constraints are non-obvious.
- An engineer should not need this chat to discover a rule such as “the gameplay
  audit must use water mechanics and must not directly wash or collect objects.”

## Unity and behavior safety

- Group Inspector settings by purpose and use helpful tooltips for non-obvious
  tuning. Prefer private serialized fields for new Inspector-only settings; expose
  public members when other code needs them.
- Preserve serialized names, component types, script identities, `.meta` GUIDs and
  prefab references. A necessary field rename requires an explicit migration such
  as `FormerlySerializedAs` and saved-reference verification. A naming preference
  alone is not a reason to change an existing public or serialized contract.
- Keep readability cleanup behavior-preserving: retain callback order, coroutine
  timing, random-number sequence, coordinate conversions, physics calls, resource
  cleanup and object ownership. Do not silently retune gameplay during cleanup.
- Make intended behavior changes distinguishable from cleanup in the diff and
  handoff. Use the relevant checks from [VERIFICATION.md](VERIFICATION.md).

## Maintainer review before handoff

Read the changed methods as an engineer returning without conversation history:

1. Are purpose, inputs, outputs and state changes apparent from the names and flow?
2. Can setup, normal operation, failure handling and cleanup be followed without
   unpacking multiple actions on a line?
3. Are units, coordinate spaces and non-obvious tuning explained where needed?
4. Are method/class responsibilities clear without needless indirection?
5. Does the diff preserve behavior and Unity references except for explicitly
   intended changes? Have relevant compilation/behavior checks been completed?

Fix readability problems introduced by the task before completion. Report relevant
remaining debt in touched areas accurately. A passing formatter or test suite does
not replace this review, and no extra user approval is required for routine readable
implementation within the authorized task.
