[简体中文] | [English](./README.en-US.md)

# FolderStyleEditorForWindows

---

<div align="center">
    <img src="docs/images/FSM_Image.png" alt="FSM_Image" width="128"/>
    <br/>
    <br/>
    <strong>[ 现代化的一个 Windows 文件夹样式编辑器 ]</strong>
</div>
<br/>
<br/>
<div align="center">
    <a href="https://github.com/PingGai/FolderStyleEditorForWindows/releases">
      <img alt="GitHub release" src="https://img.shields.io/github/v/release/PingGai/FolderStyleEditorForWindows?display_name=release">
    </a>
    <a href="https://github.com/PingGai/FolderStyleEditorForWindows/stargazers">
      <img alt="GitHub Repo stars" src="https://img.shields.io/github/stars/PingGai/FolderStyleEditorForWindows">
    </a>
</div>
<div align="center">
    <a href="https://github.com/PingGai/FolderStyleEditorForWindows/releases">
      <img alt="Release date" src="https://img.shields.io/github/release-date/PingGai/FolderStyleEditorForWindows?display_date=published_at">
    </a>
    <a href="https://github.com/PingGai/FolderStyleEditorForWindows/commits">
      <img alt="Commit activity (month)" src="https://img.shields.io/github/commit-activity/m/PingGai/FolderStyleEditorForWindows">
    </a>
    <a href="https://github.com/PingGai/FolderStyleEditorForWindows/commits">
      <img alt="Last commit" src="https://img.shields.io/github/last-commit/PingGai/FolderStyleEditorForWindows">
    </a>
</div>
<div align="center">
    <img alt="GitHub Downloads (all assets, all releases)" src="https://img.shields.io/github/downloads/PingGai/FolderStyleEditorForWindows/total">
    <img alt="GitHub Downloads (all assets, latest release)" src="https://img.shields.io/github/downloads/PingGai/FolderStyleEditorForWindows/latest/total">
</div>

---

这是一个可以快速修改 Windows 系统上文件夹别名，以及速将一个应用的文件夹设置成它内部的应用图标的工具。

- 修改别名：修改其显示名称，但是不修改路径名称，基于Windows Desktop.ini配置项实现
- 修改图标：使用相对路径调用文件夹内的文件的图标，如果选择文件夹外的图标或有图标的文件，将会提取图标并放在当前文件夹的.ICON下

## 快速概览

### 项目描述

FolderStyleEditorForWindows 是一款基于 Avalonia UI 框架和 .NET 9.0 开发的桌面应用程序，旨在为用户提供一个现代化、美观的界面，以便通过对desktop.ini进行可视化编辑，轻松自定义 Windows 文件夹的样式，包括修改文件夹的别名和图标。

## 特色

### 已实现功能

* **图标路径便携化**: 图标采用相对路径存储，确保移动文件夹（如存入U盘）后样式依然生效。外部图标会自动存入文件夹内的 `.ICON` 隐藏目录，实现一体化管理。
* **全方位拖拽操作**:
  - **目标**: 拖拽 **文件夹** 以指定编辑目标。
  - **图标**: 拖拽 `.ico`, `.exe`, `.dll` 文件以提取和设置图标。
  - **别名**: 拖拽 **文本** 以快速填充别名。
* **图标智能识别**: 一键扫描并展示文件夹内 `.exe` 或 `.dll` 包含的所有图标。本地图标只引用路径，不冗余复制。
* **别名快速编辑**: 直观地修改文件夹的显示名称。

### 阶段性目标

* **高级权限处理**: 支持对需要管理员权限的系统文件夹进行修改。
* **一键与批量操作**: 拖入含应用的可执行文件，一键应用其图标；支持对多个文件夹进行批量处理。
* **图片自动转换**: 支持将 `.png`, `.jpg` 等格式的图片自动转换为 `.ico` 图标。

### 技术栈

- **C# / .NET 9.0**: 构建高性能 Windows 桌面应用。
- **Avalonia UI**: 跨平台 UI 框架，提供现代化的用户界面。

## 安装与运行指南

### 环境要求

- **操作系统**: Windows 10/11

### 构建

#### 方式一：从 GitHub Releases 下载 (稳定版)

