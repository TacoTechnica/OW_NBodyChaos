using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using OWML.Common;
using OWML.ModHelper;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBodyChaos;

public static class FixPlanets
{
    private static List<GravityVolume> _globalVolumes = new();
    private static List<Shape> _shapesToKeepAlive = new List<Shape>();
    private static OceanEffectController _gdOceanEffect;
    private static FluidDetector _referenceFluidSplash;

    public static void Init()
    {
        _globalVolumes = Object.FindObjectsOfType<GravityVolume>()
            .Where(volume => volume._isPlanetGravityVolume)
            .Distinct().ToList();
        _shapesToKeepAlive.Clear();
    }

    public static void ApplyGravityToEverything()
    {
        foreach (var detector in Object.FindObjectsOfType<ForceDetector>())
        {
            // Ignore non-physics gravity bodies... Experiment with this though!
            var parentBody = detector.GetComponentInParent<OWRigidbody>();
            if (parentBody == null)
            {
                NBodyChaos.Console.WriteLine($"    X {detector.gameObject.name} has NO OWRigidBody");
                continue;
            }
            ApplyDefaultGravityToForceDetector(detector);
        }
    }

    public static void AddCollidersToEverything(Predicate<AstroObject> shouldAddDynamicFluidSystem, Func<AstroObject, float> getDensityMultiplier, Func<AstroObject, float> getDragMultiplier)
    {
        foreach (var astro in Object.FindObjectsOfType<AstroObject>())
        {
            if (astro._gravityVolume != null)
            {
                float radius = astro._gravityVolume._upperSurfaceRadius;
                AddSphereDetectorToBody(astro.gameObject, radius, shouldAddDynamicFluidSystem(astro), getDensityMultiplier(astro), getDragMultiplier(astro));
            }
        }
    }

    /**
     * Changes mostly reference https://github.com/Outer-Wilds-New-Horizons/new-horizons/blob/main/NewHorizons/Main.cs#L430-L434
     * Thanks JohnCorby for pointing this out!
     */
    public static void FixPlanetLOD() {

        // Jank shape fixer
        if (Object.FindObjectOfType<JankShapeFixer>() == null)
        {
            new GameObject("Jank NBodyChaos Shape Fixer").AddComponent<JankShapeFixer>();
        }

        // Fix Timber Hearth Moon
        FixSatelliteSector("Moon_Body/Sector_THM", "TimberHearth_Body/StreamingGroup_TH");

        // Fix Hollow's Lantern
        FixSatelliteSector("VolcanicMoon_Body/Sector_VM", "BrittleHollow_Body/StreamingGroup_BH");

        //Fix brittle hollow north pole projection platform
        var northPoleSurface = SearchUtilities.Find("BrittleHollow_Body/Sector_BH/Sector_NorthHemisphere/Sector_NorthPole/Sector_NorthPoleSurface").GetComponent<Sector>();
        var remoteViewer = SearchUtilities.Find("BrittleHollow_Body/Sector_BH/Sector_NorthHemisphere/Sector_NorthPole/Sector_NorthPoleSurface/Interactables_NorthPoleSurface/LowBuilding/Prefab_NOM_RemoteViewer").GetComponent<NomaiRemoteCameraPlatform>();
        remoteViewer._visualSector = northPoleSurface;

        // Fix Orbital Probe Cannon
        string giantsDeepStreamingGroup = "GiantsDeep_Body/StreamingGroup_GD";
        FixSatelliteSector("OrbitalProbeCannon_Body/Sector_OrbitalProbeCannon", giantsDeepStreamingGroup, "OrbitalProbeCannon_Body/Sector_OrbitalProbeCannon/SectorTrigger_OrbitalProbeCannon");
        FixSatelliteSector("CannonBarrel_Body/Sector_CannonDebrisMid", giantsDeepStreamingGroup, "CannonBarrel_Body/Sector_CannonDebrisMid/SectorTrigger_CannonDebrisMid");
        FixSatelliteSector("CannonMuzzle_Body/Sector_CannonDebrisTip", giantsDeepStreamingGroup, "CannonMuzzle_Body/Sector_CannonDebrisTip/SectorTrigger_CannonDebrisTip");

        // Fix Hourglass Twins
        FixSatelliteSector("TowerTwin_Body/Sector_TowerTwin", "FocalBody/StreamingGroup_HGT");
        FixSatelliteSector("CaveTwin_Body/Sector_CaveTwin", "FocalBody/StreamingGroup_HGT");

        // Fix islands (in the event they fly away from giant's deep...)
        FixSatelliteSector("ConstructionYardIsland_Body/Sector_ConstructionYard", giantsDeepStreamingGroup, "ConstructionYardIsland_Body/Sector_ConstructionYard/SectorTrigger_ConstructionYard");
        FixSatelliteSector("StatueIsland_Body/Sector_StatueIsland", giantsDeepStreamingGroup, "StatueIsland_Body/Sector_StatueIsland/SectorTrigger_StatueIsland");
        FixSatelliteSector("BrambleIsland_Body/Sector_BrambleIsland", giantsDeepStreamingGroup, "BrambleIsland_Body/Sector_BrambleIsland/SectorTrigger_BrambleIsland");
        FixSatelliteSector("QuantumIsland_Body/Sector_QuantumIsland", giantsDeepStreamingGroup, "QuantumIsland_Body/Sector_QuantumIsland/SectorTrigger_QuantumTower");
        FixSatelliteSector("GabbroIsland_Body/Sector_GabbroIsland", giantsDeepStreamingGroup, "GabbroIsland_Body/Sector_GabbroIsland/SectorTrigger_GabbroIsland");

        FixBrittleHollowFragments("BrittleHollow_Body/Sector_BH", "BrittleHollow_Body/StreamingGroup_BH");

        FixVolcanicMoon("VolcanicMoon_Body/MoltenCore_VM/DestructionVolume");

        // TODO: Volcanic shards?
        //string whiteHoleStreamingGroup = "WhiteHole_Body/StreamingGroup_WH";
    }

