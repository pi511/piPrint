# PiPrint 🖨️

[![Platform: Windows](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(x64)-blue.svg)](https://github.com/pi511/piPrint)
[![.NET 8.0](https://img.shields.io/badge/.NET-8.0%20WPF-512BD4.svg)](https://dotnet.microsoft.com/)
[![License: Apache 2.0](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![Tests: Passing](https://img.shields.io/badge/Tests-7%20Passing-brightgreen.svg)](PiPrint.Tests)
[![Built with AI](https://img.shields.io/badge/Built%20with-AI%20Assistance-blueviolet.svg)](#-ai-development-notice)

A modern, lightweight, high-performance Windows print preview and page organizer utility (an open-source alternative to **FinePrint**) built on **.NET 8 (WPF)**.

PiPrint seamlessly intercepts print jobs from **any** Windows application, allowing you to preview in high resolution, delete unwanted pages, remove blank pages automatically, reorganize sheets via drag-and-drop, apply multi-page imposition (2-Up, 4-Up, 8-Up, Booklet), add localized watermarks, convert to toner-saving grayscale, and route final output to any physical or virtual printer or export directly to PDF.

---

## ✨ Key Features

### 🖨️ Universal Print Interception
- **Zero Kernel Drivers:** Installs as a native Windows printer device named **"PiPrint"** using the standard, WHQL-signed `Microsoft XPS Document Writer v4` driver.
- **Works with Every Program:** Notepad, Google Chrome, Mozilla Firefox, Microsoft Edge, Word, Excel, Adobe Acrobat, AutoCAD, and any application with a print dialog.
- **Background Tray Listener:** Runs quietly in the system tray. The moment you print from any application, PiPrint instantly appears with your document loaded.
- **Multi-Job Merging:** Keep PiPrint open and print from different applications sequentially; incoming jobs automatically append into a single combined session.

### 📑 Visual Page Organizer
- **Interactive Thumbnails:** Live vector-rendered page thumbnails in the sidebar.
- **Drag-and-Drop Reordering:** Simply drag page cards in the left sidebar to change page order, or use the **Move Up** / **Move Down** buttons.
- **One-Click Blank Page Removal:** Automatically analyzes pages and removes empty or nearly blank pages.
- **Page Rotation & Deletion:** Rotate individual pages (90° clockwise, 180°) and delete unwanted pages with a single click.

### 📐 Imposition & N-Up Layout Engine
- **1-Up:** Standard full-sheet preview and print.
- **2-Up:** Two pages per landscape sheet with dividing border.
- **4-Up:** Four pages per sheet in a 2x2 grid.
- **8-Up:** Eight pages per sheet in an 8-up grid.
- **Booklet (Saddle-Stitch):** Automates folded front/back sheet order with automatic signature ordering and blank page padding for multi-page booklets.

### 💧 Dynamic Localized Watermarks
- **Localized Presets:** Instant watermark stamps adapted to the selected language:
  - **English:** `DRAFT`, `CONFIDENTIAL`, `COPY`
  - **Türkçe:** `TASLAK`, `GİZLİ`, `KOPYA`
  - **Deutsch:** `ENTWURF`, `VERTRAULICH`, `KOPIE`
  - **Español:** `BORRADOR`, `CONFIDENCIAL`, `COPIA`
- **Custom Watermarks:** Enter any text with adjustable opacity slider and rotation angle.

### 🌿 Ink & Paper Economy
- **Live Paper Savings Meter:** Computes and displays the exact percentage of paper saved in real-time (e.g., `8 pages ➔ 2 sheets (Paper Saved: 75%)`).
- **Grayscale / B&W Mode:** Converts color elements to monochrome to conserve expensive color toner and ink.
- **Manual Duplex Assistant:** For printers without built-in duplex units, prints front pages first, prompts you to flip the stack, then prints back pages.

### 🔍 High-DPI Vector Preview & Shortcuts
- High-fidelity vector rendering for crystal-clear text at any magnification level.
- Full keyboard and mouse navigation:
  - `Ctrl` + `+` / `Ctrl` + `=` / `Numpad +`: Zoom in
  - `Ctrl` + `-` / `Numpad -`: Zoom out
  - `Ctrl` + `0`: Reset zoom / Fit to sheet
  - `Ctrl` + `Mouse Wheel`: Smooth interactive zoom

### 🌐 Multilingual Support
- Automatically detects system language on launch.
- Live language switcher in the interface with zero restarts required:
  - 🇬🇧 English
  - 🇹🇷 Türkçe
  - 🇩🇪 Deutsch
  - 🇪🇸 Español

---

## 🛠️ How It Works

```
┌─────────────────────────┐
│ Any Windows Application │  (Chrome, Word, Notepad, Acrobat, etc.)
└────────────┬────────────┘
             │ Prints to "PiPrint"
             ▼
┌─────────────────────────┐
│ Windows Spooler / Port  │  (Captures spool into %ProgramData%\PiPrint\Spool)
└────────────┬────────────┘
             │ SpoolWatcherService detects completed file
             ▼
┌─────────────────────────┐
│  PiPrint Core Engine    │  • XPS Page Extraction & Vector Rendering
│       (WPF / MVVM)      │  • Imposition Engine (1-Up, 2-Up, 4-Up, Booklet)
│                         │  • Watermarking, Reordering & Duplex Engine
└────────────┬────────────┘
             │ User clicks "Print" or "Save as PDF"
             ▼
┌────────────────────────────────────────────────────────┐
│ Target Printer (Physical HP/Canon/Epson, PDF, or XPS)  │
└────────────────────────────────────────────────────────┘
```

---

## 📦 Installation & Setup

### Option 1: Single-File Setup (`PiPrintSetup.exe`)
Built with Inno Setup 6, suitable for direct distribution or Windows Desktop App packaging (Microsoft Store):
1. Download or build `PiPrintSetup.exe`.
2. Run the installer (supports English, Turkish, German, and Spanish setup wizards).
3. The installer registers the `PiPrint` printer device, sets up background autostart, and creates desktop/start menu shortcuts.

### Option 2: Portable 1-Click Package
1. Extract the portable ZIP distribution package.
2. Right-click `Setup.cmd` and select **Run as Administrator**.
3. The virtual printer and startup service will be registered automatically.

> **Uninstallation:**
> To cleanly remove the virtual printer, spool port, and startup entries, run `Uninstall.cmd` or uninstall from Windows Settings / Add or Remove Programs.

---

## ⌨️ Keyboard Shortcuts

| Shortcut | Action |
| :--- | :--- |
| `Ctrl` + `+` / `Ctrl` + `=` | Zoom In |
| `Ctrl` + `-` | Zoom Out |
| `Ctrl` + `0` | Reset Zoom (Fit to Screen) |
| `Ctrl` + `Mouse Wheel` | Interactive Zoom |
| `Delete` | Remove selected page |

---

## 🏗️ Building from Source

### Prerequisites
- Windows 10 or Windows 11 (64-bit)
- [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- Inno Setup 6 (optional, automatically fetched by build scripts for installer packaging)

### Build Solution
```powershell
# Clone the repository
git clone https://github.com/pi511/piPrint.git
cd piPrint

# Build in Release configuration
dotnet build -c Release
```

### Run Unit Tests
```powershell
dotnet test
```

### Build Single-File Setup Installer
To produce `dist\Setup\PiPrintSetup.exe`:
```powershell
powershell -ExecutionPolicy Bypass -File Scripts\Build-Installer.ps1
```

---

## 🔒 Security & Privacy

- **100% Offline & Private:** PiPrint does not send any telemetry, analytics, or document contents to the internet.
- **Local Spooling:** Spool files are processed entirely in memory and stored locally in `%ProgramData%\PiPrint\Spool\`.
- **Zero Third-Party Drivers:** Uses Windows' built-in Microsoft XPS Document Writer v4 print driver. No kernel-mode drivers or untrusted certificates are installed.

---

## 🤖 AI Development Notice

PiPrint was designed, engineered, and tested with the assistance of AI (**Google DeepMind's Antigravity** agentic coding platform). The architecture, vector rendering pipeline, N-up imposition algorithms, multilingual localization, and packaging toolchains were collaboratively pair-programmed using human guidance, rigorous unit testing, and automated verification.

---

## 📄 License
 
This project is licensed under the [Apache License 2.0](LICENSE).
