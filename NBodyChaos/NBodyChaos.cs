using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;

namespace NBodyChaos;

public class NBodyChaos : ModBehaviour
{
    private static NBodyChaos Instance;
    public static IModConsole Console => Instance.ModHelper.Console;

    private static bool InSolarSystem => LoadManager.GetCurrentScene() == OWScene.SolarSystem;
    private static bool _useSunGravity = true;
    public static float SunSpawnGravityExponent { get; private set; } = 2;
    public static float SunSpawnGravityMultiplier { get; private set; } = 1;
    public static float RandomizePlanetMassPercent { get; private set; } = 1;
    public static float RandomizePlanetVelocityPercent { get; private set; } = 1;

    private void Awake()
    {
        // You won't be able to access OWML's mod helper in Awake.
        // So you probably don't want to do anything here.
        // Use Start() instead.
        Instance = this;
    }

    private void Start()
    {
        // Starting here, you'll have access to OWML's mod helper.
        ModHelper.Console.WriteLine($"{nameof(NBodyChaos)} is loaded!", MessageType.Success);

        Harmony.CreateAndPatchAll(typeof(FixPlanets));
        Harmony.CreateAndPatchAll(typeof(FixSunGravity));
        Harmony.CreateAndPatchAll(typeof(FixOrbitLines));
        Harmony.CreateAndPatchAll(typeof(DisableReturnToSolarSystem));

        // Example of accessing game code.
        LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
        {
            if (loadScene != OWScene.SolarSystem) return;
            ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
            Invoke(nameof(ApplyNBodiesAllPlanets), 1);
        };
    }

    public override void Configure(IModConfig config)
    {
        bool useSunGravity = config.GetSettingsValue<bool>("sunGravity");
        if (_useSunGravity != useSunGravity)
        {
            _useSunGravity = useSunGravity;
            if (InSolarSystem)
            {
                if (_useSunGravity)
                {
                    FixPlanets.MakeStaticBodyAffectedByGravity("Sun", "Sun_Body");
                }
                else
                {
                    FixPlanets.DisableStaticBodyGravity("Sun_Body");
                }
            }
        }
        SunSpawnGravityExponent = config.GetSettingsValue<float>("sunSpawnGravityExponent");
        SunSpawnGravityMultiplier = config.GetSettingsValue<float>("sunSpawnGravityMultiplier");
        RandomizePlanetMassPercent = config.GetSettingsValue<float>("randomizePlanetMassPercent");
        RandomizePlanetVelocityPercent = config.GetSettingsValue<float>("randomizePlanetVelocityPercent");
    }
    
    private void ApplyNBodiesAllPlanets()
    {
        ModHelper.Console.WriteLine("Bodies will now apply forces to EVERYTHING...");
        SearchUtilities.ClearCache();

        FixPlanets.Init();

        FixPlanets.FixQuantumMoon();
        FixPlanets.FixPlanetLOD();

        if (_useSunGravity)
        {
            FixPlanets.MakeStaticBodyAffectedByGravity("Sun", "Sun_Body");
        }
        FixPlanets.MakeStaticBodyAffectedByGravity("WhiteholeStation", "WhiteholeStation_Body");
        FixPlanets.MakeStaticBodyAffectedByGravity("WhiteholeStationSuperstructure", "WhiteholeStationSuperstructure_Body");

        FixPlanets.ApplyGravityToEverything();

        FixPlanets.AddDynamicSphereDetectorToBody("OrbitalProbeCannon_Body", 8, true);
        FixPlanets.AddDynamicSphereDetectorToBody("CannonBarrel_Body", 5, true);
        FixPlanets.AddDynamicSphereDetectorToBody("CannonMuzzle_Body", 5, true);
        // This one already has a detector, but we want to add a splasher 
        FixPlanets.AddDynamicSphereDetectorToBody("QuantumIsland_Body", 50, true);

        // Makes moons and other things fall in black holes and... fun stuff I guarantee it
        FixPlanets.AddCollidersToEverything(
            astro => astro._type == AstroObject.Type.Moon || astro._type == AstroObject.Type.Satellite ||
                                                     astro._type == AstroObject.Type.SpaceStation,
            astro => 0.7f,
            astro => 0.9f
            );
        
        // Makes the sand funnel ALWAYS point from ash twin to ember twin.... no matter what...
        FixPlanets.FixAshTwinSand("TowerTwin_Body", "CaveTwin_Body", "SandFunnel_Body");

        // Randomization! for fun!
        RandomizePlanets.RandomizePlanetBehavior(RandomizePlanetMassPercent, RandomizePlanetVelocityPercent);
    }
}