    public static void FixAshTwinSand(string ashTwinBodyPath, string emberTwinBodyPath, string sandFunnelBodyPath)
    {
        GameObject ashTwinBody = SearchUtilities.Find(ashTwinBodyPath),
            emberTwinBody = SearchUtilities.Find(emberTwinBodyPath),
            sandFunnelBody = SearchUtilities.Find(sandFunnelBodyPath);

        // Disable the sand funnel's sector proxy, it prevents us from colliding with it
        Object.Destroy(sandFunnelBody.GetComponentInChildren<SectorProxy>(true));
        Object.Destroy(sandFunnelBody.GetComponentInChildren<SectorCollisionGroup>(true));
        sandFunnelBody.GetComponentInChildren<SandfallHazardVolume>(true).enabled = true;
        sandFunnelBody.GetComponentInChildren<SandfallHazardVolume>(true).GetComponent<CapsuleShape>().enabled = true;
        sandFunnelBody.GetComponentInChildren<SimpleFluidVolume>(true).enabled = true;
        sandFunnelBody.GetComponentInChildren<CompoundShape>(true).enabled = true;
        //sandFunnelBody.GetComponentInChildren<CapsuleShape>(true).enabled = true;
        sandFunnelBody.GetComponentInChildren<OWTriggerVolume>(true).enabled = true;
        sandFunnelBody.GetComponentInChildren<SandFunnelTriggerVolume>(true).enabled = true;
        foreach (var renderer in sandFunnelBody.GetComponentsInChildren<Renderer>(true))
        {
            renderer.gameObject.SetActive(true);
            renderer.transform.parent.gameObject.SetActive(true);
            renderer.enabled = true;
        }

        var pointer = sandFunnelBody.AddComponent<SandFunnelPointer>();
        pointer.AshTwin = ashTwinBody;
        pointer.EmberTwin = emberTwinBody;
        pointer.SandFunnel = sandFunnelBody;
        // Tweak these
        pointer.DistanceScaleFactor = 0.00211f;
        pointer.DistanceScaleOffset = 0f;
        pointer.DistancePositionFactor = 0.059f;
    }

