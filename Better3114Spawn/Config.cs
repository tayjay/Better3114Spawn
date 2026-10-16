using System.Collections.Generic;
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

     [Description("Translations for hints and messages. {0} will be replaced with remaining disguise time.")]
     public Dictionary<string, string> Translations { get; set; } = new Dictionary<string, string>
     {
          { "scp3114_blend_in_hint", "Blend in for {0} before you can attack." },
          { "scp3114_wait_hint", "You must wait {0} before revealing." },
          { "scp3114_allow_reveal_hint", "You may now reveal yourself." },
          { "scp3114_force_reveal_hint", "You have been revealed!" }
     };

     public string Translate(string key, params object[] args)
     {
          string translation = Translations.ContainsKey(key) ? Translations[key] : key;
          return string.Format(translation, args);
     }
}