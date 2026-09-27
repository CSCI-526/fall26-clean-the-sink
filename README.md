# Clean the Sink — playable Web build

Play at https://firemanwolf.github.io/clean-the-sink/.

This branch contains the exported Unity game. The Unity project source lives on `main`.
GitHub Pages publishes the root of `gh-pages` whenever this branch is updated.

## Updating the game

1. Export the scene from Unity as a Web build, with Compression Format set to Disabled,
   or use compression with Decompression Fallback enabled.
2. In a separate checkout of `gh-pages`, replace `index.html`, `Build/`, and `TemplateData/`
   with the complete output of the new build. Include `StreamingAssets/` if generated.
3. Keep `.nojekyll` at the branch root, then commit and push to `gh-pages`.
4. Wait for the Pages deployment in the repository Actions tab, then open the public URL
   and test the game in a desktop browser before submitting.

The initial publication was prepared from the supplied Web export. Its three Brotli
files were losslessly decompressed and the corresponding URLs in `index.html` updated.
No gameplay code was changed.

Do not merge this generated-build branch into `main`.
