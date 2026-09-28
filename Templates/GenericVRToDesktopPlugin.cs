using System;
using BepInEx;
using BepInEx.Unity.IL2CPP;
using UnityEngine;
using UnityEngine.InputSystem;
using Il2CppInterop.Runtime.Injection;

namespace GenericDesktopVR
{
    /// <summary>
    /// 通用 Unity IL2CPP VR 转 PC 桌面键盘鼠标控制器模板
    /// 适用于任意 Unity 2021+ / Unity 6 IL2CPP VR 游戏
    /// </summary>
    [BepInPlugin("com.generic.desktopvrcontroller", "Generic Desktop VR Controller", "1.0.0")]
    public class GenericDesktopVRPlugin : BasePlugin
    {
        public override void Load()
        {
            Log.LogInfo("Generic Desktop VR Controller: Loading...");

            // 1. 注册自定义 MonoBehaviour 到 IL2CPP 运行时
            ClassInjector.RegisterTypeInIl2Cpp<GenericDesktopComponent>();

            // 2. 创建常驻场景对象
            var go = new GameObject("GenericDesktopVRComponent");
            go.hideFlags = HideFlags.HideAndDontSave;
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<GenericDesktopComponent>();

            Log.LogInfo("Generic Desktop VR Controller: Ready!");
        }
    }

    public class GenericDesktopComponent : MonoBehaviour
    {
        public GenericDesktopComponent(IntPtr ptr) : base(ptr) { }

        // 基础参数
        private float moveSpeed = 1.5f;
        private float rotateSpeed = 0.15f;
        private bool isRightMouseDown = false;
        private bool showHUD = true;

        void Start()
        {
            // 确保鼠标指针默认可见且不锁定
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        void Update()
        {
            var kb = Keyboard.current;
            var mouse = Mouse.current;
            if (kb == null && mouse == null) return;

            // ----------------------------------------------------
            // 1. 鼠标控制：右键自由旋转视角
            // ----------------------------------------------------
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

                if (isRightMouseDown)
                {
                    Vector2 delta = mouse.delta.ReadValue();
                    float mouseX = delta.x * rotateSpeed;
                    float mouseY = -delta.y * rotateSpeed;

                    Camera cam = Camera.main;
                    if (cam != null)
                    {
                        Transform parentT = cam.transform.parent != null ? cam.transform.parent : cam.transform;
                        parentT.Rotate(0f, mouseX, 0f, Space.World);
                        cam.transform.Rotate(mouseY, 0f, 0f, Space.Self);
                    }
                }

                // 鼠标滚轮（可在此接入游戏速度/缩放事件）
                Vector2 scroll = mouse.scroll.ReadValue();
                if (Mathf.Abs(scroll.y) > 5f)
                {
                    // TODO: 在此调用游戏的调速方法
                }
            }

            // ----------------------------------------------------
            // 2. 键盘控制：平移走动 (Shift + WASD / QE)
            // ----------------------------------------------------
            if (kb != null)
            {
                bool shift = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
                if (shift)
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
                            float speed = kb.leftCtrlKey.isPressed ? moveSpeed * 2.5f : moveSpeed;
                            Transform parentT = cam.transform.parent != null ? cam.transform.parent : cam.transform;
                            parentT.position += moveDir.normalized * speed * Time.deltaTime;
                        }
                    }
                }

                // ----------------------------------------------------
                // 3. 游戏事件绑定区域 (在此处按需填充目标游戏逻辑)
                // ----------------------------------------------------
                if (kb.spaceKey.wasPressedThisFrame)
                {
                    // TODO: 触发原手柄 Trigger 扳机事件
                }

                if (kb.rKey.wasPressedThisFrame)
                {
                    // TODO: 触发原手柄视角重置 Recenter 事件
                }

                if (kb.tabKey.wasPressedThisFrame)
                {
                    // TODO: 触发原手柄菜单开闭 Menu 事件
                }

                // HUD 开关
                if (kb.hKey.wasPressedThisFrame)
                {
                    showHUD = !showHUD;
                }
            }
        }

        void OnGUI()
        {
            if (!showHUD) return;

            GUI.color = new Color(0.05f, 0.05f, 0.08f, 0.85f);
            GUI.Box(new Rect(12, 12, 280, 220), GUIContent.none);

            GUI.color = Color.white;
            GUIStyle headerStyle = new GUIStyle { fontSize = 13, fontStyle = FontStyle.Bold };
            headerStyle.normal.textColor = new Color(0.3f, 0.95f, 1.0f);

            GUIStyle labelStyle = new GUIStyle { fontSize = 11 };
            labelStyle.normal.textColor = Color.white;

            GUI.Label(new Rect(22, 18, 260, 22), "★ VR to Desktop Controller ★", headerStyle);
            GUI.Label(new Rect(22, 45, 260, 20), "按住鼠标右键滑动 : 自由旋转视角", labelStyle);
            GUI.Label(new Rect(22, 70, 260, 20), "Shift + WASD     : 平移移动视角", labelStyle);
            GUI.Label(new Rect(22, 95, 260, 20), "Q / E            : 上下升降高度", labelStyle);
            GUI.Label(new Rect(22, 120, 260, 20), "Space            : 手柄动作触发", labelStyle);
            GUI.Label(new Rect(22, 145, 260, 20), "H 键             : 隐藏/显示此提示", labelStyle);
        }
    }
}
