using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace SinkLab.Tests
{
    /// <summary>
    /// Drives the actual production Update methods through virtual input devices.
    /// Scene contracts cover the physics in isolation; these checks cover the human controls.
    /// </summary>
    public sealed class ControlsPlayModeTests
    {
        SinkWorld world;
        Keyboard keyboard;
        Mouse mouse;
        readonly List<InputDevice> disabledDevices = new List<InputDevice>();
        readonly List<GameObject> suspendedWorlds = new List<GameObject>();
        readonly List<SinkPlayer> controlledPlayers = new List<SinkPlayer>();
        InputSettings.UpdateMode oldUpdateMode;
        InputSettings.BackgroundBehavior oldBackgroundBehavior;
        InputSettings.EditorInputBehaviorInPlayMode oldEditorBehavior;
        bool oldRunInBackground;
        float oldTimeScale;
        CursorLockMode oldCursorLock;
        bool oldCursorVisible;
        bool settingsCaptured;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            oldUpdateMode = InputSystem.settings.updateMode;
            oldBackgroundBehavior = InputSystem.settings.backgroundBehavior;
            oldEditorBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            oldRunInBackground = Application.runInBackground;
            oldTimeScale = Time.timeScale;
            oldCursorLock = Cursor.lockState;
            oldCursorVisible = Cursor.visible;
            settingsCaptured = true;
            InputSystem.settings.updateMode = InputSettings.UpdateMode.ProcessEventsInDynamicUpdate;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            Application.runInBackground = true;
            Time.timeScale = 1f;

            // A saved playable sink must not collide with the independent test prefab instance.
            foreach (SinkWorld existing in Object.FindObjectsByType<SinkWorld>(FindObjectsSortMode.None))
            {
                if (!existing.gameObject.activeInHierarchy) continue;
                if (existing.player != null && existing.player.HasControl) controlledPlayers.Add(existing.player);
                suspendedWorlds.Add(existing.gameObject);
                existing.gameObject.SetActive(false);
            }

            // Physical mouse motion must not steal Mouse.current from the synthetic device.
            foreach (InputDevice device in InputSystem.devices)
            {
                if ((device is Mouse || device is Keyboard) && device.enabled)
                {
                    disabledDevices.Add(device);
                    InputSystem.DisableDevice(device);
                }
            }
            keyboard = InputSystem.AddDevice<Keyboard>("Sink test keyboard");
            mouse = InputSystem.AddDevice<Mouse>("Sink test mouse");
            world = PlayModePrefabFactory.InstantiateLevel();
            yield return null; // Let the real components initialize their spawn state.
            world.player.SetControl(true);
            yield return InputFrame(); // Release the capture click safety gate.
            Assert.That(world.player.HasControl, Is.True, "The test player must own input before assertions.");
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            // Restore state even when a test assertion or its setup failed.
            if (world != null)
            {
                world.player.SetControl(false);
                // Prefab instances borrow persistent material assets; this fixture owns
                // only the instantiated hierarchy, not those shared dependencies.
                Object.Destroy(world.gameObject);
            }
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (mouse != null && mouse.added) InputSystem.RemoveDevice(mouse);
            foreach (InputDevice device in disabledDevices)
                if (device != null && device.added) InputSystem.EnableDevice(device);
            disabledDevices.Clear();
            foreach (GameObject previousWorld in suspendedWorlds)
                if (previousWorld != null) previousWorld.SetActive(true);
            suspendedWorlds.Clear();
            foreach (SinkPlayer previousPlayer in controlledPlayers)
                if (previousPlayer != null) previousPlayer.SetControl(true);
            controlledPlayers.Clear();
            if (settingsCaptured)
            {
                InputSystem.settings.updateMode = oldUpdateMode;
                InputSystem.settings.backgroundBehavior = oldBackgroundBehavior;
                InputSystem.settings.editorInputBehaviorInPlayMode = oldEditorBehavior;
                Application.runInBackground = oldRunInBackground;
                Time.timeScale = oldTimeScale;
                Cursor.lockState = oldCursorLock;
                Cursor.visible = oldCursorVisible;
                settingsCaptured = false;
            }
            yield return null;
        }

        [UnityTest]
        public IEnumerator MouseDeltaTurnsView_AndPitchStopsAtBothLimits()
        {
            float yaw = world.player.Yaw;
            float pitch = world.player.Pitch;
            float sensitivity = world.player.lookSensitivity;
            yield return InputFrame(mouseState: new MouseState { delta = new Vector2(80f, -40f) });
            Assert.That(world.player.Yaw, Is.EqualTo(yaw + 80f * sensitivity).Within(.01f));
            Assert.That(world.player.Pitch, Is.EqualTo(pitch + 40f * sensitivity).Within(.01f));

            yield return InputFrame(mouseState: new MouseState { delta = new Vector2(0f, 100000f) });
            Assert.That(world.player.Pitch, Is.EqualTo(5f).Within(.01f));
            yield return InputFrame(mouseState: new MouseState { delta = new Vector2(0f, -100000f) });
            Assert.That(world.player.Pitch, Is.EqualTo(85f).Within(.01f));
            Assert.That(world.player.viewCamera.transform.forward.y, Is.LessThan(-.99f),
                "Clamped look state must also turn the actual first-person camera.");
        }

        [UnityTest]
        public IEnumerator HeldSprayStopsOnReleaseAndEscape_AndCaptureClickCannotFire()
        {
            MouseState held = new MouseState().WithButton(MouseButton.Left);
            yield return InputFrame(mouseState: held);
            Assert.That(world.water.IsSpraying, Is.True, "Holding LMB must turn the faucet on.");
            yield return InputFrame();
            Assert.That(world.water.IsSpraying, Is.False, "Releasing LMB must turn the faucet off.");

            yield return InputFrame(mouseState: held);
            Assert.That(world.water.IsSpraying, Is.True);
            yield return InputFrame(new[] { Key.Escape }, held);
            Assert.That(world.player.HasControl, Is.False);
            Assert.That(world.water.IsSpraying, Is.False, "Escape must stop a held spray immediately.");
            yield return InputFrame(mouseState: held);
            Assert.That(world.player.HasControl, Is.False, "The held button cannot recapture without a fresh click.");

            yield return InputFrame();
            yield return InputFrame(mouseState: held);
            Assert.That(world.player.HasControl, Is.True);
            Assert.That(world.water.IsSpraying, Is.False, "The capture click must be consumed.");
            yield return InputFrame(mouseState: held);
            Assert.That(world.water.IsSpraying, Is.False, "Continuing to hold the capture click must stay safe.");
            yield return InputFrame();
            yield return InputFrame(mouseState: held);
            Assert.That(world.water.IsSpraying, Is.True, "A second, intentional click must spray normally.");
        }

        [UnityTest]
        public IEnumerator KeyboardAndMouseModesAndWheelPressureReachTheFaucet()
        {
            Assert.That(world.water.WideSpray, Is.False);
            yield return InputFrame(new[] { Key.Q });
            Assert.That(world.water.WideSpray, Is.True);
            yield return InputFrame();
            yield return InputFrame(new[] { Key.Digit1 });
            Assert.That(world.water.WideSpray, Is.False);
            yield return InputFrame(new[] { Key.Digit2 });
            Assert.That(world.water.WideSpray, Is.True);
            yield return InputFrame(mouseState: new MouseState().WithButton(MouseButton.Right));
            Assert.That(world.water.WideSpray, Is.False);

            float initialPressure = world.water.Pressure;
            yield return InputFrame(mouseState: new MouseState { scroll = new Vector2(0f, 120f) });
            Assert.That(world.water.Pressure, Is.GreaterThan(initialPressure));
            float raisedPressure = world.water.Pressure;
            yield return InputFrame(mouseState: new MouseState { scroll = new Vector2(0f, -120f) });
            Assert.That(world.water.Pressure, Is.LessThan(raisedPressure));
        }

        [UnityTest]
        public IEnumerator WasdMovesPlayer_AndHoldingForwardCannotWalkThroughTheCounter()
        {
            Vector3 start = world.player.transform.position;
            for (float elapsed = 0f; elapsed < .3f; elapsed += Time.deltaTime)
                yield return InputFrame(new[] { Key.D });
            Assert.That(world.player.transform.position.x, Is.GreaterThan(start.x + .2f),
                "D must move the real CharacterController sideways.");

            for (float elapsed = 0f; elapsed < .3f; elapsed += Time.deltaTime)
                yield return InputFrame(new[] { Key.A });
            Assert.That(Mathf.Abs(world.player.transform.position.x - start.x), Is.LessThan(.2f));

            for (float elapsed = 0f; elapsed < .6f; elapsed += Time.deltaTime)
                yield return InputFrame(new[] { Key.W });
            Vector3 blocked = world.player.transform.position;
            Collider counter = world.transform.Find("Sink/Counter/Counter front").GetComponent<Collider>();
            Assert.That(blocked.z, Is.GreaterThan(start.z + .04f), "W must move until reaching the obstacle.");
            Assert.That(blocked.z, Is.LessThan(counter.bounds.min.z),
                "Holding W must leave the player's center on the near side of the real counter collider.");
            Assert.That(blocked.y, Is.LessThan(.15f), "The controller must not climb onto the counter.");

            for (float elapsed = 0f; elapsed < .2f; elapsed += Time.deltaTime)
                yield return InputFrame(new[] { Key.S });
            Assert.That(world.player.transform.position.z, Is.LessThan(blocked.z - .15f),
                "S must back away from the obstacle instead of leaving the player stuck.");
        }

        [UnityTest]
        public IEnumerator ResetKeyRestoresFoodStainsAndPlayer_EvenAfterMouseRelease()
        {
            Vector3 playerStart = world.player.transform.position;
            FoodScrap food = world.foods[0];
            Vector3 foodStart = food.transform.position;
            Vector3 drainCenter = world.drain.transform.position;
            food.Body.position = new Vector3(drainCenter.x, world.drain.captureHeight - .08f, drainCenter.z);
            Physics.SyncTransforms();
            Assert.That(world.drain.TryConsume(food), Is.True);
            world.stains[0].Wash(100f);
            Assert.That(world.FoodRemaining, Is.EqualTo(world.foods.Length - 1));
            Assert.That(world.StainsRemaining, Is.EqualTo(world.stains.Length - 1));

            yield return InputFrame(mouseState: new MouseState { delta = new Vector2(150f, -80f) });
            for (float elapsed = 0f; elapsed < .2f; elapsed += Time.deltaTime)
                yield return InputFrame(new[] { Key.D });
            yield return InputFrame(mouseState: new MouseState().WithButton(MouseButton.Left));
            Assert.That(world.water.IsSpraying, Is.True);
            yield return InputFrame(new[] { Key.Escape });
            Assert.That(world.player.HasControl, Is.False);

            yield return InputFrame(new[] { Key.R });
            Assert.That(world.FoodRemaining, Is.EqualTo(world.foods.Length));
            Assert.That(world.StainsRemaining, Is.EqualTo(world.stains.Length));
            Assert.That(world.IsComplete, Is.False);
            Assert.That(world.drain.DrainedCount, Is.Zero);
            Assert.That(food.IsDrained, Is.False);
            Assert.That(food.Body.isKinematic, Is.False);
            Assert.That(food.GetComponent<Collider>().enabled, Is.True);
            Assert.That(food.GetComponent<Renderer>().enabled, Is.True);
            Assert.That(Vector3.Distance(food.Body.position, foodStart), Is.LessThan(.12f));
            Assert.That(Vector3.Distance(world.player.transform.position, playerStart), Is.LessThan(.06f));
            Assert.That(world.player.Yaw, Is.EqualTo(0f).Within(.01f));
            Assert.That(world.player.Pitch, Is.EqualTo(world.player.initialPitch).Within(.01f));
            Assert.That(world.water.IsSpraying, Is.False);
        }

        [UnityTest]
        public IEnumerator VisibleStainCanBeWashedFromLeftSide_WithTheRealHeldNozzle()
        {
            // Reproduce the actual side-of-counter viewpoint that exposed nozzle/rim parallax.
            // Keep the complete production sink, food, faucet, and nozzle placement untouched.
            Assert.That(world.FoodRemaining, Is.EqualTo(world.foods.Length));
            Assert.That(world.StainsRemaining, Is.EqualTo(world.stains.Length));
            StainPatch stain = world.stains[0];
            Assert.That(stain.Remaining, Is.EqualTo(1f));
            CharacterController controller = world.player.GetComponent<CharacterController>();
            controller.enabled = false;
            world.player.transform.position = new Vector3(-2.45f, .015f, -.02f);
            controller.enabled = true;

            Vector3 direction = (stain.transform.position - world.player.viewCamera.transform.position).normalized;
            float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(direction.y) * Mathf.Rad2Deg;
            world.player.SetView(yaw, pitch);
            Physics.SyncTransforms();
            Ray aim = world.player.viewCamera.ViewportPointToRay(new Vector3(.5f, .5f));
            Assert.That(Physics.Raycast(aim, out RaycastHit hit, world.water.maxDistance,
                world.water.waterMask, QueryTriggerInteraction.Ignore), Is.True);
            Assert.That(hit.collider.name, Is.EqualTo("Basin floor left"),
                "This regression is meaningful only when the camera can actually see the stained floor.");
            Assert.That(Vector3.Distance(hit.point, stain.transform.position), Is.LessThan(.03f));

            MouseState held = new MouseState().WithButton(MouseButton.Left);
            for (float elapsed = 0f; elapsed < 1f; elapsed += Time.deltaTime)
                yield return InputFrame(mouseState: held);

            Assert.That(world.water.IsSpraying, Is.True);
            Assert.That(stain.Remaining, Is.LessThan(.98f),
                "The real nozzle must clear the near rim and wash the visible stain while LMB is held.");
        }

        IEnumerator InputFrame(Key[] keys = null, MouseState? mouseState = null)
        {
            keyboard.MakeCurrent();
            mouse.MakeCurrent();
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys ?? new Key[0]));
            InputSystem.QueueStateEvent(mouse, mouseState ?? new MouseState());
            yield return null;
        }
    }
}
