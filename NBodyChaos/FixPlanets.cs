using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;

namespace NBodyChaos;

public static class FixPlanets
{
    private static List<GravityVolume> _globalVolumes = new();

    public static void LoadAllVolumes()
    {
        _globalVolumes = Object.FindObjectsOfType<GravityVolume>()
            .Where(volume => volume._isPlanetGravityVolume)
            .Distinct().ToList();
    }

    public static void ApplyGravityToEverything()
    {
        foreach (var detector in Object.FindObjectsOfType<ForceDetector>())
        {
            // Ignore non-physics gravity bodies... Experiment with this though!
            var parentBody = detector.GetComponentInParent<OWRigidbody>();
            if (parentBody == null)
            {
                Debug.Log($"    X {detector.gameObject.name} has NO OWRigidBody");
                continue;
            }
            ApplyDefaultGravityToDetector(detector);
        }
    }

    /**
     * Changes mostly reference https://github.com/Outer-Wilds-New-Horizons/new-horizons/blob/main/NewHorizons/Main.cs#L430-L434
     * Thanks JohnCorby for pointing this out!
     */
    public static void FixPlanetLOD() {

        // Fix Timber Hearth Moon
        FixSatelliteSector("Moon_Body/Sector_THM", "TimberHearth_Body/StreamingGroup_TH");

        // Fix Hollow's Lantern
        FixSatelliteSector("VolcanicMoon_Body/Sector_VM", "BrittleHollow_Body/StreamingGroup_BH");

        //Fix brittle hollow north pole projection platform
        var northPoleSurface = SearchUtilities.Find("BrittleHollow_Body/Sector_BH/Sector_NorthHemisphere/Sector_NorthPole/Sector_NorthPoleSurface").GetComponent<Sector>();
        var remoteViewer = SearchUtilities.Find("BrittleHollow_Body/Sector_BH/Sector_NorthHemisphere/Sector_NorthPole/Sector_NorthPoleSurface/Interactables_NorthPoleSurface/LowBuilding/Prefab_NOM_RemoteViewer").GetComponent<NomaiRemoteCameraPlatform>();
        remoteViewer._visualSector = northPoleSurface;

        // Fix Orbital Probe Cannon
        FixSatelliteSector("OrbitalProbeCannon_Body/Sector_OrbitalProbeCannon", "GiantsDeep_Body/StreamingGroup_GD");
        SearchUtilities.Find("OrbitalProbeCannon_Body/Sector_OrbitalProbeCannon/SectorTrigger_OrbitalProbeCannon")
            .GetComponent<SphereShape>().enabled = true;

        // Fix Hourglass Twins
        FixSatelliteSector("TowerTwin_Body/Sector_TowerTwin", "FocalBody/StreamingGroup_HGT");
        FixSatelliteSector("CaveTwin_Body/Sector_CaveTwin", "FocalBody/StreamingGroup_HGT");
    }

    public static void MakeStaticBodyAffectedByGravity(string baseName, string bodyName)
    {
        // Add force detector to the sun...
        var body = GameObject.Find(bodyName);
        var newFieldDetectorObject = new GameObject(baseName);
        newFieldDetectorObject.transform.SetParent(body.transform, false);
        var newForceDetector = newFieldDetectorObject.AddComponent<ConstantForceDetector>();
        var newForceApplier = newFieldDetectorObject.AddComponent<ForceApplier>();

        newForceApplier._attachedBody = body.GetComponent<OWRigidbody>();
        newForceApplier._forceDetector = newForceDetector;
        newForceApplier._applyForces = true;
        newForceApplier._applyFluids = true;
    }

    private static void FixSatelliteSector(string satelliteSectorPath, string parentStreamingGroupPath)
    {
        var satellite_sector = SearchUtilities.Find(satelliteSectorPath).GetComponent<Sector>();
        foreach (var component in satellite_sector.GetComponentsInChildren<Component>(true))
        {
            if (component is ISectorGroup sectorGroup)
            {
                sectorGroup.SetSector(satellite_sector);
            }

            if (component is SectoredMonoBehaviour behaviour)
            {
                behaviour.SetSector(satellite_sector);
            }
        }
        var satellite_ss_obj = new GameObject("Sector_Streaming");
        satellite_ss_obj.transform.SetParent(satellite_sector.transform, false);
        var satellite_ss = satellite_ss_obj.AddComponent<SectorStreaming>();
        // TEMP TEST might cause performance issues
        satellite_ss._softLoadRadius = 1000000;
        satellite_ss._streamingGroup = SearchUtilities.Find(parentStreamingGroupPath).GetComponent<StreamingGroup>();
        satellite_ss.SetSector(satellite_sector);
    }

