using HarmonyLib;

namespace NBodyChaos;

public static class DisableReturnToSolarSystem
{
    // Setting this to true temporarily will prevent autopilot from auto-returning to solar system and (should) have no side effects
    private static bool _prevSystemFailure;

    [HarmonyPatch(typeof(ShipCockpitController), "FixedUpdate")]
    [HarmonyPrefix]
    private static void JankDisableAutoPilotSunReturnPre(ShipCockpitController __instance)
    {
        _prevSystemFailure = __instance._shipSystemFailure; 
        __instance._shipSystemFailure = true;
    }
    [HarmonyPatch(typeof(ShipCockpitController), "FixedUpdate")]
    [HarmonyPostfix]
    private static void JankDisableAutoPilotSunReturnPost(ShipCockpitController __instance)
    {
        __instance._shipSystemFailure = _prevSystemFailure; 
    }
}