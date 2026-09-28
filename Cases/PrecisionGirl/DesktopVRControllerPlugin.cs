using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.InputSystem;
using Il2CppInterop.Runtime.Injection;

namespace DesktopVRController
{
    [BepInPlugin("com.precisiongirl.desktopcontroller", "PrecisionGirl Desktop VR Controller", "1.0.2")]
    public class DesktopVRControllerPlugin : BasePlugin
    {
        public override void Load()
        {
            Log.LogInfo("Desktop VR Controller: Initializing (New Input System mode)...");

            ClassInjector.RegisterTypeInIl2Cpp<DesktopControllerComponent>();

            var go = new GameObject("DesktopVRControllerComponent");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DesktopControllerComponent>();

            Log.LogInfo("Desktop VR Controller: Successfully injected into game loop!");
        }
    }

    public class DesktopControllerComponent : MonoBehaviour
    {
        public DesktopControllerComponent(IntPtr ptr) : base(ptr) { }

        // State trackers
        private float spacePressTime = 0f;
        private bool spaceHandledAsLong = false;
        private bool isSpaceDown = false;
        private bool showHUD = true;
        private bool initialRecentered = false;
        private float initTimer = 0f;

        private bool isStateListOpen = false;
        private bool isQuickMenuOpen = false;
        private bool isAdminPanelOpen = false;

        // Camera control
        private float moveSpeed = 1.2f;
        private float rotateSpeed = 0.15f;
        private bool isRightMouseDown = false;

        // Cached references
        private QuickMenuController cachedQuickMenu = null;
        private SexActionEngine cachedActionEngine = null;
        private VRRecenterController cachedRecenter = null;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            DisableAutoScreenshot();
        }

        private void DisableAutoScreenshot()
        {
            try
            {
                var autoShots = UnityEngine.Object.FindObjectsOfType<AutoScreenshot>();
                if (autoShots != null)
                {
                    for (int i = 0; i < autoShots.Count; i++)
                    {
                        var s = autoShots[i];
                        if (s != null)
                        {
                            s.recording = false;
                            s.enabled = false;
                            UnityEngine.Object.Destroy(s);
                        }
                    }
                }
            }
            catch { }
        }

        void Update()
        {
            // Continuously neutralize any AutoScreenshot instances in early scene loading
            if (initTimer < 5.0f)
            {
                DisableAutoScreenshot();
            }

            // Auto recenter after 1.5 seconds to align view with character
            if (!initialRecentered)
            {
                initTimer += Time.unscaledDeltaTime;
                if (initTimer > 1.5f)
                {
                    initialRecentered = true;
                    DoRecenter();
                }
            }

            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null && mouse == null)
            {
                return;
            }

            // 1. Mouse cursor & Right-click freelook
            if (mouse != null)
            {
                if (mouse.rightButton.wasPressedThisFrame)
                {
                    isRightMouseDown = true;
                    Cursor.lockState = CursorLockMode.Locked;
                    Cursor.visible = false;
                }
                else if (mouse.rightButton.wasReleasedThisFrame)
                {
                    isRightMouseDown = false;
                    Cursor.lockState = CursorLockMode.None;
                    Cursor.visible = true;
                }

                // 2. Camera Rotation via Mouse
                if (isRightMouseDown)
                {
                    Vector2 mouseDelta = mouse.delta.ReadValue();
                    float mouseX = mouseDelta.x * rotateSpeed;
                    float mouseY = -mouseDelta.y * rotateSpeed;

                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Transform parentT = cam.transform.parent != null ? cam.transform.parent : cam.transform;
                        parentT.Rotate(0f, mouseX, 0f, Space.World);
                        cam.transform.Rotate(mouseY, 0f, 0f, Space.Self);
                    }
                }