    private static void ApplyDefaultGravityToDetector(ForceDetector detector)
    {
        var parentBody = detector.GetComponentInParent<OWRigidbody>();

        // Make this body not attached to anything
        detector._activeInheritedDetector = null;
        //parentBody._attachedFluidDetector = null;
        //parentBody._attachedRFVolume = null;
        // These do not affect physics, and are used as references elsewhere. Don't set these to null!
        //parentBody._attachedForceDetector = null;
        //parentBody._attachedGravityVolume = null;

        // Make this body affected by all other volumes
        foreach (var gravityVolume in _globalVolumes)
        {
            if (gravityVolume == null)
            {
                NBodyChaos.Console.WriteLine("Wat 2");
                continue;
            }
            var gravityVolumeParentBody = gravityVolume.GetComponentInParent<OWRigidbody>();
            // Do not apply gravity to ourselves, that would be bad...
            if (gravityVolumeParentBody == parentBody)
            {
                if (gravityVolumeParentBody == null)
                {
                    NBodyChaos.Console.WriteLine($"    Y {gravityVolume.gameObject.name} has NO OWRigidBody", MessageType.Warning);
                }

                continue;
            }

            //ModHelper.Console.WriteLine($"{detector.gameObject.name} -> {gravityVolume.gameObject.name}");
            if (detector is ConstantForceDetector constantForceDetector)
            {
                constantForceDetector.AddConstantVolume(gravityVolume, false);
            }
            else
            {
                detector.AddVolume(gravityVolume);
            }
        }
    }

    public static void FixQuantumMoon()
    {
        // Make a dummy force detector for the quantum moon so it doesn't get affected
        var quantumMoon = Object.FindObjectOfType<QuantumMoon>();
        // ReSharper disable once Unity.IncorrectMonoBehaviourInstantiation
        quantumMoon._constantForceDetector = new ConstantForceDetector
        {
            _activeVolumes = new List<EffectVolume>()
        };
    }

    [HarmonyPatch(typeof(ConstantForceDetector), "ClearAllFields")]
    [HarmonyPostfix]
    private static void DefaultToEveryField(ConstantForceDetector __instance)
    {
        if (__instance == null)
        {
            NBodyChaos.Console.WriteLine("Invalid when clearing?", MessageType.Warning);
            return;
        }
        ApplyDefaultGravityToDetector(__instance);
    }

    [HarmonyPatch(typeof(PriorityDetector), "DeactivateBelowPriority")]
    [HarmonyPrefix]
    private static void DisableDeactivation(PriorityDetector __instance, ref bool __runOriginal)
    {
        if (__instance is ForceDetector)
        {
            __runOriginal = false;
        }
    }
    // Unsure if we need this one...
    [HarmonyPatch(typeof(PriorityDetector), "DeactivateLayer")]
    [HarmonyPrefix]
    private static void DisableDeactivationLayer(PriorityDetector __instance, ref bool __runOriginal)
    {
        if (__instance is ForceDetector)
        {
            __runOriginal = false;
        }
    }

    [HarmonyPatch(typeof(PriorityDetector), "RemoveVolume")]
    [HarmonyPrefix]
    private static void DoNotRemoveGravityVolume(EffectVolume eVol, ref bool __runOriginal)
    {
        if (_globalVolumes.Contains(eVol))
        {
            __runOriginal = false;
        }
    }

    [HarmonyPatch(typeof(DebrisLeash), "FixedUpdate")]
    [HarmonyPrefix]
    private static void DisableDebrisLeash(ref bool __runOriginal)
    {
        // Make brittle hollow pieces free to fly around
        __runOriginal = false;
    }

    [HarmonyPatch(typeof(MeteorController), "Launch")]
    [HarmonyPostfix]
    private static void MakeMeteorsIndependent(MeteorController __instance)
    {
        // Make meteors independent from brittle hollow
        __instance._constantForceDetector._activeInheritedDetector = null;
    }

    [HarmonyPatch(typeof(FragmentIntegrity), "Awake")]
    [HarmonyPostfix]
    private static void MakeFragmentsWeaker(FragmentIntegrity __instance)
    {
        // Make fragments weaker, since there will be less meteorites hitting brittle hollow.
        __instance._damageMultiplier *= 100;
    }
}