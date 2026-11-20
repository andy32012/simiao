# 寺庙 · 霧鎖千佛山（Unity 版）

第一人称恐怖游戏。原本是 three.js 网页版，移植到 Unity 6（URP），目标设备 iPad Pro。

- 引擎：Unity 6000.0.77f1，URP（Forward+、即时阴影、SSAO、后期处理）
- 场景全部由程序生成：`Assets/Game/Scripts/Boot.cs` 启动，`World.cs` 生成山林、迷宫、寺庙
- 架构说明：`PORTING.md`

## 在 Windows 上打包测试
Unity 菜单 **QF → Build Windows**，产出 `Builds/Windows/QianFoShan.exe`。
加 `-qftest` 参数启动会自动走一遍并截图到 `Shots/`。

## 在 GitHub 上编译 iPad 版
1. 在 Windows 上用 Unity 菜单 **QF → Build iOS (Xcode project)** 导出 `Builds/iOS`（需要 iOS Build Support 模块）。
2. 把 `Builds/iOS` 压成 `xcode-project.zip`，上传到 release `ios-latest`。
3. Actions → **Build iPad** → Run workflow。GitHub 的 Mac 会编出未签名的 `QianFoShan-iPad.ipa`，附在同一个 release 上。
4. 用 Sideloadly 以自己的 Apple ID 签名并安装到 iPad。

这个流程不需要在 GitHub 上放 Unity 帐号或授权。