                // 4. Mouse ScrollWheel: Piston Speed
                Vector2 scroll = mouse.scroll.ReadValue();
                if (scroll.y > 5f)
                {
                    SexEventHub.OnSpeedChangeRequest?.Invoke(1.0f);
                }
                else if (scroll.y < -5f)
                {
                    SexEventHub.OnSpeedChangeRequest?.Invoke(-1.0f);
                }
            }

            if (kb != null)
            {
                // A/D Keyboard rotation
                if (!isRightMouseDown)
                {
                    if (kb.aKey.isPressed)
                    {
                        SexEventHub.OnStickRotationRequest?.Invoke(-1.0f);
                    }
                    else if (kb.dKey.isPressed)
                    {
                        SexEventHub.OnStickRotationRequest?.Invoke(1.0f);
                    }
                }

                // 3. Camera Movement (Shift + WASD / QE)
                bool shiftPressed = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (shiftPressed)
                {
                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Vector3 moveDir = Vector3.zero;
                        if (kb.wKey.isPressed) moveDir += cam.transform.forward;
                        if (kb.sKey.isPressed) moveDir -= cam.transform.forward;
                        if (kb.aKey.isPressed) moveDir -= cam.transform.right;
                        if (kb.dKey.isPressed) moveDir += cam.transform.right;
                        if (kb.eKey.isPressed) moveDir += Vector3.up;
                        if (kb.qKey.isPressed) moveDir -= Vector3.up;

                        if (moveDir != Vector3.zero)
                        {
                            float currentSpeed = kb.leftCtrlKey.isPressed ? moveSpeed * 2.5f : moveSpeed;
                            Transform parentT = cam.transform.parent != null ? cam.transform.parent : cam.transform;
                            parentT.position += moveDir.normalized * currentSpeed * Time.deltaTime;
                        }
                    }
                }
                else
                {
                    // Non-shift W/S: Piston Speed
                    if (kb.wKey.wasPressedThisFrame)
                    {
                        SexEventHub.OnSpeedChangeRequest?.Invoke(1.0f);
                    }
                    else if (kb.sKey.wasPressedThisFrame)
                    {
                        SexEventHub.OnSpeedChangeRequest?.Invoke(-1.0f);
                    }
                }

                // 5. Backspace: Reset Speed
                if (kb.backspaceKey.wasPressedThisFrame)
                {
                    SexEventHub.OnSpeedResetRequest?.Invoke();
                }

                // 6. Recenter View: Key R
                if (kb.rKey.wasPressedThisFrame)
                {
                    DoRecenter();
                }

                // 7. Space Key: Short press for PreCum / Long press for Climax
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    isSpaceDown = true;
                    spacePressTime = Time.unscaledTime;
                    spaceHandledAsLong = false;
                }
                if (isSpaceDown && kb.spaceKey.isPressed && !spaceHandledAsLong)
                {
                    if (Time.unscaledTime - spacePressTime > 0.45f)
                    {
                        spaceHandledAsLong = true;
                        FireClimax();
                    }
                }
                if (kb.spaceKey.wasReleasedThisFrame)
                {
                    isSpaceDown = false;
                    if (!spaceHandledAsLong)
                    {
                        SexEventHub.OnPreCumSquirt?.Invoke();
                    }
                }

                // Alternative Climax Key: F
                if (kb.fKey.wasPressedThisFrame)
                {
                    FireClimax();
                }

                // 8. Next State / Pose: Key N or Enter
                if (kb.nKey.wasPressedThisFrame || kb.enterKey.wasPressedThisFrame)
                {
                    SexEventHub.OnNextStateRequest?.Invoke();
                }

                // 9. State List Menu: Key L
                if (kb.lKey.wasPressedThisFrame)
                {
                    isStateListOpen = !isStateListOpen;
                    Transform origin = Camera.main != null ? Camera.main.transform : null;
                    SexEventHub.OnStateListToggle?.Invoke(isStateListOpen, origin);
                }

                // 10. Quick Menu Toggle: Tab or M
                if (kb.tabKey.wasPressedThisFrame || kb.mKey.wasPressedThisFrame)
                {
                    isQuickMenuOpen = !isQuickMenuOpen;
                    Transform origin = Camera.main != null ? Camera.main.transform : null;
                    SexEventHub.OnQuickMenuToggle?.Invoke(isQuickMenuOpen, origin);
                }

                // 11. Main Admin Panel UI: Key U or Escape
                if (kb.uKey.wasPressedThisFrame || kb.escapeKey.wasPressedThisFrame)
                {
                    isAdminPanelOpen = !isAdminPanelOpen;
                    SexEventHub.OnToggleUI?.Invoke(isAdminPanelOpen);
                }

                // 12. Quick Direct Toggles (1: Naked, 2: Male, 3: Penis, 4: Excitement)
                if (kb.digit1Key.wasPressedThisFrame)
                {
                    EnsureQuickMenu()?.ToggleNaked();
                }
                if (kb.digit2Key.wasPressedThisFrame)
                {
                    EnsureQuickMenu()?.ToggleMaleVisible();
                }
                if (kb.digit3Key.wasPressedThisFrame)
                {
                    EnsureQuickMenu()?.TogglePenisVisible();
                }
                if (kb.digit4Key.wasPressedThisFrame)
                {
                    EnsureQuickMenu()?.ToggleExcitementDisplay();
                }

                // 13. HUD Toggle: Key H
                if (kb.hKey.wasPressedThisFrame)
                {
                    showHUD = !showHUD;
                }
            }
        }

        private void DoRecenter()
        {
            SexEventHub.OnViewResetRequest?.Invoke();
            if (cachedRecenter == null)
            {
                cachedRecenter = UnityEngine.Object.FindObjectOfType<VRRecenterController>();
            }
            if (cachedRecenter != null)
            {
                cachedRecenter.RecenterView();
            }
        }

        private void FireClimax()
        {
            SexEventHub.OnFinishRequest?.Invoke();
            SexEventHub.OnEjaculationRequest?.Invoke();
        }

        private QuickMenuController EnsureQuickMenu()
        {
            if (cachedQuickMenu == null)
            {
                cachedQuickMenu = UnityEngine.Object.FindObjectOfType<QuickMenuController>();
            }
            return cachedQuickMenu;
        }

        private SexActionEngine EnsureActionEngine()
        {
            if (cachedActionEngine == null)
            {
                cachedActionEngine = UnityEngine.Object.FindObjectOfType<SexActionEngine>();
            }
            return cachedActionEngine;
        }

        void OnGUI()
        {
            if (!showHUD)
            {
                GUI.color = new Color(1f, 1f, 1f, 0.5f);
                GUI.Label(new Rect(15, 12, 220, 22), "按 [H] 展开键鼠操作提示");
                return;
            }

            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            GUI.Box(new Rect(12, 12, 320, 390), GUIContent.none);

            GUI.color = Color.white;
            GUIStyle headerStyle = new GUIStyle()
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold
            };
            headerStyle.normal.textColor = new Color(0.3f, 0.95f, 1.0f);

            GUIStyle statusStyle = new GUIStyle()
            {
                fontSize = 11
            };
            statusStyle.normal.textColor = new Color(0.7f, 1.0f, 0.7f);

            GUIStyle labelStyle = new GUIStyle()
            {
                fontSize = 11
            };
            labelStyle.normal.textColor = new Color(0.92f, 0.92f, 0.92f);

            GUIStyle keyStyle = new GUIStyle()
            {
                fontSize = 11,
                fontStyle = FontStyle.Bold
            };
            keyStyle.normal.textColor = new Color(1.0f, 0.82f, 0.25f);

            GUI.Label(new Rect(22, 18, 290, 22), "★ 精密少女 · 键盘鼠标控制器 ★", headerStyle);

            var engine = EnsureActionEngine();
            string statusText = "状态: 运行中";
            if (engine != null)
            {
                try
                {
                    string tag = engine.GetCurrentPositionTag().ToString();
                    string label = engine.GetCurrentMotionLabel();
                    float bpm = engine.GetEffectiveBPM();
                    statusText = $"体位: {tag} | 动作: {label} | BPM: {bpm:F0}";
                }
                catch { }
            }
            GUI.Label(new Rect(22, 40, 300, 20), statusText, statusStyle);

            GUI.color = new Color(0.4f, 0.4f, 0.5f, 0.5f);
            GUI.Box(new Rect(20, 62, 300, 1), GUIContent.none);
            GUI.color = Color.white;

            int y = 68;
            int step = 20;

            void DrawRow(string key, string desc)
            {
                GUI.Label(new Rect(22, y, 120, 20), key, keyStyle);
                GUI.Label(new Rect(142, y, 180, 20), desc, labelStyle);
                y += step;
            }

            DrawRow("W / S 或 滚轮", "抽插活塞速度 调节");
            DrawRow("Backspace", "重置为默认初始速度");
            DrawRow("A / D 或 拖拽右键", "水平旋转视点");
            DrawRow("按住右键滑动", "自由仰角与方向旋转");
            DrawRow("Shift + WASD", "平移移动视点位置");
            DrawRow("Q / E", "视角垂直升降");
            DrawRow("空格 Space (短按)", "弱绝顶 / 小射精");
            DrawRow("空格 (长按) / F", "绝顶大射精");
            DrawRow("N 键 / 回车", "切换下一个动作姿势");
            DrawRow("L 键", "打开 / 关闭姿势列表");
            DrawRow("Tab / M 键", "迷你快捷菜单 (QuickMenu)");
            DrawRow("U 键 / Esc", "主控制面板 UI (AdminPanel)");
            DrawRow("R 键", "重置正面最佳观赏视点");
            DrawRow("数字 1 / 2 / 3", "一键 爆衣/隐男主/隐丁丁");
            DrawRow("H 键", "隐藏 / 展开本说明浮层");
        }
    }
}
