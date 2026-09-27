var world = UnityEngine.Object.FindFirstObjectByType<SinkLab.SinkWorld>();
var camera = world.player.viewCamera;
var previousTarget = camera.targetTexture;
var previousActive = UnityEngine.RenderTexture.active;
var target = new UnityEngine.RenderTexture(1600, 1000, 24);
var pixels = new UnityEngine.Texture2D(1600, 1000, UnityEngine.TextureFormat.RGB24, false);
try
{
    camera.targetTexture = target;
    camera.Render();
    UnityEngine.RenderTexture.active = target;
    pixels.ReadPixels(new UnityEngine.Rect(0, 0, 1600, 1000), 0, 0);
    pixels.Apply();
    System.IO.File.WriteAllBytes("Verification/Integration/rendered-saved-scene.png", pixels.EncodeToPNG());
}
finally
{
    camera.targetTexture = previousTarget;
    UnityEngine.RenderTexture.active = previousActive;
    UnityEngine.Object.DestroyImmediate(pixels);
    target.Release();
    UnityEngine.Object.DestroyImmediate(target);
}
return "Rendered the actual saved-scene camera; no IMGUI overlay is included.";
