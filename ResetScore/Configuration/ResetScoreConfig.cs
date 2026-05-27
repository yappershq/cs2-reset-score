using Sharp.Shared.Objects;

namespace ResetScore.Configuration;

internal interface IResetScoreConfig
{
    /// <summary>Master switch.</summary>
    bool Enabled { get; }

    /// <summary>Restrict the self-reset command to players holding <see cref="VipPermission"/>.</summary>
    bool OnlyVip { get; }

    /// <summary>Permission required for the self-reset command when <see cref="OnlyVip"/> is true.</summary>
    string VipPermission { get; }

    /// <summary>Permission required for the admin set-score command.</summary>
    string AdminPermission { get; }

    /// <summary>When true, MVP stars are NOT reset (kept on the scoreboard).</summary>
    bool KeepMvp { get; }

    /// <summary>Cooldown in seconds between self-resets per player. 0 = no cooldown.</summary>
    int Cooldown { get; }
}

internal sealed class ResetScoreConfig : IResetScoreConfig
{
    private readonly IConVar? _cvEnabled;
    private readonly IConVar? _cvOnlyVip;
    private readonly IConVar? _cvVipPermission;
    private readonly IConVar? _cvAdminPermission;
    private readonly IConVar? _cvKeepMvp;
    private readonly IConVar? _cvCooldown;

    public ResetScoreConfig(InterfaceBridge bridge)
    {
        var cv = bridge.ConVarManager;

        _cvEnabled         = cv.CreateConVar("rs_enabled",          true, "Enable ResetScore [0=off, 1=on]");
        _cvOnlyVip         = cv.CreateConVar("rs_only_vip",         false, "Restrict !rs to players with the VIP permission");
        _cvVipPermission   = cv.CreateConVar("rs_vip_permission",   "@resetscore/vip", "Permission required for !rs when rs_only_vip is 1");
        _cvAdminPermission = cv.CreateConVar("rs_admin_permission", "@resetscore/admin", "Permission required for !setscore");
        _cvKeepMvp         = cv.CreateConVar("rs_keep_mvp",         false, "Keep MVP stars when resetting (do not zero MVPs)");
        _cvCooldown        = cv.CreateConVar("rs_cooldown",         0, "Seconds between self-resets per player (0=disabled)");

        // Generate/load editable config at sharp/configs/resetscore.cfg (NukoLevelRank style).
        var logger = bridge.LoggerFactory.CreateLogger("ResetScore.Config");
        IConVar?[] all = [_cvEnabled, _cvOnlyVip, _cvVipPermission, _cvAdminPermission, _cvKeepMvp, _cvCooldown];
        ConVarConfigFile.Sync(bridge.SharpPath, "resetscore.cfg", "ResetScore", logger,
            System.Array.FindAll(all, c => c is not null)!);
    }

    public bool   Enabled         => _cvEnabled?.GetBool()         ?? true;
    public bool   OnlyVip         => _cvOnlyVip?.GetBool()         ?? false;
    public string VipPermission   => _cvVipPermission?.GetString() ?? "@resetscore/vip";
    public string AdminPermission => _cvAdminPermission?.GetString() ?? "@resetscore/admin";
    public bool   KeepMvp         => _cvKeepMvp?.GetBool()         ?? false;
    public int    Cooldown        => _cvCooldown?.GetInt32()       ?? 0;
}
