[简体中文](./README.md) | [English]

# FolderStyleEditorForWindows

---

<div align="center">
    <img src="docs/images/FSM_Image.png" alt="FSM_Image" width="128"/>
    <br/>
    <br/>
    <strong>[&nbsp;A Modern Windows Folder Style Editor&nbsp;]</strong>
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

This is a tool to quickly modify folder aliases on Windows systems, and to quickly set a folder's icon to that of an application inside it.

- **Modify Alias**: Change its display name without modifying the path name, based on the Windows Desktop.ini configuration.
- **Modify Icon**: Use a relative path to call the icon of a file within the folder. If an icon or a file with an icon is selected from outside the folder, the icon will be extracted and placed under the `.ICON` directory in the current folder.

## Quick Overview

### Project Description
FolderStyleEditorForWindows is a desktop application developed with the Avalonia UI framework and .NET 9.0. It aims to provide users with a modern, beautiful interface to easily customize the style of Windows folders by visually editing `desktop.ini`, including modifying folder aliases and icons.

## Features

### Implemented Features

*   **Portable Icon Paths**: Icons are stored with relative paths, ensuring styles remain intact when folders are moved (e.g., to a USB drive). External icons are auto-saved to a hidden `.ICON` folder for self-contained management.
*   **Comprehensive Drag-and-Drop**:
    -   **Target**: Drag a **folder** to specify the editing target.
    -   **Icon**: Drag `.ico`, `.exe`, or `.dll` files to extract and set icons.
    -   **Alias**: Drag **text** to quickly populate the alias.
*   **Smart Icon Recognition**: One-click scan to display all icons from `.exe` or `.dll` files within a folder. Local icons are referenced by path, not duplicated.
*   **Quick Alias Editing**: Intuitively modify the folder's display name.

### Roadmap

*   **Advanced Permission Handling**: Support for modifying system folders that require administrator privileges.
*   **One-Click & Batch Operations**: Instantly apply an app's icon by dragging its executable; support for batch processing multiple folders.
*   **Automatic Image Conversion**: Convert images like `.png` and `.jpg` to `.ico` format automatically.

### Tech Stack
- **C# / .NET 9.0**: For building high-performance Windows desktop applications.
- **Avalonia UI**: A cross-platform UI framework for modern user interfaces.

## Installation and Running Guide

### System Requirements
- **Operating System**: Windows 10/11

### Building

#### Method 1: Download from GitHub Releases (Stable)

We recommend downloading the latest pre-compiled version directly from the [GitHub Releases](https://github.com/PingGai/FolderStyleEditorForWindows/releases) page. This is the easiest and fastest way to get the application.

#### Method 2: Get the Latest Development Build (via Actions)

If you want to try the latest features, you can fork this repository and build it yourself using GitHub Actions:

1.  **Fork the Repository**: Click the "Fork" button at the top right of this page to copy this repository to your own GitHub account.
2.  **Enable Actions**: In your forked repository, go to "Settings" > "Actions" > "General", select "Allow all actions and reusable workflows", and save.
3.  **Run the Workflow**:
    *   Go to the "Actions" tab.
    *   Find the workflow named "Build Application" on the left and click on it.
    *   Click the "Run workflow" dropdown, then click the green "Run workflow" button again.
4.  **Download the Artifact**: After the workflow is complete, find the artifact named "FolderStyleEditorForWindows-Executables" on the workflow's "Summary" page and click to download it.

#### Method 3: Build it Yourself Locally

If you prefer to build the application yourself, you can use the `build.ps1` script located in the `build/` directory. This script handles all dependencies and packages the application into a single executable file with a version number.

1.  **Ensure Environment**:
    *   Install [.NET 9 SDK](https://dotnet.microsoft.com/download/dotnet/9.0).
    *   Windows PowerShell 5.1 or PowerShell 7+.
2.  **Execute the Script**:
    Open a PowerShell terminal and run the following command:
    ```powershell
    # Build all supported versions (x64 and x86)
    .\build\build.ps1
    ```
    After a successful build, the application will be available in the `publish/` directory, automatically named according to the content of `build/version.txt`.

#### Alternative Method: Use Visual Studio

1.  **Clone the repository**:
    ```bash
    git clone https://github.com/PingGai/FolderStyleEditorForWindows
    cd FolderStyleEditorForWindows
    ```
2.  **Open the solution**:
    Open `FolderStyleEditorForWindows.sln` with **Visual Studio 2022** or a later version.
3.  **Build the project**:
    In Visual Studio, select the `Release` configuration and click "Build Solution". This will generate the application files in the `FolderStyleEditorForWindows_Avalonia/bin/Release` directory.
4.  **Run the program**:
    You can directly start the `FolderStyleEditorForWindows_Avalonia` project from within Visual Studio for debugging.

## How to Use

### 1. Drag a folder into the application
- **Drag to the main interface**: Drag the folder you want to customize directly onto the main interface of the application. The application will automatically recognize it and enter the folder's editing page.
- **Click to select a folder**: You can also click the "Click to select a folder" area on the main interface to select the target folder through the file explorer.

### 2. Edit the folder alias
- After entering the editing page, you will see a "Folder Alias" input box.
- Directly enter the desired folder alias in the input box.
- You can also drag text into this window to automatically fill in the alias.

### 3. Edit the folder icon
- On the editing page, find the "Folder Icon" section.
- **Drag icon file**: Drag a `.ico` file, or an `.exe` or `.dll` file containing icons, directly to the icon input box area. (This may not be fully implemented yet)
    - If it is an `.exe` or `.dll` file, the application will automatically extract the icons within it.
    - The application uses relative paths by default. If the icon file is not in the current folder, the application will create a hidden `.ICON` folder within this folder to store external icons.
- **Click to select icon**: Click the folder icon button to the right of the icon input box to select an icon file through the file explorer.
- **Auto-get icon**: The application will automatically parse all icons in the selected `.exe` or `.dll` file and display them as thumbnails below. You can select one of them, and the application will automatically fill its path into the input box.
- **Reset icon**: If you want to clear the current icon setting, you can use the "Reset Icon" button, which will restore the folder to its default icon.

## Images

<div align="center">
    <strong>Software Interface</strong>
</div>

<img width="1061" height="800" alt="image" src="https://github.com/user-attachments/assets/f953988f-4d0c-47d9-ae4e-dd2b572fed51" />

<div align="center">
    <strong>Modified Folder Style</strong>
</div>

<img width="238" height="205" alt="QQ_1759019766132" src="https://github.com/user-attachments/assets/ae2dacae-1259-450a-b350-d69f89ea8548" />


## Technical Overview

This project is developed using C# and .NET 9.0, with the interface built using the Avalonia UI framework, and follows the MVVM design pattern. This architecture achieves a modular code structure and efficient data binding.

## Project Nature

This is an **experimental project** with two goals:
- To implement the basic software functions.
- To explore the feasibility of directly generating small desktop applications from natural language project plans.

## Known Issues / Future Plans

Editing directories that require administrator privileges is not yet implemented. Running the main process with administrator rights directly causes it to be unable to receive drag-and-drop, which is a serious problem. A feasible solution has not yet been designed.

## Contribution Guide

If you are not opposed to the nature of this project, you are welcome to contribute! If you have any suggestions, feature requests, or bug reports, please feel free to submit an Issue or Pull Request.

## License

This project is licensed under the [MIT License](LICENSE). For license information of third-party libraries, please refer to the [`THIRD-PARTY-NOTICES.md`](THIRD-PARTY-NOTICES.md) and [`LICENSES/`](LICENSES/) directory.
