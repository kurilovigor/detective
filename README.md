# DETECTive

**DETECTive** is a modern, lightweight Windows desktop application designed for image annotation and object detection labeling. Built with .NET 10 and WPF-UI Fluent Design, DETECTive provides a streamlined workflow for preparing computer vision datasets with native export to the YOLO format.

---

## Table of Contents

- [Overview](#overview)
- [Main Menu Functions](#main-menu-functions)
  - [Home (🏠)](#home-)
  - [File Menu](#file-menu)
  - [Edit Menu](#edit-menu)
  - [Images Menu](#images-menu)
- [Workspace & Panels](#workspace--panels)
  - [Label Editor (Canvas)](#label-editor-canvas)
  - [Labels Panel](#labels-panel)
  - [Files Panel](#files-panel)
  - [Status Bar](#status-bar)
- [Keyboard Shortcuts & Navigation](#keyboard-shortcuts--navigation)
  - [Shortcuts Cheatsheet](#shortcuts-cheatsheet)
  - [Mouse & Canvas Controls](#mouse--canvas-controls)
- [YOLO Dataset Export](#yolo-dataset-export)
- [Application Settings](#application-settings)
- [Tech Stack & Architecture](#tech-stack--architecture)
- [Getting Started](#getting-started)

---

## Overview

DETECTive simplifies bounding box annotation for machine learning and computer vision projects:
- **Embedded Database Storage**: Projects are stored as self-contained `.detective` files using LiteDB, keeping all image references, class definitions, and bounding box coordinates unified in a single file without external database configuration.
- **Fluent Windows Experience**: Clean UI powered by [WPF-UI](https://github.com/lepoco/wpfui) following Windows 11 Fluent design principles, with automatic system dark/light theme and accent color synchronization.
- **YOLO Export Pipeline**: One-click generation of standard YOLO directory structures (`images/train`, `images/val`, `labels/train`, `labels/val`), normalized coordinate annotations, and `data.yml` configuration files.

---

## Main Menu Functions

The main menu is accessible directly from the application title bar at the top of the window.

```
[ 🏠 Home ]  [ File ▾ ]  [ Edit ▾ ]  [ Images ▾ ]
```

### Home (🏠)
Clicking the green home icon button switches the current view to the **Welcome / Home Page**:
- **New Project**: Quick action to create a new project file.
- **Open Project**: Quick action to browse and open an existing `.detective` file.
- **Recent Files List**: Shows recently opened projects with double-click support for fast resumption.

---

### File Menu

| Item | Shortcut | Description |
| :--- | :--- | :--- |
| **New...** | `Ctrl+N` | Prompts for a save path to create a brand new `.detective` project file, adds it to recent files, and opens the workspace. |
| **Open...** | `Ctrl+O` | Opens a file picker to select an existing `.detective` project file and loads its labels, images, and annotations. |
| **Recent Files** | — | Dynamically lists up to the 5 most recently opened projects. Clicking any item opens that project directly. |
| **Export > To YOLO...** | — | Saves any in-flight edits on the current image, verifies the active database, and prompts to save a `data.yml` configuration file. Automatically outputs image copies, normalized coordinate label files (`.txt`), and the full YOLO directory structure. *(Enabled when an active project is loaded)* |
| **Settings...** | — | Opens the application **Settings** page to configure startup preferences (e.g., whether to open the Home page or resume the last project upon launch). |
| **Exit** | — | Gracefully shuts down the application while preserving window position, dimensions, maximized state, and sidebar layout settings. |

---

### Edit Menu

The Edit menu provides annotation history and selection operations within the active image editor.

| Item | Shortcut | Description |
| :--- | :--- | :--- |
| **Undo** | `Ctrl+Z` | Reverts the last annotation action (bounding box creation, move, resize, or deletion). *(Enabled when undo history is available)* |
| **Redo** | `Ctrl+Y` | Re-applies the most recently undone action. *(Enabled when redo history is available)* |
| **Delete Label** | `Delete` | Deletes the currently selected bounding box from the active image canvas. *(Enabled when a bounding box is selected)* |

---

### Images Menu

The Images menu manages image datasets within the current project.

| Item | Shortcut | Description |
| :--- | :--- | :--- |
| **Add Files...** | — | Opens an open file dialog with multiselect support to add individual image files (`*.jpg`, `*.jpeg`, `*.png`, `*.bmp`) to the project. *(Enabled when an active project is loaded)* |
| **Add Folder...** | — | Opens a folder browser dialog to recursively scan an entire directory and import all supported images into the project. *(Enabled when an active project is loaded)* |
| **Delete file** | — | Prompts for confirmation and deletes the currently selected image and its saved detections from the project database. *(Enabled when an image is selected)* |
| **Delete All** | — | Prompts for confirmation and clears all images and detections from the current project. *(Enabled when project contains images)* |
| **Prior...** | — | Selects the previous image in the file list. If at the top or nothing is selected, wraps or selects the last item. *(Enabled when files exist)* |
| **Next...** | — | Selects the next image in the file list. *(Enabled when files exist)* |

---

## Workspace & Panels

When a project is open, the main workspace divides into a customizable two-column view separated by a draggable splitter.

### Label Editor (Canvas)
The primary annotation surface on the right:
- **Interactive Box Creation**: Click and drag over the image to draw a new bounding box. The currently selected default label is applied automatically.
- **Resize Grips**: Selecting any box reveals 8 corner/edge resize handles for precise boundary adjustments.
- **Box Movement**: Click and drag inside an existing box to reposition it.
- **Hover & Selection Feedback**: Highlights boxes under the cursor and shows visual borders for the selected box.

### Labels Panel
Located at the top of the left sidebar (collapsible via the header chevron):
- **Add Label**: Enter a class name into the input field and press `Enter` or click the `+` button. Duplicate names are automatically prevented.
- **Label Selection**: Click any label chip to set it as the active class for new bounding boxes.

### Files Panel
Located at the bottom of the left sidebar (collapsible via the header chevron):
- **Search & Filter**: Type into the search box to dynamically filter image files by name.
- **Detection Badges**: Each image entry features an `InfoBadge` indicating the count of labeled objects on that image.
- **Quick Buttons**: Dedicated header buttons for adding files (`ImageAdd24`) and folders (`FolderAdd24`), plus an inline delete button per row.

### Status Bar
Located underneath the image canvas:
- Displays real-time geometry and class information for the currently selected bounding box:
  ```
  X:120 | Y:85 | W:310 | H:240 | Label:vehicle
  ```

---

## Keyboard Shortcuts & Navigation

### Shortcuts Cheatsheet

| Shortcut | Context | Action |
| :--- | :--- | :--- |
| `Ctrl+N` | Global | New project |
| `Ctrl+O` | Global | Open project |
| `Ctrl+Z` | Editor | Undo annotation change |
| `Ctrl+Y` | Editor | Redo annotation change |
| `Delete` | Editor | Delete selected bounding box |
| `Enter` | Labels Panel | Add label entered in text box |

### Mouse & Canvas Controls

| Input | Action |
| :--- | :--- |
| **Left Click + Drag** *(empty area)* | Draw a new bounding box with the active label |
| **Left Click** *(on box)* | Select the bounding box |
| **Left Click + Drag** *(on box)* | Move the selected bounding box |
| **Left Click + Drag** *(on grip)* | Resize the selected bounding box |
| **Mouse Wheel Scroll** | Navigate quickly to the **Next** (scroll down) or **Previous** (scroll up) image |

---

## YOLO Dataset Export

When selecting **File > Export > To YOLO...**, DETECTive automatically:

1. **Compiles Class Indices**: Orders all unique labels defined in the project.
2. **Generates `data.yml`**:
   ```yaml
   path: <dataset-folder>
   train: images/train
   val: images/val
   nc: 3
   names: ['person', 'vehicle', 'bicycle']
   ```
3. **Creates the YOLO Directory Tree**:
   ```
   <dataset-folder>/
   ├── data.yml
   ├── images/
   │   ├── train/
   │   └── val/
   └── labels/
       ├── train/
       └── val/
   ```
4. **Converts Coordinates & Copies Assets**:
   - Copies annotated images into `images/train/`.
   - Generates matching `<image-name>.txt` files in `labels/train/` with normalized bounding box coordinates:
     ```
     <class_id> <x_center_ratio> <y_center_ratio> <width_ratio> <height_ratio>
     ```

---

## Application Settings

Accessible via **File > Settings...**:
- **Startup Page**: Choose which page displays when the application starts:
  - `Home`: Always launch onto the Welcome screen.
  - `Last`: Automatically reopen the most recently opened project on startup (falls back to Home if no recent files exist).
- **Persistent State**: Window dimensions, window state (normal/maximized), and sidebar splitter width are automatically saved upon exit and restored on the next session.

---

## Tech Stack & Architecture

- **Runtime & Framework**: [.NET 10.0](https://dotnet.microsoft.com/) Windows Desktop (WPF) with Windows Forms integration for folder dialogs.
- **UI Framework**: [WPF-UI 4.3](https://wpfui.lepo.co/) (Fluent Design System, Windows 11 icons and styling).
- **Embedded Database**: [LiteDB](https://www.litedb.org/) for local NoSQL project file storage.
- **Serialization**: [SharpYaml](https://github.com/xoofx/SharpYaml) for standard YAML dataset configuration export; `System.Text.Json` for application settings.
- **Unit Testing**: MSTest with complete test suites covering YOLO export generation and coordinate conversion.

---

## Getting Started

### Prerequisites
- Windows 10 (version 1809 or higher) or Windows 11
- [.NET 10.0 SDK](https://dotnet.microsoft.com/download)

### Build and Run

Clone the repository and build using the .NET CLI:

```powershell
# Restore dependencies and build
dotnet build

# Run the application
dotnet run --project Detective\Detective.csproj

# Run test suite
dotnet test
```

