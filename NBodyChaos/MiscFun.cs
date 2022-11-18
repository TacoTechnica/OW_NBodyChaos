using UnityEngine;

namespace NBodyChaos;

public static class MiscFun
{
    public static void FadeOutVolcanicMoon(OWRigidbody moonRb)
    {
        var moltenCore = moonRb.gameObject.FindChild("MoltenCore_VM");
        var sandLevel = moltenCore.GetComponent<SandLevelController>();
        float timeNow = TimeLoop.GetMinutesElapsed();
        float scaleNow = moltenCore.transform.localScale.x;

        float durationToFadeOut = 15 / 60f;

        // Generate a curve that goes from our position to zero
        sandLevel._scaleCurve = AnimationCurve.EaseInOut(timeNow, scaleNow, timeNow + durationToFadeOut, 0);
    }
}