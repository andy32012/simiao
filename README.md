# 寺庙 · 霧鎖千佛山（Unity 版）

第一人称恐怖游戏。原本是 three.js 网页版，移植到 Unity 6（URP），目标设备 iPad Pro。

- 引擎：Unity 6000.0.77f1，URP（Forward+、即时阴影、SSAO、后期处理）
- 场景全部由程序生成：`Assets/Game/Scripts/Boot.cs` 启动，`World.cs` 生成山林、迷宫、寺庙
- 架构说明：`PORTING.md`

## 在 Windows 上打包测试
Unity 菜单 **QF → Build Windows**，产出 `Builds/Windows/QianFoShan.exe`。
加 `-qftest` 参数启动会自动走一遍并截图到 `Shots/`。

## 在 GitHub 上编译 iPad 版
Actions → **Build iPad** → Run workflow。需要先在 Settings → Secrets and variables → Actions 设定：
`UNITY_LICENSE`（Unity_lic.ulf 的内容）、`UNITY_EMAIL`、`UNITY_PASSWORD`。
完成后下载 `QianFoShan-iPad`（未签名 .ipa），用 Sideloadly 以自己的 Apple ID 安装到 iPad。
