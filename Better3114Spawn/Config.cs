using System.ComponentModel;

namespace Better3114Spawn;

public class Config
{
     [Description("If your idea of a better 3114 spawn is to remove it from the pool, set this to true.")]
     public bool RemoveScp3114Spawning { get; set; } = false;
     
     [Description("The time in seconds before Scp3114 can force reveal itself. Default is 150 seconds. Scp3114 will be automatically revealed after 150 seconds or this value if higher.")]
     public double Scp3114PreventRevealTime { get; set; } = 150;
     
     [Description("Should allow Scp3114 to reveal itself early if it's been damaged by a human? Default is true.")]
     public bool Scp3114AllowEarlyRevealOnDamage { get; set; } = true;
}