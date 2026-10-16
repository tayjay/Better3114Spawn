using System;
using System.Collections.Generic;
using System.ComponentModel;
using Logger = LabApi.Features.Console.Logger;


namespace Better3114Spawn;

public class Config
{
     private const double DefaultPreventRevealTime = 150;
     private const double MaximumPreventRevealTime = 86400;
     private double _scp3114PreventRevealTime = DefaultPreventRevealTime;

     [Description("If your idea of a better 3114 spawn is to remove it from the pool, set this to true.")]
     public bool RemoveScp3114Spawning { get; set; } = false;
     
     [Description("The time in seconds before Scp3114 can force reveal itself. Default is 150 seconds. Scp3114 will be automatically revealed after 150 seconds or this value if higher.")]
     public double Scp3114PreventRevealTime
     {
          get => _scp3114PreventRevealTime;
          set
          {
               if (double.IsNaN(value) || double.IsInfinity(value) || value < 0 || value > MaximumPreventRevealTime)
               {
                    _scp3114PreventRevealTime = DefaultPreventRevealTime;
                    Logger.Warn($"Invalid Scp3114PreventRevealTime '{value}'; expected 0 through {MaximumPreventRevealTime} seconds. Using {DefaultPreventRevealTime}.");
                    return;
               }

               _scp3114PreventRevealTime = value;
          }
     }
     
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
          args ??= Array.Empty<object>();
          try
          {
               string translation = Translations.ContainsKey(key) ? Translations[key] : key;
               return string.Format(translation, args);
          }
          catch (Exception e)
          {
               Logger.Error($"Error translating key '{key}': {e.Message}");
               return "INVALID_TRANSLATION: " + key + " " + string.Join(", ", args);
          }
          
     }
}
