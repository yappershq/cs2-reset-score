# Reset Score (CS2 / ModSharp)

Lets players reset their own scoreboard stats with a chat command, plus an admin command to set
another player's stats. The scoreboard updates live for everyone.

ModSharp port of the CounterStrikeSharp plugin
[stefanx111/cs2-SimpleResetScore](https://github.com/stefanx111/cs2-SimpleResetScore) by StefanX.
Independent reimplementation — no code is shipped from that project.

## Commands

Usable in chat (`!rs`) or console.

| Command | Permission | Description |
|---------|-----------|-------------|
| `rs` / `resetscore` | none (or VIP if `rs_only_vip`) | Reset your own kills/deaths/assists/damage/score/MVPs to 0. |
| `setscore <target> <kills> <deaths> <assists> <damage> <mvps> <score>` | `rs_admin_permission` | Set a player's stats (matches by name substring). |

## ConVars

| ConVar | Default | Description |
|--------|---------|-------------|
| `rs_enabled` | `1` | Master switch. |
| `rs_only_vip` | `0` | Restrict `!rs` to players with the VIP permission. |
| `rs_vip_permission` | `@resetscore/vip` | Permission for `!rs` when `rs_only_vip` is `1`. |
| `rs_admin_permission` | `@resetscore/admin` | Permission required for `!setscore`. |
| `rs_keep_mvp` | `0` | Keep MVP stars when resetting (don't zero MVPs). |
| `rs_cooldown` | `0` | Seconds between self-resets per player (`0` = no cooldown). |

Permissions use the ModSharp `AdminManager` model (`IAdmin.HasPermission`), e.g.
`@resetscore/admin`, `@css/cheats`, `admin:slay`. If `AdminManager` isn't loaded, permission-gated
commands are denied.

## What gets reset

| Field | Netvar flushed |
|-------|----------------|
| Kills, Deaths, Assists, Damage (`MatchStats`) | `m_pActionTrackingServices` |
| Score | `m_iScore` |
| MVPs (unless `rs_keep_mvp`) | `m_iMVPs` |

Each value is written through `IPlayerController` / `IControllerActionTrackingService`, then
`NetworkStateChanged` is called on the netvars above so every client's scoreboard re-reads them
(the ModSharp equivalent of CounterStrikeSharp's `Utilities.SetStateChanged`).

## Build & deploy

```bash
dotnet build -c Release
# output: .build/modules/ResetScore/ResetScore.dll
# assets: .build/locales/resetscore.json
```

Deploy `.build` to your server's `sharp/` directory (module → `sharp/modules`,
locale → `sharp/locales`).
