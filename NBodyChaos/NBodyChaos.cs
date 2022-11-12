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

        // Example of accessing game code.
        LoadManager.OnCompleteSceneLoad += (scene, loadScene) =>
        {
            if (loadScene != OWScene.SolarSystem) return;
            ModHelper.Console.WriteLine("Loaded into solar system!", MessageType.Success);
            Invoke(nameof(ApplyNBodiesAllPlanets), 1);
        };
    }
    
    private void ApplyNBodiesAllPlanets()
    {
        ModHelper.Console.WriteLine("Bodies will now apply forces to EVERYTHING...");
        SearchUtilities.ClearCache();

        FixPlanets.FixQuantumMoon();
        FixPlanets.FixPlanetLOD();

        FixPlanets.MakeStaticBodyAffectedByGravity("Sun", "Sun_Body");
        FixPlanets.MakeStaticBodyAffectedByGravity("WhiteholeStation", "WhiteholeStation_Body");
        FixPlanets.MakeStaticBodyAffectedByGravity("WhiteholeStationSuperstructure", "WhiteholeStationSuperstructure_Body");

        FixPlanets.LoadAllVolumes();
        FixPlanets.ApplyGravityToEverything();
        
    }
}