# Predator Control

A lightweight, open-source replacement for PredatorSense on Acer Predator laptops. Built with C# and WinForms, it sits in your system tray and gives you direct control over your laptop's performance, fans, display and keyboard RGB — without the bloat.

---

## Features

-  **Power Modes** — Silent, Balanced, Turbo, Turbo+, Eco (auto-switches with power state)
-  **Fan Control** — Auto, Max, Custom
-  **Display Refresh Rate** — Toggle between 60 Hz and your panel's max Hz
-  **Keyboard RGB** — 8 lighting modes (Static, Breathing, Neon, Wave, Shifting, Zoom, Meteor, Twinkling) with brightness and speed control
-  **Live CPU/GPU temperatures** in the title bar
-  **System tray** — full control without opening the window
-  **Remembers your settings** across reboots via the registry
-  **Runs on startup** automatically

---

## Requirements

- Acer Predator laptop (uses Acer's `AcerGamingFunction` WMI interface)
- Windows 10 or 11
- **.NET 10 Runtime** — download from [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0)
- Must be run as **Administrator**

> **Note:** This app has only been tested on one specific Predator model. It may or may not work on yours. Check the disclaimer at the bottom.

---

## Download

Go to the [Releases](../../releases) page and download the latest `PredatorControlApp.exe`. Right-click → **Run as administrator**.

---

## Building from Source

1. Install [.NET 10 SDK](https://dotnet.microsoft.com/download/dotnet/10.0)
2. Clone this repo
3. Open `PredatorControlApp.slnx` in Visual Studio 2022+, or run:
   ```
   dotnet build
   ```
4. The output is in `PredatorControlApp/bin/Debug/net10.0-windows/`

---

## Replacing PredatorSense — Full Setup Guide

This app communicates directly with Acer's WMI driver (which is part of Windows, not PredatorSense), so you can safely disable PredatorSense and all its background services.

### Step 1 — Disable PredatorSense Services

Open **Services** (`Win + R` → type `services.msc` → Enter) and set the following services to **Disabled**:

| Service Name | What it does |
|---|---|
| `Acer Gaming Service` | PredatorSense background daemon |
| `Acer Quick Access Service` | Hotkey management (Fn keys) |
| `AcerService` | Core Acer system service |
| `Acer Power Button Service` | Hardware button handling |

For each service:
1. Double-click it
2. Set **Startup type** to `Disabled`
3. Click **Stop** if it's running
4. Click **OK**

> **Note:** The WMI driver that actually controls hardware (power modes, fans, RGB) is separate from these services and remains active — that's what this app uses.

### Step 2 — Disable PredatorSense from Startup

1. Press `Ctrl + Shift + Esc` to open Task Manager
2. Click the **Startup apps** tab
3. Find **PredatorSense** and any other Acer apps
4. Right-click → **Disable**

### Step 3 — Remove PredatorSense from Startup Registry (optional, thorough)

1. Press `Win + R`, type `regedit`, press Enter
2. Navigate to:
   ```
   HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
   ```
3. Delete any entries related to `PredatorSense`, `AcerGaming`, or `Acer`
4. Also check:
   ```
   HKEY_LOCAL_MACHINE\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
   ```

### Step 4 — Make Predator Control Run on Startup Instead

This app registers itself on startup automatically the first time it runs. It launches with the `-hidden` flag so it starts directly in the system tray without showing the window.

To verify it's registered:
1. Open `regedit`
2. Navigate to:
   ```
   HKEY_CURRENT_USER\SOFTWARE\Microsoft\Windows\CurrentVersion\Run
   ```
3. You should see a `PredatorControl` entry pointing to the app's `.exe`

If it's not there, just run the app once as Administrator and it will self-register.

### Step 5 — Uninstall PredatorSense (optional)

If you want to fully remove it:
1. Open **Settings** → **Apps** → **Installed apps**
2. Search for **PredatorSense**
3. Click the three dots → **Uninstall**

>  Do this **after** confirming Predator Control works correctly for you. Keep PredatorSense installed as a fallback until you're happy.

---

## How It Works

The app talks directly to the `AcerGamingFunction` WMI class in the `root\WMI` namespace — the same low-level interface PredatorSense uses under the hood. No Acer background services are required.

---

## Disclaimer

>  **This app was built with the assistance of AI tools.**
>
> It has been tested on **one system only** (an Acer Predator (Helios Neo 16) running Windows 11). Compatibility with other Predator models, Windows versions, or hardware configurations is not guaranteed.
>
> **Use at your own risk. Any issues, damage, or unexpected behavior that occurs as a result of using this app are solely your responsibility.** The author provides no warranty, support, or guarantee of any kind.
>
> If something breaks — reflash your BIOS, reinstall PredatorSense, or restore from a backup. That's on you.

---

## License

[MIT](LICENSE) — do whatever you want with it.