    private static void FixVolcanicMoon(string destructionVolumePath)
    {
        // This would be very funny though
        SearchUtilities.Find(destructionVolumePath).GetComponent<DestructionVolume>()._shrinkBodies = false;
    }

    public static void MakeStaticBodyAffectedByGravity(string baseName, string bodyName)
    {
        // Add force detector to the sun...
        var body = GameObject.Find(bodyName);
        var owRB = body.GetComponent<OWRigidbody>();
        ForceApplier existingApplier = null;
        foreach (var forceApplier in body.GetComponentsInChildren<ForceApplier>(true))
        {
            if (forceApplier._attachedBody == owRB)
            {
                existingApplier = forceApplier;
                break;
            }
        }
        if (existingApplier == null)
        {
            var newFieldDetectorObject = new GameObject(baseName + "_Detector");
            newFieldDetectorObject.transform.SetParent(body.transform, false);
            var newForceDetector = newFieldDetectorObject.AddComponent<ConstantForceDetector>();
            var newForceApplier = newFieldDetectorObject.AddComponent<ForceApplier>();

            newForceApplier._attachedBody = owRB;
            newForceApplier._forceDetector = newForceDetector;
            newForceApplier._applyForces = true;
            newForceApplier._applyFluids = true;
        }
        else
        {
            existingApplier.enabled = true;
        }
    }

    public static void DisableStaticBodyGravity(string bodyPath)
    {
        var body = GameObject.Find(bodyPath);
        var existingApplier = body.GetComponentInChildren<ForceApplier>(true);
        if (existingApplier != null)
        {
            existingApplier.enabled = false; 
        }
    }

    public static void AddDynamicSphereDetectorToBody(string bodyPath, float radius,
        bool useDynamicFluidDetection = false)
    {
        AddSphereDetectorToBody(SearchUtilities.Find(bodyPath), radius, useDynamicFluidDetection);
    }
    private static void AddSphereDetectorToBody(GameObject body, float radius, bool useDynamicFluidDetector = false, float densityMultiplier = 1, float dragMultiplier = 1)
    {
        var detectorLayer = LayerMask.NameToLayer("BasicDetector");
        GameObject detectorLayerObject = null;
        foreach (var comp in body.GetComponentsInChildren<Component>(true))
        {
            if (comp.gameObject.layer == detectorLayer)
            {
                detectorLayerObject = comp.gameObject;
                break;
            }
        }

        if (detectorLayerObject == null)
        {
            // No detector object
            detectorLayerObject = new GameObject("DetectorVolume");
            detectorLayerObject.layer = detectorLayer;
            detectorLayerObject.transform.SetParent(body.transform, false);
        }

        ForceApplier forceApplier = body.GetComponentInChildren<ForceApplier>();
        if (forceApplier == null)
        {
            // This shouldn't happen? Eh doesn't hurt I guess?
            forceApplier = detectorLayerObject.AddComponent<ForceApplier>();
            forceApplier._applyForces = true;
            forceApplier._attachedBody = body.GetAttachedOWRigidbody();
        }

        Collider collider = detectorLayerObject.GetComponent<Collider>();
        if (collider == null)
        {
            var newSphere = detectorLayerObject.AddComponent<SphereCollider>();
            newSphere.radius = radius;
            collider = newSphere;
        }

        OWCollider owCollider = detectorLayerObject.GetComponent<OWCollider>();
        if (owCollider == null)
        {
            owCollider = detectorLayerObject.AddComponent<OWCollider>();
            owCollider._active = true;
        }
        owCollider._collider = collider;

        // TODO: Get + Add shape too? Is this needed?
        if (useDynamicFluidDetector)
        {
            DynamicFluidDetector fluidDetector = detectorLayerObject.GetComponent<DynamicFluidDetector>();
            if (fluidDetector == null)
            {
                fluidDetector = detectorLayerObject.AddComponent<DynamicFluidDetector>();
                fluidDetector._buoyancy.boundingRadius = radius;
                // Make sure our drag curve makes sense, copy an island...
                CopyIslandBuoyancyTo(fluidDetector);
                fluidDetector._buoyancy.density *= densityMultiplier;
                fluidDetector._dragFactor *= dragMultiplier;
            }
            fluidDetector._dontApplyForces = false;
            fluidDetector._collider = collider;
            forceApplier._fluidDetector = fluidDetector;
            forceApplier._applyFluids = true;

            if (detectorLayerObject.GetComponent<OceanSplasher>() == null)
            {
                AddSplasher(fluidDetector, radius);
            }

            // Make Volcanic moon lose its volcanic shell for funsies after landing in the water (JANK HARDCODED FIX)
            if (detectorLayerObject.name == "FieldDetector_VM")
            {
                fluidDetector.OnEnterFluidType += type =>
                {
                    if (type == FluidVolume.Type.WATER)
                    {
                        MiscFun.FadeOutVolcanicMoon(detectorLayerObject.GetAttachedOWRigidbody());
                    }
                };
            }
        }
    }

