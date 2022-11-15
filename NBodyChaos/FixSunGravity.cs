using HarmonyLib;
using UnityEngine;

namespace NBodyChaos;

public static class FixSunGravity
{
    [HarmonyPatch(typeof(GravityVolume), "Awake")]
    [HarmonyPostfix]
    private static void OverrideSunGravityOnSunAwake(GravityVolume __instance)
    {
        if (__instance.transform.parent.name == "Sun_Body")
        {
            NBodyChaos.Console.WriteLine("Found sun!");
            // Re-apply mass/calculations
            __instance._falloffExponent = NBodyChaos.SunSpawnGravityExponent;
            __instance._surfaceAcceleration *= NBodyChaos.SunSpawnGravityMultiplier;
            __instance._gravitationalMass = __instance._surfaceAcceleration * Mathf.Pow(__instance._upperSurfaceRadius, __instance._falloffExponent) / 0.001f;
            if (__instance._setMass)
            {
                __instance._attachedBody.SetMass(__instance._gravitationalMass);
            }
        }
    }
}