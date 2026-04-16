# RoosterAudioSwitcher

RoosterAudioSwitcher is a Windows tray utility for quickly switching playback devices with global hotkeys.

## Features

- Runs in the Windows system tray.
- Enumerates active playback devices.
- Switches device from tray menu selection.
- Three configurable global hotkeys:
   - Switch hotkey -> switches to a selected target device.
   - Return hotkey -> switches to a selected default device.
   - Third hotkey -> switches to a selected third device.
- Configurable startup with Windows.
- Optional switch notifications.
- Custom app icon support wired to:
   - executable icon
   - tray icon
   - settings window icon

## Default Hotkeys

- Switch hotkey: Ctrl+Alt+S
- Return hotkey: Ctrl+Alt+D
- Third hotkey: Ctrl+Alt+F

## Requirements

- Windows 10/11
- .NET 8 SDK for development
- .NET 8 runtime to run published binaries

## Tech Stack

- .NET 8 (WinForms)
- NAudio 2.2.1
- NAudio.Wasapi 2.2.1

## Build and Run

```bash
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

Release executable:

```text
bin/Release/net8.0-windows/RoosterAudioSwitcher.exe
```

## Configuration

Configuration file:

```text
%AppData%\RoosterAudioSwitcher\config.json
```

Important settings keys:

- HotKey (legacy compatibility)
- SwitchToDeviceHotKey
- ReturnToDefaultHotKey
- ThirdDeviceHotKey
- SwitchToDeviceId
- DefaultDeviceId
- ThirdDeviceId
- ShowNotifications
- StartWithWindows

## Project Structure

```text
RoosterAudioSwitcher/
   .github/workflows/
      dotnet-ci.yml
   Forms/
      SettingsForm.cs
      SettingsForm.Designer.cs
   Managers/
      AudioDeviceManager.cs
      ConfigManager.cs
      HotkeyManager.cs
      Logger.cs
      NotificationManager.cs
      TrayIconManager.cs
   Models/
      AudioDevice.cs
      Settings.cs
   Resources/
      Icon.ico
   App.config
   LICENSE
   Program.cs
   README.md
   RoosterAudioSwitcher.csproj
   RoosterAudioSwitcher.sln
```

## GitHub / CI

- License: MIT
- CI workflow: .github/workflows/dotnet-ci.yml
- Branch: main

## Notes

- If a hotkey cannot be registered, it is usually already in use by another app.
- Device switching depends on Windows Core Audio policy behavior and available playback endpoints.
