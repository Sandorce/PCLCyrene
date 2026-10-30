# PCL 昔涟版（PCL Cyrene）

基于 [PCL-CE](https://github.com/PCL-Community/PCL-CE) 修改的 Minecraft 启动器，
界面与工具围绕「昔涟」做了一整套定制。

> 遵循上游《PCL 分发有限许可》：名称以 PCL 开头、默认主色相为蓝色、源码公开。

## 功能

- 粉色 / 蓝色主题可切换（默认蓝）
- 樱花穿透联机（内置 frpc）
- 整合包直装（.mrpack → 完全离线包）与实例导出
- 存档备份 / 启动前自检 / 日志翻译 / 崩溃诊断
- 简单开服（自动 eula、后台运行、显示控制台）
- 资源库（光影、资源包一键部署）
- 微软正版登录开箱可用

## 下载

见 [Releases](https://github.com/Sandorce/PCLCyrene/releases)：
PCLCyrene-Setup.exe（安装包）、PCLCyrene-Update.zip（更新包）。

## 结构

`
Plain Craft Launcher 2\   主程序（上游代码 + 少量接入点）
PCL.Cyrene\               定制模块（窗口、工具、版本与更新公告）
PCL.Core\                 核心库（上游）
`

## 致谢

- [Plain Craft Launcher](https://github.com/Meloong-Git/PCL) —— 龙腾猫跃
- [PCL-CE](https://github.com/PCL-Community/PCL-CE) —— PCL 社区

本版本仅做界面与工具层修改，未移除任何上游署名。