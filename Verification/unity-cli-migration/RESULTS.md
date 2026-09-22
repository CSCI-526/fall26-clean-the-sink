# Unity CLI migration — 2026-09-22

Configured the existing official Unity CLI 1.0.0-beta.5 and installed Unity Pipeline 0.7.0-exp.1 in this Unity 6000.3.22f1 project. Installed the official Codex CLI instructions in the user profile for reuse across projects.

Removed AI Game Developer MCP 0.91.0, its unused PlayerPrefsEx dependency and OpenUPM registry, 42 imported DLLs, 77 generated MCP skills, project-local Node CLI bridge, MCP settings, scripting defines, and caches. Updated the project README.

Verification passed: CLI reports the Editor ready; recompilation completed with no compiler errors; live C# execution successfully read the loaded SinkLab scene. Static checks confirm the old integration is absent. Existing gameplay tests were not rerun for this tooling migration. Unity-generated YAML uses its normal trailing spaces on empty values.

The CLI is installed once per computer/user and reused across Editor versions. Each project needs its own Pipeline package for live Editor control (Unity 6.0+), which can be included in a project template. Both the CLI and Pipeline currently use prerelease versions.

Recoverable removed files are outside the project in the private temporary backup recorded in backup-location.txt; those temporary files are not a permanent backup.
