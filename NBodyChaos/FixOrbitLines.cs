﻿using System;
using HarmonyLib;
using UnityEngine;
using Object = UnityEngine.Object;

namespace NBodyChaos;

public static class FixOrbitLines
{
    [HarmonyPatch(typeof(OrbitLine), "Awake")]
    [HarmonyPostfix]
    private static void OnOrbitLineCreate(OrbitLine __instance)
    {
        // Create a trail renderer instead
        GameObject gameObject = __instance.gameObject;
        var trailRenderer = gameObject.AddComponent<TrailRenderer>();
        var lineRendererToCopy = __instance._lineRenderer;
        trailRenderer.material = lineRendererToCopy.material;
        trailRenderer.sharedMaterial = lineRendererToCopy.sharedMaterial;
        trailRenderer.startColor = lineRendererToCopy.startColor;
        trailRenderer.endColor = lineRendererToCopy.endColor;
        trailRenderer.startWidth = lineRendererToCopy.startWidth;
        trailRenderer.endWidth = lineRendererToCopy.endWidth;
        trailRenderer.time = 50;

        var trailTracker = gameObject.AddComponent<OrbitTrail>();
        trailTracker.TrailRenderer = trailRenderer;
        trailTracker.Color = __instance._color;
        trailTracker.AstroObject = __instance._astroObject;
        trailTracker.LineWidth = __instance._lineWidth;
        trailTracker.MaxLineWidth = __instance._maxLineWidth;

        gameObject.transform.localPosition = Vector3.zero;
        gameObject.transform.localRotation = Quaternion.identity;

        Object.Destroy(__instance);
    }

    private class OrbitTrail : MonoBehaviour
    {
        public TrailRenderer TrailRenderer;
        public AstroObject AstroObject;
        public Color Color;
        public float LineWidth;
        public float MaxLineWidth;

        private Color _startColor, _endColor;
        private static readonly Color Alpha = new Color(0, 0, 0, 0);
        private void Start()
        {
            if (TrailRenderer != null)
            {
                _startColor = TrailRenderer.startColor;
                _endColor = TrailRenderer.endColor;
            }
            GlobalMessenger.AddListener("EnterMapView", OnEnterMapView);
            GlobalMessenger.AddListener("ExitMapView", OnExitMapView);
            enabled = false;
        }
        private void OnDestroy()
        {
            GlobalMessenger.RemoveListener("EnterMapView", OnEnterMapView);
            GlobalMessenger.RemoveListener("ExitMapView", OnExitMapView);
        }

        private void OnEnable()
        {
            if (TrailRenderer != null)
            {
                TrailRenderer.startColor = _startColor;
                TrailRenderer.endColor = _endColor;
            }
        }

        private void OnDisable()
        {
            if (TrailRenderer != null)
            {
                TrailRenderer.startColor = Alpha;
                TrailRenderer.endColor = Alpha;
            }
        }

        private void OnEnterMapView()
        {
            enabled = true;
        }

        private  void OnExitMapView()
        {
            enabled = false;
        }
    }
}