#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
通用 Unity IL2CPP BepInEx 插件一键编译脚本
自动加载目标游戏的 BepInEx / dotnet / interop 引用并调用 Roslyn csc 进行高性能编译
"""

import subprocess, os, sys, glob

def compile_plugin(game_dir, src_cs, output_dll_name=None):
    if not os.path.exists(game_dir):
        print(f"[-] Game directory not found: {game_dir}")
        return False

    dotnet_dir = os.path.join(game_dir, "dotnet")
    core_dir = os.path.join(game_dir, "BepInEx", "core")
    interop_dir = os.path.join(game_dir, "BepInEx", "interop")
    plugin_dir = os.path.join(game_dir, "BepInEx", "plugins")
    os.makedirs(plugin_dir, exist_ok=True)

    if not output_dll_name:
        base = os.path.splitext(os.path.basename(src_cs))[0]
        output_dll_name = base + ".dll"
    out_dll = os.path.join(plugin_dir, output_dll_name)

    # Find csc compiler
    script_dir = os.path.dirname(os.path.abspath(__file__))
    csc_candidates = [
        os.path.join(script_dir, r"..\..\brain\68453733-5d0f-48da-bf96-fba6e082430e\scratch\roslyn\tasks\net472\csc.exe"),
        r"C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe"
    ]
    csc = None
    for c in csc_candidates:
        if os.path.exists(c):
            csc = c
            break

    if not csc:
        print("[-] C# compiler (csc.exe) not found!")
        return False

    dotnet_dlls = glob.glob(os.path.join(dotnet_dir, "*.dll"))
    core_dlls = [
        os.path.join(core_dir, "BepInEx.Core.dll"),
        os.path.join(core_dir, "BepInEx.Unity.Common.dll"),
        os.path.join(core_dir, "BepInEx.Unity.IL2CPP.dll"),
        os.path.join(core_dir, "Il2CppInterop.Runtime.dll"),
    ]
    interop_dlls = glob.glob(os.path.join(interop_dir, "*.dll"))

    rsp_lines = [
        "/target:library",
        f'/out:"{out_dll}"',
        "/nostdlib+",
        "/noconfig",
        "/langversion:latest"
    ]

    all_refs = set(dotnet_dlls + core_dlls + interop_dlls)
    native_blacklist = [
        "coreclr.dll", "clrjit.dll", "mscordaccore.dll", "mscordbi.dll", "msquic.dll", 
        "clretwrc.dll", "dbgshim.dll", "hostpolicy.dll", "mscorrc.dll", 
        "mscordaccore_amd64_amd64_6.0.722.32202.dll", "system.io.compression.native.dll", 
        "microsoft.diasymreader.native.amd64.dll"
    ]

    for r in sorted(all_refs):
        bn = os.path.basename(r).lower()
        if any(bn == x or bn.startswith("mscordac") or "native" in bn for x in native_blacklist):
            continue
        rsp_lines.append(f'/r:"{r}"')

    rsp_lines.append(f'"{src_cs}"')

    rsp_file = os.path.join(script_dir, "compile_args.rsp")
    with open(rsp_file, "w", encoding="utf-8") as f:
        f.write("\n".join(rsp_lines))

    print(f"[*] Compiling {src_cs} -> {out_dll}...")
    cmd = [csc, f"@{rsp_file}"]
    proc = subprocess.run(cmd, capture_output=True, text=True)
    if proc.returncode == 0:
        print("[+] Build succeeded!")
        return True
    else:
        print("[-] Build failed:")
        print(proc.stdout)
        if proc.stderr:
            print(proc.stderr)
        return False

if __name__ == '__main__':
    if len(sys.argv) < 3:
        print("Usage: python build.py <game_root_dir> <plugin_source.cs> [output_dll_name]")
        sys.exit(1)
    out_name = sys.argv[3] if len(sys.argv) > 3 else None
    compile_plugin(sys.argv[1], sys.argv[2], out_name)
