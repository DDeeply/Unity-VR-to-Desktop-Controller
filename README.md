# Unity VR to Desktop Controller

> 🎮 **免除 XR 模式与任何头显设备模拟，将仅支持 VR 手柄的 Unity 游戏无缝转为纯 PC 键盘鼠标操作**  
> Seamlessly convert VR-only Unity games into native PC desktop keyboard & mouse controls without requiring SteamVR or virtual headset emulators.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Unity Version](https://img.shields.io/badge/Unity-2021+-blue.svg)](https://unity.com/)
[![Runtime](https://img.shields.io/badge/Runtime-IL2CPP%20x64-green.svg)](https://docs.bepinex.dev/)
[![Input System](https://img.shields.io/badge/Input-InputSystem%20Package-purple.svg)](https://docs.unity3d.com/Packages/com.unity.inputsystem@latest)

---

## 💡 为什么需要本项目？(Why This Project?)

很多优秀的 VR 独占游戏本身可以在 PC 上正常渲染窗口画面（当未检测到 VR 头显时，OpenXR 自动回退至桌面输出），但游戏由于缺少键鼠交互逻辑，**所有操作仅响应 VR 手柄（XR Controller）输入**。

传统的解决方案往往依赖 **Driver4VR / OpenVR-Desktop / ALVR** 等头显模拟器：
- ❌ **严重降级画质**：画面强制渲染为双目鱼眼畸变或圆形分屏，黑边严重。
- ❌ **极度繁琐沉重**：必须先启动 SteamVR 或虚拟驱动，占用大量 CPU/GPU 资源且容易闪退崩溃。

**本项目的革命性解决方案**：
- ✅ **拒绝 XR 模拟**：保持纯粹的原生 PC 2D 桌面窗口，完全不启动 SteamVR，画质 100% 原始呈现。
- ✅ **极轻量零驱动**：基于 **BepInEx 6 IL2CPP** 原生注入，直接在游戏逻辑层将手柄事件映射到键盘与鼠标原生信号。
- ✅ **全自由漫游与控制**：内置右键自由旋转视角、Shift+WASD 漫游走动、鼠标滚轮调速、按键触发动作、优雅的左上角悬浮 HUD 指引。

---

## 📂 项目结构 (Repository Structure)

```text
Unity-VR-to-Desktop/
├── Cases/                          # 实战游戏适配案例
│   ├── PrecisionGirl/              # 案例 1: 《精密少女》完整插件源码与说明
│   │   └── DesktopVRControllerPlugin.cs
│   └── DragonHVR/                  # 案例 2: 《DragonHVR / 龙娘》完整插件源码与说明
│       └── DragonDesktopControllerPlugin.cs
├── Templates/                      # 通用复用脚手架与工具
│   ├── GenericVRToDesktopPlugin.cs # 通用插件 C# 模板 (已内置漫游、视角、新输入系统兼容)
│   ├── build.py                    # 跨游戏一键编译构建脚本
│   └── Scripts/
│       ├── inspect_metadata.py     # 通用 IL2CPP 元数据逆向分析与方法提取工具
│       ├── EnablePlugin.bat        # 一键启用批处理脚本
│       └── DisablePlugin.bat       # 一键禁用还原官方纯净版批处理脚本
├── Docs/
│   └── Game_Adaptation_Guide.md    # 核心文档：针对其他 Unity 游戏的保姆级复用指南
├── README.md                       # 项目主文档
└── LICENSE                         # MIT 开源许可证
```

---

## 🕹️ 默认按键设计标准 (Default Keybinding Standard)

| 功能类别 | 键鼠推荐映射 | 对应手柄操作 | 功能说明 |
| :--- | :--- | :--- | :--- |
| **主交互 / 动作触发** | **空格键 Space (长按) / `F` 键** | 扳机键 (Trigger) 长按 | 触发核心动作 / 高潮 / 确定 |
| **次交互 / 微触发** | **空格键 Space (短按)** | 扳机键 (Trigger) 短按 | 轻度触发 / 辅助交互 |
| **节奏 / 速度调节** | **键盘 `W` / `S` 或 鼠标滚轮** | 摇杆 (Thumbstick) 上/下 | 平滑增加 / 减小动作播放速度 |
| **视角水平旋转** | **键盘 `A` / `D` 或 右键水平拖拽** | 摇杆 (Thumbstick) 左/右 | 角色环绕平滑水平旋转 |
| **360° 自由视角旋转** | **按住鼠标右键滑动** | - | 俯仰角与水平方向全自由漫游 |
| **自由走动平移视角** | **`Shift + WASD`** | 握把键 (Grip) 移动 | 像自由摄像机一样移动到场景任意位置 |
| **垂直高度升降** | **`Q` 键 (下降) / `E` 键 (升高)** | - | 快速调整视点高低 |
| **重置正面视点** | **`R` 键** | 握把键 3 连按 / A 键长按 | 自动重置至正面最佳观赏距离 |
| **动作姿态切换** | **`N` 键 或 `Enter` 回车** | A 键单击 | 切换下一动作 / 姿势 |
| **快捷菜单 / 面板** | **`Tab` 键 / `M` 键 / `U` 键** | 摇杆按下 / B 键 | 打开或关闭游戏设置与属性面板 |
| **按键指南浮层** | **`H` 键** | - | 随时隐藏或唤起屏幕左上角 HUD 提示 |

---

## 🔄 如何将本工具复用到其他游戏？(Reusability Guide)

详细分步指南请查阅：**[Docs/Game_Adaptation_Guide.md](Docs/Game_Adaptation_Guide.md)**。

简要流程仅需 3 步：
1. **部署 BepInEx 6**：复制 `winhttp.dll`、`doorstop_config.ini`、`BepInEx/`、`dotnet/` 到新游戏根目录，运行一次生成 `interop` 程序集。
2. **提取事件**：运行 `python Templates/Scripts/inspect_metadata.py <global-metadata.dat>`，查找关键控制器和事件。
3. **套用模板并编译**：根据发现的方法修改 `Templates/GenericVRToDesktopPlugin.cs`，使用 `python Templates/build.py <游戏目录> <脚本路径>` 一键编译生效！

---

## 🛠️ 常见难点攻克 (Known Issues & Solutions)

1. **Unity 新输入系统报错 (`InvalidOperationException`)**：
   - 现代 Unity 游戏（如 Unity 2022+ / Unity 6）锁定了新版 `Input System Package`，旧版 `UnityEngine.Input` 会直接抛出异常。
   - 本项目全系模板原生采用 `UnityEngine.InputSystem`（`Keyboard.current` 与 `Mouse.current`），100% 兼容无报错。
2. **语言回退至英文**：
   - 部分游戏在加载外部 CoreCLR 时语言环境重置为英文。模板提供了自动遍历 `LocalizationSettings.AvailableLocales` 优先激活 `zh-Hans` / `zh-CN` 并支持 `F1` 键循环切换的完整方案。
3. **开发调试截屏残留**：
   - 拦截并销毁部分游戏作者残留的后台自动截图组件（如 `AutoScreenshot`），保护桌面整洁。

---

## 📄 开源许可证 (License)

本项目基于 [MIT License](LICENSE) 开源发布。
欢迎提交 Issue 与 Pull Request 贡献更多游戏的适配案例！
