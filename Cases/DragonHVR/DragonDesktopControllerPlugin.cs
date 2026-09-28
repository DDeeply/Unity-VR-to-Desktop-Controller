using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;
using Il2CppInterop.Runtime.Injection;

namespace DragonDesktopController
{
    [BepInPlugin("com.ninoveria.dragondesktopcontroller", "DragonHVR Desktop Controller", "1.0.2")]
    public class DragonDesktopControllerPlugin : BasePlugin
    {
        public override void Load()
        {
            Log.LogInfo("DragonHVR Desktop Controller: Initializing...");

            ClassInjector.RegisterTypeInIl2Cpp<DragonControllerComponent>();

            var go = new GameObject("DragonDesktopControllerComponent");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<DragonControllerComponent>();

            Log.LogInfo("DragonHVR Desktop Controller: Injected successfully!");
        }
    }

    public class DragonControllerComponent : MonoBehaviour
    {
        public DragonControllerComponent(IntPtr ptr) : base(ptr) { }

        // State trackers
        private float spacePressTime = 0f;
        private bool spaceHandledAsLong = false;
        private bool isSpaceDown = false;
        private bool showHUD = true;
        private bool initialRecentered = false;
        private float initTimer = 0f;
        private bool lookAtActive = false;
        private bool localeInitialized = false;

        // Camera control
        private float moveSpeed = 1.2f;
        private float rotateSpeed = 0.15f;
        private bool isRightMouseDown = false;
        private float currentAnimSpeed = 1.0f;

        // Cached references
        private TimelineLoopAndJump cachedTimelineJump = null;
        private XRRecenterWithRigidbodies cachedRecenter = null;
        private LookAtManager cachedLookAt = null;
        private MasterAnimationController cachedAnimCtrl = null;

        void Start()
        {
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
            SetChineseLocale();
        }

        private void SetChineseLocale()
        {
            try
            {
                var locales = LocalizationSettings.AvailableLocales?.Locales;
                if (locales != null && locales.Count > 0)
                {
                    // 1. Prefer Simplified Chinese
                    foreach (var loc in locales)
                    {
                        string code = loc.Identifier.Code.ToLower();
                        string name = loc.LocaleName;
                        if (code.Contains("zh-hans") || code.Contains("zh-cn") || name.Contains("简体") || name.Contains("Simplified"))
                        {
                            LocalizationSettings.SelectedLocale = loc;
                            localeInitialized = true;
                            return;
                        }
                    }
                    // 2. Fallback to any Chinese
                    foreach (var loc in locales)
                    {
                        string code = loc.Identifier.Code.ToLower();
                        string name = loc.LocaleName;
                        if (code.Contains("zh") || name.Contains("中文") || name.Contains("Chinese"))
                        {
                            LocalizationSettings.SelectedLocale = loc;
                            localeInitialized = true;
                            return;
                        }
                    }
                }
            }
            catch { }
        }

        private void CycleLocale()
        {
            try
            {
                var locales = LocalizationSettings.AvailableLocales?.Locales;
                if (locales != null && locales.Count > 1)
                {
                    var cur = LocalizationSettings.SelectedLocale;
                    int curIdx = -1;
                    for (int i = 0; i < locales.Count; i++)
                    {
                        if (locales[i] == cur)
                        {
                            curIdx = i;
                            break;
                        }
                    }
                    int nextIdx = (curIdx + 1) % locales.Count;
                    LocalizationSettings.SelectedLocale = locales[nextIdx];
                }
            }
            catch { }
        }

        void Update()
        {
            // Auto recenter and ensure Chinese locale in first 2 seconds
            if (!initialRecentered || !localeInitialized)
            {
                initTimer += Time.unscaledDeltaTime;
                if (!localeInitialized && initTimer > 0.5f)
                {
                    SetChineseLocale();
                }
                if (!initialRecentered && initTimer > 1.5f)
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

                // 3. Mouse ScrollWheel: Animation Speed
                Vector2 scroll = mouse.scroll.ReadValue();
                if (scroll.y > 5f)
                {
                    AdjustSpeed(0.1f);
                }
                else if (scroll.y < -5f)
                {
                    AdjustSpeed(-0.1f);
                }
            }

            if (kb != null)
            {
                // A/D Keyboard rotation
                if (!isRightMouseDown)
                {
                    if (kb.aKey.isPressed)
                    {
                        Camera cam = Camera.main;
                        if (cam != null)
                        {
                            Transform parentT = cam.transform.parent != null ? cam.transform.parent : cam.transform;
                            parentT.Rotate(0f, -60f * Time.deltaTime, 0f, Space.World);
                        }
                    }
                    else if (kb.dKey.isPressed)
                    {
                        Camera cam = Camera.main;
                        if (cam != null)
                        {
                            Transform parentT = cam.transform.parent != null ? cam.transform.parent : cam.transform;
                            parentT.Rotate(0f, 60f * Time.deltaTime, 0f, Space.World);
                        }
                    }
                }

                // 4. Camera Movement (Shift + WASD / QE)
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
                    // Non-shift W/S: Speed Adjust
                    if (kb.wKey.wasPressedThisFrame)
                    {
                        AdjustSpeed(0.1f);
                    }
                    else if (kb.sKey.wasPressedThisFrame)
                    {
                        AdjustSpeed(-0.1f);
                    }
                }

                // 5. Recenter: Key R
                if (kb.rKey.wasPressedThisFrame)
                {
                    DoRecenter();
                }

                // 6. LookAt Camera Toggle: Key T or Alt
                if (kb.tKey.wasPressedThisFrame || kb.leftAltKey.wasPressedThisFrame)
                {
                    ToggleLookAt();
                }

                // 7. Climax Jump: Space (long press) or Key F
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
                        TriggerClimax();
                    }
                }
                if (kb.spaceKey.wasReleasedThisFrame)
                {
                    isSpaceDown = false;
                }

