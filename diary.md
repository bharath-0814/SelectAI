# SelectAI - Project Diary & Milestones

This file serves as a persistent memory and milestone tracker for the SelectAI project. Any AI assistant or developer working on this project should read this file to understand the context, past achievements, current state, and future goals.

## Project Overview
**SelectAI** is a Windows desktop application inspired by Samsung Galaxy AI's "Circle to Search" feature. It allows users to seamlessly select an area on their screen using global hotkeys and instantly analyzes it using Google Lens and Gemini AI, all without requiring manual API key configuration.

---

## 🏆 Milestones Achieved (Past)

1. **Initial Project Setup & WPF Foundation**
   - Created a WPF application with a transparent overlay window for screen selection.
   - Implemented basic drawing logic (rectangles, ellipses, freeform).
   - Set up the project structure (`Core`, `UI`, `Services`).

2. **Global Hotkey Integration**
   - Implemented a low-level global keyboard hook using Win32 API.
   - Configured `Ctrl + Shift + C` (and initially Space) to trigger the selection overlay.

3. **Screen Capture & Image Processing**
   - Integrated logic to capture the exact coordinates of the user's screen selection.
   - Saved the cropped image to a temporary file for processing.

4. **Single-Instance Application & IPC**
   - Solved the "multiple instances running in background" bug.
   - Implemented an OS-level `Mutex` and `EventWaitHandle` (`SelectAI_Wakeup_Event_9B87F1C4`) to ensure only one instance runs.
   - Made the app robustly wake up and restore its UI when launched again, bypassing Windows 11 foreground lock restrictions using `ShowWindow(SW_RESTORE)` and `SetForegroundWindow`.

5. **Google Lens & Gemini Proof-of-Concept**
   - Verified that a multipart POST upload to `https://lens.google.com/v3/upload` returns an HTTP 303 redirect directly to the Lens results page containing Gemini AI overviews.
   - Proved that the app can bypass the need for a Google API key by using the browser.

6. **Automated Build & Deployment Script**
   - Created `sync_dist.ps1` to stop the app, build a self-contained executable, sign it with Authenticode, and package it into a distributable ZIP file.

7. **Self-Installing & Auto-Updating App Experience**
   - Implemented `EnsureSelfInstalled()` in `App.xaml.cs`. When run from any folder outside `%LOCALAPPDATA%\Programs\SelectAI`, it automatically installs the executable, creates Desktop and Start Menu shortcuts, and adds a shortcut to the Windows **Startup folder** so the global hotkey (`Ctrl+Shift+C`) is always on after boot.
   - Handles in-place updates by terminating prior instances and overwriting program files.

8. **Samsung Galaxy AI Visuals & Auto-Snap**
   - Implemented exact Galaxy AI visual style: deep translucent dim layer (`#A0000000`), cyan tracing glow, and instant auto-snapping of freeform gestures into clean rounded rectangles with multi-color gradient bloom borders.
   - Restructured the floating action bar to strictly show the exact 3-pill options: `[ Copy ]`, `[ Share ]`, and `[ Save ]`.

9. **Docked Split-Screen Browser Panel & Zero-API-Key Google Lens / Gemini**
   - Built `SideSearchPanel.xaml` featuring an embedded `WebView2` control and animated slide-in from the right edge.
   - Added smooth desktop left-shift animation (`-120px`) matching the Galaxy AI split-screen gesture.
   - Created `GoogleLensService.cs` performing background multipart upload of the cropped selection to Google Lens, extracting the 303 redirect target URL with Gemini AI results, and seamlessly loading it into the side panel.
   - Added browser switcher support (Chrome, Brave, Firefox, Edge, and "+ Add More" custom browser detection) and prominent "Show results in browser" button.

10. **Interactive Bounding Box Drag Handles, Zero-Friction Re-Drawing & Clean Recognition**
    - Added 4 interactive corner drag handles (TopLeft, TopRight, BottomLeft, BottomRight) allowing real-time bidirectional scaling and fine-tuning of the auto-snapped box.
    - Floating action bar dynamically follows handle dragging in real-time.
    - Zero-friction redrawing: clicking anywhere outside the corner handles immediately resets the box and begins a fresh drawing trace without modal resets.
    - Switched freeform cropping from jagged transparent polygon cutouts to intact rectangular bounding box slices, ensuring complete visual and text context is preserved for Google Lens & Gemini AI recognition.
    - Implemented clean window deactivation: pressing `Win + D`, `Alt + Tab`, or clicking the taskbar smoothly yields focus without locking the user's screen.
    - Re-published application as a clean, single-file bundle containing only `SelectAI.exe` and `README.txt`.

11. **Whole-Box Draggable Panning, Exact Galaxy AI Header with Engine Dropdown & Dark Mode**
    - Whole-box movement: hovering inside the selected area changes the cursor to `SizeAll`, allowing the user to drag and move the entire selection rectangle to any position on the screen.
    - Exact Galaxy AI side panel header: removed messy browser pills row completely. Replaced with an elegant engine switcher button (`[ 🌐 Google ▾ ]` / `[ 🔷 Bing ▾ ]`) with down-arrow popup menu to choose engines (Google, Bing, Brave, Firefox, Edge, Custom).
    - Embedded selection thumbnail: displayed miniature rounded preview of the cropped image inside the search capsule right next to the search query.
    - Fixed GDI+ DPI resolution offset drift in `ImageHelper.CropRect` via direct memory `Bitmap.Clone()`.

12. **Intelligent Hybrid OCR + Visual Pipeline & Native Desktop Engine**
    - Eliminated "no response available" / empty results on text selections by implementing an intelligent hybrid pipeline:
      - Automatically executes `WindowsMediaOcrProvider` (<15ms) on the cropped selection.
      - If text is present (e.g., file names, error messages, code, documentation), navigates to Google Search with the exact query text, immediately rendering Google's **Gemini AI Overview** and full search results.
      - If pure image/illustration (e.g., Pikachu, photo, logo), uploads to Google Lens for visual matching.
    - Fixed WebView2 script execution: removed the Android mobile User-Agent that was breaking Google desktop scripts and causing blank pages. Used standard desktop Chrome User-Agent with dynamic `overflow-x: hidden` injection for clean vertical presentation.
    - Populated the top search capsule with the extracted OCR query text for seamless multisearch and user refinement.

13. **Official v1.1.0 Release & Verification**
    - Bumped version to 1.1.0 across `.csproj`, `version.json`, `index.html`, `README.md`, `MainWindow.xaml`, and helper scripts.
    - Verified compiled binary: FileVersion `1.1.0.0`, ProductVersion `1.1.0`.
    - Verified clean 2-file distribution package (`SelectAI.exe` + `README.txt`) in `SelectAI-v1.1.0-win-x64.zip` (72 MB).
    - Deployed v1.1.0 to user Downloads and `%LOCALAPPDATA%\Programs\SelectAI`.

---

## 🚀 Current Phase & Present Goals

- **Distribution & Quality Assurance**: Verification of all components running smoothly on Windows 11 with PerMonitorV2 DPI awareness.
- **Auto-Sync Distribution**: Release builds packaged into self-contained archives in `dist` and user Downloads directory.

---

## 🔮 Future Roadmap (To Do)
- [ ] Add an auto-update checker (e.g., checking GitHub Releases on startup).
- [ ] Add optional local OCR overlay toggle directly inside the side panel.
- [ ] Add keyboard navigation shortcuts within the side search panel.

---
*Note to AI Assistants: Please append new milestones to this file as they are achieved.*
