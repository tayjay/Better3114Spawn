using System;
using LabApi.Features.Stores;
using LabApi.Loader.Features.Plugins;

namespace Better3114Spawn;

public class Core : Plugin<Config>
{
    public static Core Instance { get; private set; }
    
    private SpawnHandler _spawnHandler;
    private RoleHandler _roleHandler;
    
    public override void Enable()
    {
        Instance = this;
        _spawnHandler = new SpawnHandler();
        _roleHandler = new RoleHandler();
        
        CustomDataStoreManager.RegisterStore<Scp3114Data>();
        
        _spawnHandler.RegisterEvents();
        _roleHandler.RegisterEvents();
    }

    public override void Disable()
    {
        _spawnHandler.UnregisterEvents();
        _roleHandler.UnregisterEvents();

        CustomDataStoreManager.UnregisterStore<Scp3114Data>();
        
        _spawnHandler = null;
        _roleHandler = null;
        Instance = null;
    }

    public override string Name => "Better3114Spawn";
    public override string Description => "Make Scp3114 spawn with humans like Scp953.";
    public override string Author => "TayTay";
    public override Version Version => typeof(Core).Assembly.GetName().Version;
    public override Version RequiredApiVersion => new(1, 1, 7);
}