@echo off
chcp 65001 >nul
echo 正在禁用键鼠插件，还原游戏为官方纯净模式...
if exist winhttp.dll (
    ren winhttp.dll winhttp.dll.bak
    echo 成功禁用！游戏已还原为纯净版。
) else (
    echo 未发现启用的 winhttp.dll，当前已是禁用状态。
)
pause
