# CxCell

CxCell is a Windows companion overlay for the Codex desktop app. It shows the current Codex subscription quota as compact battery indicators in the left rail, directly above the Help (`?`) button.

## V0.1 target

- Plus: show a **5-hour** battery and a **weekly** battery.
- The number inside each battery is the remaining percentage.
- Hovering a battery shows its reset time in local time.
- Pro/other plans: quota windows are driven by what Codex actually reports. If a 5-hour window is absent, CxCell does not invent or display one.
- The overlay follows the foreground Codex window and hides when Codex is not foreground.

## How it reads quota

CxCell launches the local Codex app-server over stdio and calls:

`account/rateLimits/read`

The current Codex protocol exposes `primary` / `secondary` rate-limit windows with `usedPercent`, `windowDurationMins`, and `resetsAt`. CxCell classifies the 5-hour and weekly windows from their reported duration and calculates:

`remaining = 100 - usedPercent`

No ChatGPT password, browser cookie, or copied bearer token is stored by CxCell.

## Why this is an overlay

The current public Codex app-server protocol exposes account/rate-limit data, but the desktop left rail is not exposed as a public third-party toolbar-extension surface. V0.1 therefore uses a small no-activate WPF overlay anchored to the Codex window instead of modifying signed Codex application resources.

This keeps the prototype update-safe and lets us validate the GitHub development/CI flow before considering any deeper desktop integration.

## Requirements

- Windows 10/11
- .NET 8 Desktop Runtime
- Codex CLI available as `codex` on `PATH`
- Logged in to Codex/ChatGPT in the same user profile

## Run from source

```powershell
git clone https://github.com/archerl2025l/cxcell.git
cd cxcell
git switch feature/codex-quota-battery-v0.1
dotnet run --project .\src\CxCell\CxCell.csproj
```

Bring the Codex desktop app to the foreground. The battery overlay appears near the bottom of the left sidebar, above Help.

## Development flow

The repository uses a feature-branch workflow:

1. develop on `feature/**`
2. GitHub Actions runs restore → build → unit tests on Windows
3. open a pull request into `main`
4. merge only after CI is green and the desktop smoke test is confirmed

Unit tests currently cover Plus dual-window parsing, Pro-style weekly-only parsing, and multi-bucket `codex` quota selection.

## V0.1 desktop smoke test

1. Confirm `codex --version` works in PowerShell.
2. Start CxCell.
3. Focus the Codex desktop window.
4. Confirm the overlay is directly above the Help icon.
5. Confirm both 5H and W batteries appear for a Plus account when both windows are returned.
6. Hover each battery and verify the reset time.
7. Compare the displayed remaining percentages with Codex's own usage view.
8. Minimize/switch away from Codex and confirm the overlay disappears.