    private static void AddSplasher(DynamicFluidDetector fluidDetector, float radius)
    {
        if (fluidDetector.GetComponent<OceanSplasher>() == null)
        {
            if (_gdOceanEffect == null)
            {
                _gdOceanEffect = SearchUtilities.Find("GiantsDeep_Body/Sector_GD/Sector_GDInterior/Ocean_GD")
                    .GetComponent<OceanEffectController>();
            }
            if (_referenceFluidSplash == null)
            {
                _referenceFluidSplash = SearchUtilities.Find("StatueIsland_Body/Detector_StatueIsland")
                    .GetComponent<FluidDetector>();
            }
            OceanSplasher splasher = fluidDetector.gameObject.AddComponent<OceanSplasher>();
            splasher._ocean = _gdOceanEffect;
            splasher._splashRadius = 2 * radius + 100;
            splasher._splashLength = 10;
            splasher._waveHeight = 20;
            splasher._splashWidth = 20;

            if (fluidDetector._splashEffects == null || fluidDetector._splashEffects.Length == 0)
            {
                fluidDetector._splashEffects = _referenceFluidSplash._splashEffects;
            }

            if (fluidDetector._splashSpawnRoot == null)
            {
                fluidDetector._splashSpawnRoot = fluidDetector.transform;
            }
        }
    }

    private static void CopyBuoyancyTo(DynamicFluidDetector target, DynamicFluidDetector toCopy)
    {
        target._dragFactor = toCopy._dragFactor;
        target._angularDragFactor = toCopy._angularDragFactor;
        var buoyancyToCopy = toCopy._buoyancy;
        target._buoyancy.density = buoyancyToCopy.density;
        target._buoyancy.dragCurve = buoyancyToCopy.dragCurve;
        target._buoyancy.submergeCurve = buoyancyToCopy.submergeCurve;
    }

    private static void CopyIslandBuoyancyTo(DynamicFluidDetector target)
    {
        CopyBuoyancyTo(target, SearchUtilities.Find("GabbroIsland_Body/Detector_GabbroIsland")
            .GetComponent<DynamicFluidDetector>());
    }

    private static void FixBrittleHollowFragments(string bhSectorsPath, string bhStreamingGroupPath)
    {
        // BH's "child" sectors are located as children to this gameobject
        var bhSectors = SearchUtilities.Find(bhSectorsPath);
        var bhStreamingGroup = SearchUtilities.Find(bhStreamingGroupPath).GetComponent<StreamingGroup>();
        foreach (var sectorChild in bhSectors.GetComponentsInChildren<Sector>(true))
        {
            if (sectorChild.GetComponent<DetachableFragment>() != null)
            {
                FixSatelliteSector(sectorChild, bhStreamingGroup);
                foreach (var shape in sectorChild.GetComponentsInChildren<Shape>(true))
                {
                    shape.enabled = true;
                    _shapesToKeepAlive.Add(shape);
                }
            }
        }
    }

