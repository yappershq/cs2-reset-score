<div align="center">
  <h1><strong>ResetScore</strong></h1>
  <p>Let CS2 players reset their own scoreboard stats with a chat command — plus an admin command to set anyone's stats. The scoreboard updates live for everyone.</p>
</div>

<p align="center">
  <img src="https://img.shields.io/github/license/yappershq/cs2-reset-score" alt="License">
  <img src="https://img.shields.io/github/stars/yappershq/cs2-reset-score?style=flat&logo=github" alt="Stars">
</p>

---

A small ModSharp plugin for CS2. Players type `!rs` to zero their own kills/deaths/assists/damage/score/MVPs; admins use `!setscore` to set another player's stats. Each change flushes the relevant networked fields so every client's scoreboard refreshes immediately.

This is a ModSharp port of the CounterStrikeSharp plugin [stefanx111/cs2-SimpleResetScore](https://github.com/stefanx111/cs2-SimpleResetScore) by StefanX — an independent reimplementation that ships no original code.

## 🚀 Install

Copy the build output into your ModSharp install (`<sharp>` = your `sharp` directory):

| From | To |
|------|----|
| `.build/modules/ResetScore/` | `<sharp>/modules/ResetScore/` |
| `.build/locales/resetscore.json` | `<sharp>/locales/resetscore.json` |

Restart the server (or change map) to load. The editable config is generated on first run at `<sharp>/configs/resetscore.cfg`.

`LocalizerManager` and `AdminManager` (ship with ModSharp) are optional — both are resolved at load and used when present. Without `AdminManager`, permission-gated commands are denied.

## ⌨️ Commands

Usable in chat (`!`) or console.

| Command | Aliases | Description | Permission |
|---------|---------|-------------|------------|
| `rs` | `resetscore` | Reset your own kills/deaths/assists/damage/score/MVPs to 0. | none, or `rs_vip_permission` if `rs_only_vip` is on |
| `setscore <target> <kills> <deaths> <assists> <damage> <mvps> <score>` | — | Set a player's stats (matches every player whose name contains `<target>`). | `rs_admin_permission` |

Permissions use the ModSharp `AdminManager` model (`IAdmin.HasPermission`), e.g. `@resetscore/admin`, `@css/cheats`.

## ⚙️ Configuration

ConVars (also written to `<sharp>/configs/resetscore.cfg`, generated on first run):

| ConVar | Default | Meaning |
|--------|---------|---------|
| `rs_enabled` | `1` | Master switch for `!rs`. |
| `rs_only_vip` | `0` | Restrict `!rs` to players holding the VIP permission. |
| `rs_vip_permission` | `@resetscore/vip` | Permission for `!rs` when `rs_only_vip` is `1`. |
| `rs_admin_permission` | `@resetscore/admin` | Permission required for `!setscore`. |
| `rs_keep_mvp` | `0` | Keep MVP stars when resetting (don't zero MVPs). |
| `rs_cooldown` | `0` | Seconds between self-resets per player (`0` = no cooldown). |

## 🔧 How it works

Stats are written through `IPlayerController` / `IControllerActionTrackingService`: kills/deaths/assists/damage go to `MatchStats`, score to `m_iScore`, and MVPs to `m_iMVPs` (unless `rs_keep_mvp`). After writing, `NetworkStateChanged` is fired on `m_pActionTrackingServices`, `m_iScore`, and `m_iMVPs` so every client's scoreboard re-reads the new values — the ModSharp equivalent of CounterStrikeSharp's `Utilities.SetStateChanged`. Writes are atomic: if the action-tracking service isn't available yet, nothing is changed, so the scoreboard never ends up half-reset.

## 📦 Build

```bash
dotnet build -c Release
```

Outputs the module to `.build/modules/ResetScore/ResetScore.dll` and the locale to `.build/locales/resetscore.json`.

## 🙏 Credits

Port of [stefanx111/cs2-SimpleResetScore](https://github.com/stefanx111/cs2-SimpleResetScore) by StefanX.

---

<div align="center">
  <p>Made with ❤️ by <a href="https://github.com/yappershq">yappershq</a></p>
  <p>⭐ Star this repo if you find it useful!</p>
</div>
