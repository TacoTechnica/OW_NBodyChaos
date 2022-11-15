using System;
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

    public static void AddCollidersToEverything(Predicate<AstroObject> shouldAddDynamicFluidSystem)
    {
        foreach (var astro in Object.FindObjectsOfType<AstroObject>())
        {
            if (astro._gravityVolume != null)
            {
                float radius = astro._gravityVolume._upperSurfaceRadius;
                AddSphereDetectorToBody(astro.gameObject, radius, shouldAddDynamicFluidSystem(astro));
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

        // Make the LAST fluid cylinder extend to the BOTTOM of ash twin
        foreach (Transform child in sandFunnelBody.GetComponentInChildren<CompoundShape>(true).transform)
        {
            if (child.name == "FluidCylinder" && child.localPosition.z < 100)
            {
                var copy = child.localPosition;
                copy.z = 0;
                child.localPosition = copy;
            }
        }

        var pointer = sandFunnelBody.AddComponent<SandFunnelPointer>();
        pointer.AshTwin = ashTwinBody;
        pointer.EmberTwin = emberTwinBody;
        pointer.SandFunnel = sandFunnelBody;
        // Tweak these
        pointer.DistanceScaleFactor = 0.002f;
        pointer.DistanceScaleOffset = 0f;
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
    private static void AddSphereDetectorToBody(GameObject body, float radius, bool useDynamicFluidDetector = false)
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
                fluidDetector._dontApplyForces = false;
            }
            fluidDetector._collider = collider;
            forceApplier._fluidDetector = fluidDetector;
            forceApplier._applyFluids = true;
            fluidDetector._buoyancy.boundingRadius = radius;
            // Make sure our drag curve makes sense, copy an island...
            CopyIslandBuoyancyTo(fluidDetector);
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

        var newShape = dynamicDetectors.AddComponent<SphereShape>();


        var sphereCollider = dynamicDetectors.GetComponent<SphereCollider>();
        var owCollider = dynamicDetectors.GetComponent<OWCollider>();
        owCollider._parentBody = meteor.owRigidbody;
        newShape.radius = sphereCollider.radius;

        dynamicFluidDetector._shape = newShape;
        dynamicFluidDetector._buoyancy.boundingRadius = sphereCollider.radius;

        // Match the 
        CopyIslandBuoyancyTo(dynamicFluidDetector);

        
        var upperForceApplier = constantDetectors.GetComponent<ForceApplier>();
        upperForceApplier._fluidDetector = dynamicFluidDetector;
        upperForceApplier._applyFluids = true;
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
    private static void DoNotDestroyPlanets(ref bool __runOriginal, OWRigidbody bodyToVanish)
    {
        // Meteors immediately get destroyed from hollow's lantern itself
        if (bodyToVanish.GetComponent<AstroObject>() != null || bodyToVanish.GetComponent<MeteorController>() != null)
        {
            __runOriginal = false;
        }
    }

    [HarmonyPatch(typeof(MeteorController), "Awake")]
    [HarmonyPostfix]
    private static void FixMeteorOnInit(MeteorController __instance)
    {
        FixHollowLanternMeteor(__instance);
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

        private void Update()
        {
            if (SandFunnel != null && AshTwin != null && EmberTwin != null)
            {
                SandFunnel.transform.position = AshTwin.transform.position;
                Vector3 delta = EmberTwin.transform.position - AshTwin.transform.position;
                SandFunnel.transform.rotation = Quaternion.LookRotation(delta.normalized, SandFunnel.transform.up);
                var scale = SandFunnel.transform.localScale;
                scale.z = DistanceScaleOffset + delta.magnitude * DistanceScaleFactor;
                SandFunnel.transform.localScale = scale;
            }
        }
    }
}