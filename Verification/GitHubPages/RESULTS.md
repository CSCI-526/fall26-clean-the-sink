# GitHub Pages publication — 2026-09-27

## Published result

- Game: https://firemanwolf.github.io/clean-the-sink/
- Repository: https://github.com/Firemanwolf/clean-the-sink
- Publication branch: `gh-pages`, root `/`, HTTPS enforced.
- Commit: `c20228e` — Publish Clean the Sink Web build for GitHub Pages.
- Successful deployment: https://github.com/Firemanwolf/clean-the-sink/actions/runs/36358458513
- The user explicitly selected making the existing project repository public.

## Preparation and publication

The supplied export was `/Users/lilyxu/Desktop/Clean The SInk/`.
Its data, framework and WebAssembly files used Brotli compression without a
decompression fallback. A separate copy was prepared under `Builds/GitHubPages/`:
the three `.br` files were losslessly decompressed, their URLs in `index.html`
were updated, and an empty `.nojekyll` file was added. The original export and
Unity gameplay source were not changed.

The build was committed in a managed, separate checkout on the orphan `gh-pages`
branch, then pushed to the same GitHub repository. GitHub automatically configured
Pages to publish this branch. The Pages API verified the source, HTTPS setting and
completed deployment. GitHub's built-in workflow deploys later pushes to this branch.

The first Git push failed with HTTP 400 and did not create the remote branch.
Retrying with HTTP/1.1 and a request buffer large enough for the build succeeded.
These were per-command options; no global Git settings were changed.

## Fresh verification

- Offline project integrity: `./Tools/harness check --json` passed before and after publication.
- Decompressed WebAssembly passed `WebAssembly.validate`.
- Anonymous HTTPS requests returned HTTP 200 for `index.html`, loader, framework,
  data, WebAssembly and stylesheet. Every downloaded file matched the prepared
  copy by SHA-256. The WebAssembly response used `application/wasm`.
- The public URL rendered the actual sink scene in desktop Chrome.
- Visible control checks: Q changed JET to SHOWER; E closed the drain; water and
  food responded during spraying; Escape released gameplay control; R restored
  water to 0%, drain to 100%, and the initial food/stain counts.
- Screenshot: [live-game.png](live-game.png), taken from the public URL after reset.

## Limits and final state

Automated mouse capture caused Chromium pointer-lock errors in the controlled
browsers (`UnknownError` in the in-app browser and `WrongDocumentError` in Chrome).
The game continued rendering and responding to keyboard controls after dismissing
the alert. Full mouse aiming and a complete cleaning playthrough remain unverified;
check them manually in a normal desktop browser before submission. No error
suppression or gameplay workaround was added.

No Unity build or engine test suite was run: this commission published the supplied
export. The existing local change to `ProjectSettings/ProjectSettings.asset` was
preserved. The main checkout remains on `main`. The publication checkout is retained
for later build updates, with update instructions in its `README.md`.
