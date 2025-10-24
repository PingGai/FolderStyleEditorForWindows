[简体中文](./README.md) | [English]

# FolderStyleEditorForWindows

<div align="center">
    <img src="docs/images/FSM_Image.png" alt="FSM_Image" width="512"/>
    <br/>
    <strong>A Modern Windows Folder Style Editor</strong>
</div>

This is a tool that allows you to quickly modify folder aliases on Windows systems and set a folder's icon to the icon of an application within it.

## Quick Overview

### Project Description
FolderStyleEditorForWindows is a desktop application developed with the Avalonia UI framework and .NET 9.0. It aims to provide users with a modern, beautiful interface to easily customize the style of Windows folders by visually editing `desktop.ini`, including modifying folder aliases and icons.

### Core Features
- **Modify Folder Alias**: Quickly set or change the display name of a folder.
- **Customize Folder Icon**: Supports setting an application's internal icon as the folder icon, or using custom icons (supports icons from ICO, EXE, DLL files, etc.).
- **Drag and Drop**: Supports global drag-and-drop, allowing users to drag folders and icon files directly into the application window.
- **Intuitive UI**: Adopts a modern UI design for a smooth user experience.

### Tech Stack
- **C# / .NET 9.0**: For building high-performance Windows desktop applications.
- **Avalonia UI**: A cross-platform UI framework for modern user interfaces.

### Key Features
- **Global Drag-and-Drop with Visual Feedback**: The entire window area supports file drag-and-drop with rich visual feedback.
- **Permission Management**: Intelligently handles folder modification permissions.

## Installation and Running Guide

### System Requirements
- **Operating System**: Windows 10/11

### Building
1. **Clone the repository**:
   ```bash
   git clone https://github.com/PingGai/FolderStyleEditorForWindows
   cd FolderStyleEditorForWindows
   ```
2. **Open the solution**:
   Open the `2025-8-23_Develop_WindowsFolderStyle.sln` solution file with Visual Studio 2022 or later.
3. **Restore NuGet packages**:
   Visual Studio will automatically restore the required NuGet packages.
4. **Build the project**:
   In Visual Studio, select "Build" -> "Build Solution". You can also use the packaging script in the `build/` directory (requires .NET 9 development environment to be installed first).
5. **Run the program**:
   After a successful build, you can run the `WindowsFolderStyleEditor_Avalonia` project.

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

This project is developed using C# and .NET 9.0, with the interface built using the Avalonia UI framework, and follows the MVVM design pattern. This architecture achieves a modular code structure, a responsive user interface, and efficient data binding.

## Project Nature

This is an **experimental project** with two goals:
- To implement the basic software functions.
- To explore the feasibility of directly generating small desktop applications from natural language project plans.

## Known Issues / Future Plans

Editing directories that require administrator privileges is not yet implemented. Running the main process with administrator rights directly causes it to be unable to receive drag-and-drop, which is a serious problem. A feasible solution has not yet been designed.

## Contribution Guide

If you are not opposed to the nature of this project, you are welcome to contribute! If you have any suggestions, feature requests, or bug reports, please feel free to submit an Issue or Pull Request.

## License

This project is licensed under the [MIT License](LICENSE). For license information of third-party libraries, please refer to the [`LICENSES/`](LICENSES/) directory.