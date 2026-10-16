using LabApi.Features.Stores;
using LabApi.Features.Wrappers;
using PlayerRoles;

namespace Better3114Spawn;

public sealed class Scp3114Data : CustomDataStore<Scp3114Data>
{
    public Scp3114Data(Player owner) : base(owner) { }

    public bool CanRevealEarly { get; set; }
    public int? EarlyRevealLifeId { get; set; }
    public int? RoleLifeId { get; set; }
    public RoleTypeId InitialDisguiseRole { get; set; }
    public uint ReplacingNetId { get; set; }
    public bool PendingReplacement { get; set; }
    
    public void Reset()
    {
        CanRevealEarly = false;
        EarlyRevealLifeId = null;
        RoleLifeId = null;
        InitialDisguiseRole = RoleTypeId.None;
        ReplacingNetId = 0;
        PendingReplacement = false;
    }
}
