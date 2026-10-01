# SelectAI — Screen Selection & AI Assistant

[![Live Website](https://img.shields.io/badge/Website-select--ai--bay.vercel.app-00F0FF?style=for-the-badge&logo=vercel)](https://select-ai-bay.vercel.app)
[![Download Release](https://img.shields.io/github/v/release/bharath-0814/SelectAI?style=for-the-badge&color=0284C7&label=Download%20v1.0.0)](https://github.com/bharath-0814/SelectAI/releases/latest)
[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg?style=for-the-badge)](LICENSE)

**SelectAI** is a premium, native Windows 11 desktop utility inspired by Samsung Galaxy Book AI Select. It allows you to press a global hotkey (`Ctrl + Shift + Space`) from anywhere in the operating system, draw or circle anything on your screen with a glowing neon tracer, and perform intelligent actions on the selected content.

🌐 **Live Showcase & Interactive Demo**: [https://select-ai-bay.vercel.app](https://select-ai-bay.vercel.app)  
📦 **Download Standalone .zip**: [SelectAI-v1.0.0-win-x64.zip](https://github.com/bharath-0814/SelectAI/releases/download/v1.0.0/SelectAI-v1.0.0-win-x64.zip)

---

## ✨ Features

- **Signature Freeform Circle / Trace Selection**:
  - Draw a circle, ellipse, or irregular loop around any UI element, error message, or image.
  - Real-time anti-aliased Catmull-Rom / Chaikin spline smoothing eliminates mouse jitters.
  - Multi-layer luminous neon stroke: outer bloom halo, electric cyan mid-layer, razor-sharp white core, and a trailing star comet effect.
  - Dynamic cutout effect: the circled region is bright and crystal-clear while the rest of the screen is softly dimmed.
- **Multiple Selection Modes**:
  - 〰️ **Freeform**: Freehand drawing and circling.
  - 🔲 **Rectangle**: Standard rectangular crop with camera brackets.
  - 📝 **Text**: Optimizes focus for immediate text recognition.
  - 🖼️ **Image**: Visual capture mode.
  - Switch quickly via UI capsule or keyboard keys `1`, `2`, `3`, `4`.
- **System-Wide Global Hotkey**:
  - Default: `Ctrl + Shift + Space`.
  - Operates system-wide across all applications, games, and full-screen windows using Windows user-mode Win32 `RegisterHotKey`.
- **Offline High-Performance Windows Native OCR**:
  - Powered by Windows 10/11 native `Windows.Media.Ocr.OcrEngine`.
  - 100% offline, local, private, and instantaneous.
- **Smart Action Detection**:
  - Automatically identifies URLs (`Open Link`), emails (`Compose Email`), phone numbers, programming errors/code (`Explain Code`), and search queries.
- **Ask AI (Multimodal Provider Abstraction)**:
  - Clean `IAIProvider` interface with multi-modal support (sends high-res image + OCR text + prompt).
  - Supported providers:
    - **SelectAI Built-in Assistant**: Works out-of-the-box offline with zero API keys required.
    - **Google Gemini**: Supports `gemini-2.0-flash`, `gemini-1.5-flash`, `gemini-1.5-pro`.
    - **OpenAI**: Supports `gpt-4o`, `gpt-4o-mini`.
  - Floating card with quick prompt chips: *Explain this*, *Summarize*, *Debug code*, *Translate*.
- **Google Search & Google Lens**:
  - Instant search using the user's default browser (never hardcodes Edge or Bing).
  - One-click Google Lens visual search.
- **System Tray & Windows 11 Fluent Settings**:
  - Runs silently in the background with a system tray icon.
  - Windows DPAPI encryption protects all API keys in `%APPDATA%\SelectAI\settings.json`.
  - Optional "Launch on Windows startup" toggle.
- **Multi-Monitor & High-DPI Awareness**:
  - Native PerMonitorV2 DPI awareness.
  - Spans all connected monitors and adjusts coordinate scaling automatically for 100%, 125%, 150%, 200%, etc.

---

## 🛠️ Architecture

```
SelectAI/
├── Core/
│   ├── Enums/          # SelectionMode, ContentType, AiProviderType
│   ├── Interfaces/     # IScreenCapture, IOcrProvider, IAIProvider, ISearchProvider, ISettingsService
│   ├── Models/         # CapturedScreen, SelectionRegion, OcrResult, DetectedEntity, AiRequest
│   └── Utils/          # GeometryHelper, ImageHelper, NativeMethods
├── Capture/            # GDI+ / Win32 Virtual Desktop multi-monitor capture
├── Selection/          # Chaikin & Catmull-Rom smoothing, polygon clipping
├── Ocr/                # Windows.Media.Ocr.OcrEngine offline provider
├── SmartActions/       # URL, Email, Code, and Search Query detectors
├── AI/                 # GeminiProvider, OpenAiProvider, MockAiProvider, AiProviderFactory
├── Search/             # GoogleSearchProvider, GoogleLensProvider
├── Hotkeys/            # Win32 RegisterHotKey system-wide manager
├── Settings/           # DPAPI-encrypted AppSettings & SettingsService
└── UI/
    ├── Styles/         # ModernStyles.xaml (Windows 11 dark palette & glow effects)
    ├── Overlay/        # OverlayWindow.xaml, SelectionCanvas.cs, FloatingToolbar.xaml
    ├── AI/             # AskAiWindow.xaml (Interactive prompt & response panel)
    ├── Settings/       # SettingsWindow.xaml (Configuration & Hotkey picker)
    └── Tray/           # TrayIconManager.cs (Taskbar notification icon & menu)
```

---

## 🚀 How to Build

### Prerequisites
- Windows 10 (Build 19041+) or Windows 11
- .NET 8.0 SDK or newer

### Build Command
To build the solution:
```powershell
dotnet build SelectAI.sln -c Release
```

### Run Tests
To run the automated test suite (18 unit tests covering smoothing, DPAPI, smart detection, and boundary clamping):
```powershell
dotnet test SelectAI.Tests/SelectAI.Tests.csproj
```

---

## 💻 How to Run

### From Terminal
```powershell
dotnet run --project SelectAI/SelectAI.csproj
```

### From Published Directory
```powershell
dotnet publish SelectAI/SelectAI.csproj -c Release -r win-x64 --self-contained false -o ./publish
./publish/SelectAI.exe
```

When started:
1. The app initializes quietly in your Windows system tray.
2. Press `Ctrl + Shift + Space` anywhere in Windows to trigger the selection overlay.
3. Draw a circle or rectangle around anything on your screen.
4. Use the floating action bar to **Ask AI**, **Search Google**, **Google Lens**, or **Extract Text**!

---

## ⌨️ Default Keyboard Shortcuts

| Shortcut | Action |
|---|---|
| `Ctrl + Shift + Space` | Trigger screen selection mode |
| `Esc` | Cancel / exit selection mode |
| `1` | Switch to **Freeform** circle mode |
| `2` | Switch to **Rectangle** mode |
| `3` | Switch to **Text (OCR)** mode |
| `4` | Switch to **Image** mode |

You can customize the global hotkey in **Settings → Shortcut**.

---

## 🔒 Privacy & Credentials Storage

- **100% Local Processing Guarantee**: Screen capture, drawing, and OCR are processed entirely on your local machine. No data is sent over the network unless you explicitly trigger **Ask AI** or **Google Search**.
- **Secure Secret Storage**: API keys are encrypted with **Windows DPAPI** (`ProtectedData.Protect` using `DataProtectionScope.CurrentUser`) and stored in:
  `%APPDATA%\SelectAI\settings.json`
  Keys are never stored in plaintext or logged.

---

## 🤖 How to Configure AI Providers

1. Right-click the SelectAI tray icon and choose **Settings** (or click **Settings** from the start menu).
2. Navigate to the **AI Provider** tab.
3. Choose your provider:
   - **SelectAI Built-in Assistant**: Ready immediately without any API keys.
   - **Google Gemini**: Select model (`gemini-2.0-flash`, `gemini-1.5-flash`, etc.) and paste your [Google AI Studio API Key](https://aistudio.google.com/).
   - **OpenAI**: Select model (`gpt-4o-mini`, `gpt-4o`) and paste your OpenAI API Key.
4. Click **Save & Apply**.

---

## 📦 How to Package as an Installer

### Option 1: Self-Contained Single File Executable
```powershell
dotnet publish SelectAI/SelectAI.csproj -c Release -r win-x64 -p:PublishSingleFile=true --self-contained false -o ./dist
```

### Option 2: Inno Setup Installer
You can use Inno Setup with the following script:
```iss
[Setup]
AppName=SelectAI
AppVersion=1.0.0
DefaultDirName={autopf}\SelectAI
DefaultGroupName=SelectAI
OutputDir=.\installer
OutputBaseFilename=SelectAI_Setup_v1.0.0
Compression=lzma
SolidCompression=yes

[Files]
Source: "publish\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs

[Icons]
Name: "{group}\SelectAI"; Filename: "{app}\SelectAI.exe"
Name: "{autostartup}\SelectAI"; Filename: "{app}\SelectAI.exe"; Parameters: "--background"
```

---

## 🛡️ User-Mode Windows APIs Used
This application strictly uses documented Windows user-mode APIs with zero kernel drivers:
- `user32.dll!RegisterHotKey` / `UnregisterHotKey` for system-wide shortcuts
- `user32.dll!GetSystemMetrics` (`SM_XVIRTUALSCREEN`, `SM_YVIRTUALSCREEN`, etc.) for multi-monitor geometry
- `gdi32.dll` / GDI+ for high-fidelity hardware-accelerated screen capture
- `Windows.Media.Ocr.OcrEngine` (WinRT) for offline OCR
- WPF DirectX composition pipeline (`CompositionTarget.Rendering`) for 120Hz/144Hz fluid tracing animations
