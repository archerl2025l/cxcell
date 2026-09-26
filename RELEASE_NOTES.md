# CxCell v0.1.0

First usable release of CxCell, a Windows companion overlay for Codex/ChatGPT subscription quota.

## Highlights

- Displays Plus 5-hour and weekly remaining quota as compact battery indicators.
- Shows the remaining percentage inside each battery.
- Hovering a battery shows the local reset time.
- Handles plans where a 5-hour quota window is absent instead of inventing one.
- Reads quota from the local Codex app-server; no browser cookie or copied access token is stored.
- Aligns with the native left rail in both maximized and restored desktop windows.
- Uses a lightweight background watcher: Codex/ChatGPT starts → CxCell overlay starts; host exits → overlay exits; reopening the host starts it again.
- No CxCell tray icon is shown during normal use.
- Ships as a self-contained Windows x64 executable.

## Validated

V0.1.0 desktop smoke testing was completed on a Plus account:

- live 5-hour and weekly quota values displayed successfully;
- maximized-window alignment verified;
- restored-window alignment verified;
- Help-icon relative positioning verified;
- tray UI removed as intended;
- GitHub Actions restore, build, tests, self-contained publish, and artifact upload verified.

## Platform

Windows 10/11 x64.

Codex CLI must be installed and available on PATH, and the user must be signed in to Codex/ChatGPT.
