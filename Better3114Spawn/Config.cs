using System.ComponentModel;

namespace Better3114Spawn;

public class Config
{
     [Description("If your idea of a better 3114 spawn is to remove it from the pool, set this to true.")]
     public bool RemoveScp3114Spawning { get; set; } = false;
     
     [Description("The time in minutes before Scp3114 can force reveal itself. Default is 2.5 minutes. Scp3114 will be automatically revealed after 2.5 minutes or this value if higher.")]
     public double Scp3114PreventRevealTime { get; set; } = 2.5;
}