我们推荐直接从 [GitHub Releases](https://github.com/PingGai/FolderStyleEditorForWindows/releases) 页面下载最新的预编译版本。这是最简单、最快捷的获取方式。

#### 方式二：获取最新开发版 (通过 Actions)

如果您想体验最新的功能，可以 Fork 本仓库，并利用 GitHub Actions 自行构建：

1. **Fork 仓库**: 点击本页面右上角的 "Fork" 按钮，将此仓库复制到您自己的 GitHub 账户下。
2. **启用 Actions**: 在您 Fork 的仓库页面，进入 "Settings" > "Actions" > "General"，选择 "Allow all actions and reusable workflows" 并保存。
3. **运行工作流**:
   * 进入 "Actions" 标签页。
   * 在左侧找到名为 "Build Application" 的工作流，并点击它。
   * 点击 "Run workflow" 下拉框，然后再次点击绿色的 "Run workflow" 按钮。
4. **下载产物**: 等待工作流运行完毕后，在工作流的 "Summary" 页面找到名为 "FolderStyleEditorForWindows-Executables" 的构建产物 (Artifacts)，点击下载即可。

#### 方式三：本地手动构建

如果您想自行构建，可以使用项目根目录 `build/` 下的 `build.ps1` 脚本。该脚本会自动处理依赖，并将应用打包为带版本号的单个可执行文件。

1. **确保环境**:
   * 安装 [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)。
   * Windows PowerShell 5.1 或 PowerShell 7+。
2. **执行脚本**:
   打开 PowerShell 终端，并运行以下命令：
   ```powershell
   # 构建所有支持的版本 (x64 和 x86)
   .\build\build.ps1
   ```

   构建成功后，应用将输出到 `publish/` 目录下，并根据 `build/version.txt` 的内容自动命名。

#### 其他方式：使用 Visual Studio

1. **克隆仓库**:
   ```bash
   git clone https://github.com/PingGai/FolderStyleEditorForWindows
   cd FolderStyleEditorForWindows
   ```
2. **打开解决方案**:
   使用 **Visual Studio 2022** 或更高版本打开 `FolderStyleEditorForWindows.sln`。
3. **构建项目**:
   在 Visual Studio 中，选择 `Release` 配置并点击 "生成解决方案"。这将在 `FolderStyleEditorForWindows_Avalonia/bin/Release` 目录下生成零散的程序文件。
4. **运行程序**:
   可以直接在 Visual Studio 中启动 `FolderStyleEditorForWindows_Avalonia` 项目进行调试。

## 如何使用

### 1. 拖拽文件夹进入应用

- **拖拽到主界面**: 将您想要自定义的文件夹直接拖拽到应用程序的主界面。应用程序将自动识别并进入该文件夹的编辑页面。
- **点击选择文件夹**: 您也可以点击主界面的“点击以选择文件夹”区域，通过文件浏览器选择目标文件夹。

### 2. 编辑文件夹别名

- 进入编辑页面后，您会看到一个“文件夹别名”输入框。
- 直接在输入框中输入您想要的文件夹别名。
- 您也可以将包含文本的拖拽到此窗口上以自动填充别名。

### 3. 编辑文件夹图标

- 在编辑页面，找到“文件夹图标”部分。
- **拖拽图标文件**: 将 `.ico` 文件、包含图标的 `.exe` 或 `.dll` 文件直接拖拽到图标输入框区域。(可能暂未完全实现)
  - 如果是 `.exe` 或 `.dll` 文件，应用程序将自动提取其中的图标。
  - 应用默认会使用相对路径，如果图标文件不在当前文件夹内，应用会在本文件夹内创建一个.ICON隐藏文件夹用于存储外部来的图标。
- **点击选择图标**: 点击图标输入框右侧的文件夹图标按钮，通过文件浏览器选择图标文件。
- **自动获取图标**: 应用程序会自动解析选定的 `.exe` 或 `.dll` 文件中的所有图标，并在下方以缩略图形式展示。您可以从中选择一个图标，应用程序会自动将其路径填充到输入框中。
- **重置图标**: 如果您想清除当前图标设置，可以使用“重置图标”按钮，这将使文件夹恢复默认图标。

## 图片

<div align="center">
    <strong>头图</strong>
</div>

<div align="center">
  <img
    src="https://github.com/user-attachments/assets/8609067c-4082-43c7-b565-7ecb0b7ba503"
    alt="QQ_1764657539808"
    style="width: 95%; height: auto;"
  />
</div>

<div align="center">
    <strong>软件界面</strong>
</div>

<div align="center">
  <img
    src="https://github.com/user-attachments/assets/ea46dabf-dc9d-4ebe-ac91-992131b13696"
    alt="f6928316-c41a-408e-a895-1b416a98698f"
    style="width: 90%; height: auto;"
  />
</div>

<div align="center">
    <strong>修改后的文件夹样式</strong>
</div>

<div align="center">
  <img
    src="https://github.com/user-attachments/assets/ae2dacae-1259-450a-b350-d69f89ea8548"
    alt="QQ_1759019766132"
    style="width: 50%; height: auto;"
  />
</div>

<div align="center">
    你可以利用这个软件轻松将一个应用的文件夹修改成应用的图标
</div>

## 技术概述

本项目采用 C# 和 .NET 9.0 开发，界面使用 Avalonia UI 框架构建，并遵循 MVVM 设计模式。通过这种架构，实现了模块化的代码结构高效的数据绑定。

## 项目性质

本项目为**实验性项目**，有两个目标

- 其一：实现基本的软件功能
- 其二：探索从自然语言策划书直接生成桌面小型应用的可行性

## 已知问题 / 未来规划

目前尚未实现自由对需要管理员权限的目录进行编辑，直接给予主进程管理员身份运行会导致无法接收拖拽，是一个很严重的问题，目前暂未设计出可行性办法

## 贡献指南

如果不反感这个项目的性质，欢迎对项目进行贡献！如果您有任何建议、功能请求或 Bug 报告，请随时提交 Issue 或 Pull Request。

## 许可证

本项目遵循 [MIT 许可证](LICENSE)。第三方库的许可证信息请查阅[`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) 与 [`LICENSES/`](LICENSES/) 目录。