    private static void FixHollowLanternMeteor(MeteorController meteor)
    {
        var constantDetectors = meteor.gameObject.FindChild("ConstantDetectors");
        Object.Destroy(constantDetectors.GetComponent<ConstantFluidDetector>());
        var detectorLayer = LayerMask.NameToLayer("BasicDetector");
        var dynamicDetectors = meteor.gameObject.FindChild("DynamicDetector");
        dynamicDetectors.layer = detectorLayer;
        var dynamicFluidDetector = dynamicDetectors.GetComponent<DynamicFluidDetector>();
        dynamicFluidDetector._dontApplyForces = false;
        meteor._primaryFluidDetector = dynamicFluidDetector;

        var shape = dynamicDetectors.GetComponent<SphereShape>();
        if (shape == null)
        {
            shape = dynamicDetectors.AddComponent<SphereShape>();
        }


        var sphereCollider = dynamicDetectors.GetComponent<SphereCollider>();
        var owCollider = dynamicDetectors.GetComponent<OWCollider>();
        owCollider._parentBody = meteor.owRigidbody;
        shape.radius = sphereCollider.radius;

        dynamicFluidDetector._shape = shape;
        dynamicFluidDetector._buoyancy.boundingRadius = sphereCollider.radius;

        var forceApplier = constantDetectors.GetComponent<ForceApplier>();
        forceApplier._fluidDetector = dynamicFluidDetector;
        forceApplier._applyFluids = true;

        // Match the 
        CopyIslandBuoyancyTo(dynamicFluidDetector);

        AddSplasher(dynamicFluidDetector, shape.radius * 3f);
    }

    private static void FixSatelliteSector(string satelliteSectorPath, string parentStreamingGroupPath,
        string colliderToEnable = null)
    {
        FixSatelliteSector(SearchUtilities.Find(satelliteSectorPath).GetComponent<Sector>(), SearchUtilities.Find(parentStreamingGroupPath).GetComponent<StreamingGroup>(), colliderToEnable != null ? SearchUtilities.Find(colliderToEnable).GetComponent<Shape>() : null);
    }
    private static void FixSatelliteSector(Sector satelliteSector, StreamingGroup parentStreamingGroup, Shape colliderToEnable = null)
    {
        if (satelliteSector == null)
        {
            NBodyChaos.Console.WriteLine($"No sector found");
            return;
        }
        foreach (var component in satelliteSector.GetComponentsInChildren<Component>(true))
        {
            if (component is ISectorGroup sectorGroup)
            {
                sectorGroup.SetSector(satelliteSector);
            }

            if (component is SectoredMonoBehaviour behaviour)
            {
                behaviour.SetSector(satelliteSector);
            }
        }
        var satellite_ss_obj = new GameObject("Sector_Streaming");
        satellite_ss_obj.transform.SetParent(satelliteSector.transform, false);
        var satellite_ss = satellite_ss_obj.AddComponent<SectorStreaming>();
        // TEMP TEST might cause performance issues
        satellite_ss._softLoadRadius = 1000000;
        satellite_ss._streamingGroup = parentStreamingGroup;
        satellite_ss.SetSector(satelliteSector);

        if (colliderToEnable != null)
        {
            colliderToEnable.enabled = true;
            _shapesToKeepAlive.Add(colliderToEnable);
        }
    }

