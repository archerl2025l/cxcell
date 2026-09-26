# CxCell

CxCell is a Windows companion for the Codex/ChatGPT desktop app. It displays Codex subscription quota as compact battery indicators in the left rail, directly above the Help (`?`) button.

## V0.1 behavior

- Plus: show a **5-hour** battery and a **weekly** battery.
- The number inside each battery is the remaining percentage.
- Hovering a battery shows its reset time in local time.
- Pro/other plans: quota windows are driven by what Codex actually reports. If a 5-hour window is absent, CxCell does not invent or display one.
- The batteries use the same vertical slot rhythm as the native left-rail icons.
- Positioning is anchored to the Codex/ChatGPT **client area**, so restored and maximized windows use the same visual center line.
- The overlay only appears while Codex/ChatGPT is foreground.
- A small background watcher starts with Windows, detects Codex/ChatGPT, starts the overlay when the host starts, and closes the overlay when the host exits.
- Right-click the CxCell tray icon to refresh quota or exit CxCell for the current Codex session.

## How quota is read

CxCell launches the local Codex app-server over stdio and calls:

`account/rateLimits/read`

The Codex protocol reports rate-limit windows with `usedPercent`, `windowDurationMins`, and `resetsAt`. CxCell classifies the 5-hour and weekly windows from their reported duration and calculates:

`remaining = 100 - usedPercent`

No ChatGPT password, browser cookie, or copied bearer token is stored by CxCell.

## Why this is an overlay

The current public Codex app-server protocol exposes account/rate-limit data, but the desktop left rail is not exposed as a public third-party toolbar-extension surface. CxCell therefore uses a small no-activate WPF overlay anchored to the desktop window instead of modifying signed Codex application resources.

## Requirements

For development from source:

- Windows 10/11
- .NET 8 SDK
- Codex CLI available as `codex` on `PATH`
- Logged in to Codex/ChatGPT in the same Windows user profile

The installed build is self-contained and does not require a separate .NET runtime.

## Recommended install

After cloning the repository and switching to the feature branch, run the installer once:

```powershell
git clone https://github.com/archerl2025l/cxcell.git
cd cxcell
git switch feature/codex-quota-battery-v0.1
powershell -ExecutionPolicy Bypass -File .\scripts\install.ps1
```

The installer:

1. publishes a self-contained Windows x64 build to `%LOCALAPPDATA%\CxCell`;
2. registers the lightweight watcher under the current user's Windows startup;
3. starts the watcher immediately.

After that, **do not use `dotnet run` for normal use**. Open and close Codex/ChatGPT normally; the quota overlay follows its lifecycle automatically.

The watcher itself is installed as a separate lightweight `CxCellWatcher.exe` process and remains resident so it can detect later Codex launches during the same Windows session. The visible `CxCell.exe` overlay process is started and stopped with Codex/ChatGPT.

## Uninstall

```powershell
powershell -ExecutionPolicy Bypass -File .\scripts\uninstall.ps1
```

This removes the startup registration, stops CxCell processes, and deletes `%LOCALAPPDATA%\CxCell`.

## Development run

For UI development without installing:

```powershell
dotnet run --project .\src\CxCell\CxCell.csproj
```

## Development flow

The repository uses a feature-branch workflow:

1. develop on `feature/**`;
2. GitHub Actions runs restore → build → unit tests;
3. CI also publishes a self-contained `win-x64` artifact;
4. open a pull request into `main`;
5. merge only after CI is green and the desktop smoke test is confirmed.

## V0.1 desktop smoke test

1. Run `scripts\install.ps1`.
2. Confirm the watcher is running.
3. Start Codex/ChatGPT normally; confirm the quota batteries appear without manually launching CxCell.
4. Restore/maximize the host window and confirm the batteries remain centered on the native left-rail icon center line.
5. Confirm the bottom battery is one native icon slot above Help and the upper battery is one slot above that.
6. Confirm both 5H and W batteries appear for a Plus account when both windows are reported.
7. Hover each battery and verify the reset time.
8. Compare the displayed remaining percentages with Codex's own usage view.
9. Close Codex/ChatGPT and confirm the overlay process exits.
10. Re-open Codex/ChatGPT and confirm the watcher starts the overlay again.
11. Right-click the CxCell tray icon and choose `退出 CxCell`; confirm it stays suppressed until the current Codex session ends.
