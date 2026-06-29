<p align="center">
  <img src="Assets/logo.png" width="120" height="120" alt="MemOptimizer Logo"/>
</p>

<h1 align="center">MemOptimizer</h1>

<p align="center">
  轻量级 Windows 内存优化工具 · 一键释放系统内存
</p>

<p align="center">
  <img src="https://img.shields.io/badge/License-MIT-blue.svg" alt="License"/>
  <img src="https://img.shields.io/badge/Platform-Windows-lightgrey.svg" alt="Platform"/>
  <img src="https://img.shields.io/badge/.NET-8.0-512BD4.svg" alt=".NET"/>
  <img src="https://img.shields.io/badge/C%23-12.0-239120.svg" alt="C#"/>
  <img src="https://img.shields.io/badge/UI-WPF-68217A.svg" alt="WPF"/>
</p>

---

## 简介

MemOptimizer 是一款轻量级的 Windows 内存优化工具，通过 Windows 系统级 API（`NtSetSystemInformation`）清理系统文件缓存，一键释放被占用的内存。核心优化逻辑提取自 PCL2 启动器的内存优化功能，经过独立重构和优化。

## 功能特性

- **一键内存优化** — 基于 `NtSetSystemInformation` 系统调用，清理系统文件缓存
- **实时内存监控** — 环形进度条展示内存使用率，每秒刷新
- **深色主题 UI** — 现代化深色界面，流畅的交互动画
- **自动管理员提权** — 通过 manifest 自动请求管理员权限
- **开箱即用** — 单文件发布，无需安装依赖

## 技术栈

| 项目 | 技术 |
|------|------|
| 开发语言 | C# 12.0 |
| UI 框架 | WPF (.NET 8) |
| 架构模式 | MVVM |
| 核心 API | NtSetSystemInformation / AdjustTokenPrivileges |

## 下载与使用

### 方式一：直接下载

前往 [Releases](https://github.com/Warm-winter/MemOptimizer/releases) 页面下载最新版本，双击运行即可。

### 方式二：自行编译

```bash
# 克隆仓库
git clone https://github.com/Warm-winter/MemOptimizer.git
cd MemOptimizer

# Debug 构建
dotnet build

# Release 单文件发布
dotnet publish -c Release
```

发布后的 exe 位于 `bin\Release\net8.0-windows\win-x64\publish\` 目录。

## 核心原理

1. **权限提升**：获取 `SeProfileSingleProcessPrivilege` 权限
2. **系统调用**：循环 4 次调用 `NtSetSystemInformation(class=80)`，清理系统文件缓存
3. **内存统计**：通过 `GlobalMemoryStatusEx` 对比优化前后可用内存

## 许可证

[MIT License](LICENCE) · Copyright © 2026 Warm-winter
