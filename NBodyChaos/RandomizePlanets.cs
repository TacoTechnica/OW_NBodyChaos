using OWML.Common;
using UnityEngine;

namespace NBodyChaos;

public static class RandomizePlanets
{
    public static void RandomizePlanetBehavior(float randomizeMassPercent, float randomizeVelocityPercent)
    {
        foreach (var planet in Object.FindObjectsOfType<AstroObject>())
        {
            var body = planet.GetOWRigidbody();
            if (body != null)
            {
                float massFactor = 1f + (randomizeMassPercent * (Random.value * 2f - 1));
                var forceDetector = body.GetComponentInChildren<ForceDetector>();
                if (forceDetector != null)
                {
                    forceDetector._fieldMultiplier *= massFactor;
                }
                else
                {
                    var rb = body.GetComponent<Rigidbody>();
                    if (rb != null)
                    {
                        rb.mass *= massFactor;
                    }
                }
                
                float velocityFactor = 1f + (randomizeVelocityPercent * (Random.value * 2f - 1));
                Vector3 originalVelocity = body.GetVelocity();
                Vector3 resultVelocity = originalVelocity + Random.insideUnitSphere * originalVelocity.magnitude * velocityFactor;
                body.SetVelocity(resultVelocity);

                bool isPlayerPlanet = body.gameObject.name == "TimberHearth_Body"; 
                if (isPlayerPlanet)
                {
                    Vector3 delta = resultVelocity - originalVelocity;
                    // Also move the player and ship
                    var player = Object.FindObjectOfType<PlayerBody>();
                    if (player != null)
                    {
                        player.SetVelocity(player.GetVelocity() + delta);
                    }
                    else
                    {
                        NBodyChaos.Console.WriteLine("No player found! Death is most likely upon ye.", MessageType.Warning);
                    }
                    var ship = Object.FindObjectOfType<ShipBody>();
                    if (ship != null)
                    {
                        ship.SetVelocity(ship.GetVelocity() + delta);
                    }
                    else
                    {
                        NBodyChaos.Console.WriteLine("No ship found!", MessageType.Warning);
                    }
                }

                // Make islands work
                bool isGiantsDeep = body.gameObject.name == "GiantsDeep_Body";
                if (isGiantsDeep)
                {
                    Vector3 delta = resultVelocity - originalVelocity;
                    foreach (var island in Object.FindObjectsOfType<IslandController>())
                    {
                        var islandBody = island.GetAttachedOWRigidbody();
                        islandBody.SetVelocity(islandBody.GetVelocity() + delta);
                    }
                }
            }
        }
    }
}