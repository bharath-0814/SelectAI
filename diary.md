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

---

## 🚀 Current Phase & Present Goals (In Progress)

### 1. Self-Installing & Auto-Updating App Experience
- **Goal:** When a user extracts the ZIP and runs the app, it should automatically install itself into `%LOCALAPPDATA%\Programs\SelectAI`, add shortcuts to the Desktop and Start Menu, and restart from the installed location.
- **Update Mechanism:** If a user downloads a newer version and runs it, the app should detect the existing installation, update the files, and launch the new version.

### 2. Auto-Snap & Split-Screen UI (Samsung Galaxy AI Style)
- **Auto-Snap:** When the user draws a freeform shape around an object, the app automatically calculates the bounding box and converts it into a clean rectangle.
- **Split-Screen Chrome Dock:** Upon selection, the main desktop screen slightly shifts left, and a docked right-panel slides in. This panel contains a `WebView2` control natively rendering the Google Lens / Gemini URL, giving a seamless embedded browser experience.

---

## 🔮 Future Roadmap (To Do)
- [ ] Complete the `App.xaml.cs` logic for the self-installing behavior.
- [ ] Implement the `WebView2` integration in `OverlayWindow.xaml`.
- [ ] Add sliding animations for the split-screen effect.
- [ ] Refine the UI to look modern and visually pleasing (similar to Samsung One UI 8.5).
- [ ] Add an auto-update checker (e.g., checking GitHub Releases on startup).

---
*Note to AI Assistants: Please append new milestones to this file as they are achieved.*
