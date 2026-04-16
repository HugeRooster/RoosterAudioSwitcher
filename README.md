# RoosterAudioSwitcher

RoosterAudioSwitcher is a Windows tray utility that switches playback output devices via global hotkeys.

## Current Capabilities

- Runs in the system tray
- Enumerates active playback devices
- Switches device from tray menu
- Two configurable global hotkeys:
   - Switch to a selected target device
   - Return to a selected default device
- Saves settings to `%AppData%\RoosterAudioSwitcher\config.json`
- Optional notifications and startup with Windows

## Tech Stack

- .NET 8 (WinForms)
- NAudio 2.2.1
- NAudio.Wasapi 2.2.1

## Project Index

```text
RoosterAudioSwitcher/
   Program.cs
   RoosterAudioSwitcher.csproj
   RoosterAudioSwitcher.sln
   App.config
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
```

## Build and Run

```bash
dotnet restore
dotnet build -c Release
dotnet run -c Release
```

Executable output:

```text
bin/Release/net8.0-windows/RoosterAudioSwitcher.exe
```

## Configuration

Config file location:

```text
%AppData%\RoosterAudioSwitcher\config.json
```

Important settings keys:

- `SwitchToDeviceHotKey`
- `ReturnToDefaultHotKey`
- `SwitchToDeviceId`
- `DefaultDeviceId`
- `ShowNotifications`
- `StartWithWindows`

## GitHub Readiness

Included for source control:

- Source code under `Forms/`, `Managers/`, `Models/`
- Solution/project files (`.sln`, `.csproj`)
- `README.md`
- `.gitignore`

Excluded from source control via `.gitignore`:

- Build artifacts (`bin/`, `obj/`)
- IDE/user files (`.vs/`, `.vscode/`, `*.user`, `*.suo`)
- Temporary files and logs

## Notes

- This repository currently does not include a `LICENSE` file. Add one before publishing if desired.
- If you want release packaging next, we can add a `dotnet publish` profile and GitHub Actions workflow.
3. Review the config file to ensure it's valid JSON

---

**Made with ❤️ for audio enthusiasts and Windows power users.**