    private static void ApplyDefaultGravityToForceDetector(ForceDetector detector)
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
        ApplyDefaultGravityToForceDetector(__instance);
    }

    [HarmonyPatch(typeof(PriorityDetector), "DeactivateBelowPriority")]
    [HarmonyPrefix]
    private static void DisableDeactivation(PriorityDetector __instance, ref bool __runOriginal)
    {
        __runOriginal = false;
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

    [HarmonyPatch(typeof(DetachableFragment), "ChangeFragmentSector")]
    [HarmonyPrefix]
    private static void FragmentDoNotChangeSector(ref bool __runOriginal)
    {
        // Fragments change when exiting from the white hole, we wish to override this.
        __runOriginal = false;
    }

    [HarmonyPatch(typeof(DetachableFragment), "Detach")]
    [HarmonyPostfix]
    private static void SaveBrittleHollowFragmentShape(DetachableFragment __instance)
    {
        // Fragments move around, we need their shapes to be active though
        foreach (var shape in __instance.GetComponentsInChildren<Shape>(true))
        {
            shape.enabled = true;
            _shapesToKeepAlive.Add(shape);
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

    [HarmonyPatch(typeof(MapController), "OnBrokeMapSatellite")]
    [HarmonyPrefix]
    private static void NeverBreakMapSatellite(ref bool __runOriginal)
    {
        // Approaching the satellite breaks it in this mod since it gets tossed around
        __runOriginal = false;
    }

    [HarmonyPatch(typeof(DestructionVolume), "Vanish")]
    [HarmonyPrefix]
    private static void DoNotDestroyPlanets(ref bool __runOriginal, DestructionVolume __instance, OWRigidbody bodyToVanish, RelativeLocationData entryLocation)
    {
        // Fix Meteors immediately getting destroyed from hollow's lantern itself
        if (bodyToVanish.GetComponent<AstroObject>() != null || bodyToVanish.GetComponent<MeteorController>() != null)
        {
            __runOriginal = false;
        }

        // If we or the ship are dark bramble and we hit the SUN, kill the player or the ship.
        if (__instance.GetAttachedOWRigidbody().gameObject.name == "Sun_Body" &&
            bodyToVanish.gameObject.name == "DarkBramble_Body")
        {
            foreach (var outerWarp in Locator._outerFogWarps)
            {
                var trigger = outerWarp.GetComponentInParent<OWTriggerVolume>();
                bool playerInsideDarkBramble =
                    trigger._trackedObjects.Any(detector => detector.name == "PlayerDetector");
                bool shipInsideDarkBramble =
                    trigger._trackedObjects.Any(detector => detector.name == "ShipDetector");
                if (playerInsideDarkBramble)
                {
                    Locator.GetDeathManager().KillPlayer(__instance._deathType);
                }
                else if (shipInsideDarkBramble)
                {
                    // TODO: JANK UGLY CODE, PLS FIX: Use the function copy to just call the normal version of this function
                    bodyToVanish.gameObject.SetActive(value: false);
                    ReferenceFrameTracker component = Locator.GetPlayerBody().GetComponent<ReferenceFrameTracker>();
                    if (component.GetReferenceFrame() != null &&
                        component.GetReferenceFrame().GetOWRigidBody() == bodyToVanish)
                    {
                        component.UntargetReferenceFrame();
                    }

                    MapMarker component2 = bodyToVanish.GetComponent<MapMarker>();
                    if (component2 != null)
                    {
                        component2.DisableMarker();
                    }

                    GlobalMessenger.FireEvent("ShipDestroyed");
                }

            }
        }
    }

    [HarmonyPatch(typeof(MeteorController), "Launch")]
    [HarmonyPostfix]
    private static void FixMeteorOnInit(MeteorController __instance)
    {
        // For initialization reasons and because I am lazy (:
        __instance.StartCoroutine(FixMeteorDelayed(__instance));
    }

    private static IEnumerator FixMeteorDelayed(MeteorController __instance)
    {
        yield return null;
        yield return null;
        FixHollowLanternMeteor(__instance);
    }

    [HarmonyPatch(typeof(ForceDetector), "AccumulateAcceleration")]
    [HarmonyPrefix]
    private static void PreventDoubleCountingForces(ForceDetector __instance)
    {
        __instance._activeVolumes = __instance._activeVolumes.Distinct().ToList();

        // Totally radical assumption: NO INHERITANCE
        __instance._activeInheritedDetector = null;

        if (__instance is DynamicForceDetector dynamicForceDetector)
        {
            if (dynamicForceDetector._activeVolumes.Contains(dynamicForceDetector._inheritedVolume))
            {
                dynamicForceDetector._inheritedVolume = null;
            }
        }
    }
    [HarmonyPatch(typeof(AlignmentForceDetector), "AccumulateAcceleration")]
    [HarmonyPrefix]
    private static void PreventDoubleCountingInheritedAcceleration2(AlignmentForceDetector __instance)
    {
        PreventDoubleCountingForces(__instance);
    }

    [HarmonyPatch(typeof(FluidDetector), "AccumulateFluidAcceleration")]
    [HarmonyPrefix]
    private static void DontApplyWaterfallsToMoons(FluidDetector __instance)
    {
        // The attlerock gets stuck inside timber hearth due to waterfalls pushing it down
        __instance._activeVolumes = __instance._activeVolumes.Where(v =>
        {
            if (v is FluidVolume volume)
            {
                if (volume.gameObject.name.ToLower().Contains("waterfall"))
                {
                    return false;
                }

                return true;
            }

            return true;
        }).ToList();
    }

    [HarmonyPatch(typeof(FluidDetector), "SpawnSplash")]
    [HarmonyPrefix]
    private static void AlignSplash(FluidDetector __instance)
    {
        // TODO: Do the alignment, base it on the parent body's ocean radius or something idk
    }

    private class JankShapeFixer : MonoBehaviour
    {
        private void Update()
        {
            foreach (var shape in _shapesToKeepAlive)
            {
                if (shape != null)
                {
                    // TODO: Figure out where this is disabled and prevent that instead of running this...
                    shape.enabled = true;
                }
            }
        }
    }

    private class SandFunnelPointer : MonoBehaviour
    {
        public GameObject AshTwin;
        public GameObject EmberTwin;
        public GameObject SandFunnel;
        public float DistanceScaleOffset;
        public float DistanceScaleFactor;
        public float DistancePositionOffset;
        public float DistancePositionFactor;

        private OWRigidbody _owrb;

        private Vector3 _prevPosition;
        private Quaternion _prevRotation;

        private void Start()
        {
            _owrb = SandFunnel.GetComponent<OWRigidbody>();
            _prevPosition = SandFunnel.transform.position;
            _prevRotation = SandFunnel.transform.rotation;
            if (_owrb != null)
            {
                _owrb._currentVelocity = Vector3.zero;
                _owrb._currentAngularVelocity = Vector3.zero;
            }
        }

        private void Update()
        {
            if (SandFunnel != null && AshTwin != null && EmberTwin != null)
            {
                SandFunnel.transform.position = AshTwin.transform.position;
                Vector3 delta = EmberTwin.transform.position - AshTwin.transform.position;
                SandFunnel.transform.rotation = Quaternion.LookRotation(delta.normalized, SandFunnel.transform.up);
                SandFunnel.transform.position -= SandFunnel.transform.forward *
                                                 (DistancePositionOffset + delta.magnitude * DistancePositionFactor);
                var scale = SandFunnel.transform.localScale;
                scale.z = DistanceScaleOffset + delta.magnitude * DistanceScaleFactor;
                SandFunnel.transform.localScale = scale;

                if (_owrb != null && Time.deltaTime > 0)
                {
                    _owrb._rigidbody.velocity = (SandFunnel.transform.position - _prevPosition) / Time.deltaTime; 
                    _owrb._currentVelocity = _owrb._rigidbody.velocity;
                    _owrb._rigidbody.angularVelocity = (SandFunnel.transform.rotation * Quaternion.Inverse(_prevRotation)).eulerAngles / Time.deltaTime; 
                    _owrb._currentAngularVelocity = _owrb._rigidbody.angularVelocity;
                }

                _prevPosition = SandFunnel.transform.position;
                _prevRotation = SandFunnel.transform.rotation;
            }
        }
    }
}