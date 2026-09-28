@echo off
chcp 65001 >nul
echo 正在启用键鼠映射插件...
if exist winhttp.dll.bak (
    ren winhttp.dll.bak winhttp.dll
    echo 成功启用！现在启动游戏即可使用键盘鼠标畅玩。
) else (
    echo winhttp.dll 当前已经是启用状态，直接运行游戏 exe 即可！
)
pause