                if (kb.fKey.wasPressedThisFrame)
                {
                    TriggerClimax();
                }

                // 8. Reset Speed: Backspace
                if (kb.backspaceKey.wasPressedThisFrame)
                {
                    currentAnimSpeed = 1.0f;
                    ApplySpeed(currentAnimSpeed);
                }

                // 9. Language Switch: Key F1
                if (kb.f1Key.wasPressedThisFrame)
                {
                    CycleLocale();
                }

                // 10. HUD Toggle: Key H
                if (kb.hKey.wasPressedThisFrame)
                {
                    showHUD = !showHUD;
                }
            }
        }

        private void AdjustSpeed(float delta)
        {
            currentAnimSpeed = Mathf.Clamp(currentAnimSpeed + delta, 0.2f, 3.0f);
            ApplySpeed(currentAnimSpeed);
        }

        private void ApplySpeed(float speed)
        {
            var anim = EnsureAnimCtrl();
            if (anim != null)
            {
                anim.OnSpeedChanged(speed);
                if (anim.speedSlider != null)
                {
                    anim.speedSlider.value = speed;
                }
            }
        }

        private void TriggerClimax()
        {
            var tj = EnsureTimelineJump();
            if (tj != null)
            {
                tj.JumpToC();
            }
        }

        private void DoRecenter()
        {
            if (cachedRecenter == null)
            {
                cachedRecenter = UnityEngine.Object.FindObjectOfType<XRRecenterWithRigidbodies>();
            }
            if (cachedRecenter != null)
            {
                cachedRecenter.RecenterToTarget();
            }
            else
            {
                var rp = UnityEngine.Object.FindObjectOfType<XRResetPosition>();
                if (rp != null)
                {
                    rp.RecenterToTarget();
                }
            }
        }

        private void ToggleLookAt()
        {
            lookAtActive = !lookAtActive;
            if (cachedLookAt == null)
            {
                cachedLookAt = UnityEngine.Object.FindObjectOfType<LookAtManager>();
            }
            if (cachedLookAt != null)
            {
                cachedLookAt.SetLookAtActive(lookAtActive);
            }
        }

        private TimelineLoopAndJump EnsureTimelineJump()
        {
            if (cachedTimelineJump == null)
            {
                cachedTimelineJump = UnityEngine.Object.FindObjectOfType<TimelineLoopAndJump>();
            }
            return cachedTimelineJump;
        }

        private MasterAnimationController EnsureAnimCtrl()
        {
            if (cachedAnimCtrl == null)
            {
                cachedAnimCtrl = UnityEngine.Object.FindObjectOfType<MasterAnimationController>();
            }
            return cachedAnimCtrl;
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
            GUI.Box(new Rect(12, 12, 320, 385), GUIContent.none);

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

            GUI.Label(new Rect(22, 18, 290, 22), "★ DragonHVR 键盘鼠标控制器 ★", headerStyle);

            string curLang = "简体中文";
            try
            {
                var loc = LocalizationSettings.SelectedLocale;
                if (loc != null) curLang = loc.LocaleName;
            }
            catch { }

            string status = $"速度: {currentAnimSpeed:F1}x | 视线: {(lookAtActive ? "看镜头" : "自由")} | 语言: {curLang}";
            GUI.Label(new Rect(22, 40, 300, 20), status, statusStyle);

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

            DrawRow("W / S 或 滚轮", "动作播放速度 调节");
            DrawRow("Backspace", "重置动作速度为 1.0x");
            DrawRow("A / D 键", "水平平滑旋转视点");
            DrawRow("按住鼠标右键滑动", "自由仰角与方向旋转");
            DrawRow("Shift + WASD", "平移自由视角走动观察");
            DrawRow("Q / E", "视角高度垂直升降");
            DrawRow("空格 (长按) / F", "触发高潮演出 (JumpToC)");
            DrawRow("T 键 或 Alt", "切换角色视线看向镜头");
            DrawRow("R 键", "重置正面最佳观赏视点");
            DrawRow("F1 键", "切换游戏语言 (中/日/英)");
            DrawRow("H 键", "隐藏 / 展开本说明浮层");
        }
    }
}
