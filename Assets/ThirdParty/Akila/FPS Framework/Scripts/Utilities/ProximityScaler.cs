using Akila.FPSFramework;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static Akila.FPSFramework.ProximityCuller;

namespace Akila.FPSFramework
{
    public class ProximityScaler : MonoBehaviour
    {
        public CullingMode cullingMode;
        public float multipler = 1;
        public bool includeChildRenderers = true;

        private Renderer[] renderers;
        private TrailRenderer[] trailRenderers;

        private Vector3 position;

        private Camera mainCamera;
        private float distanceFromMainCamera;
        private float scale;

        private void Awake()
        {
            ZeroAll();
        }


        void Update()
        {
            mainCamera = FPSFrameworkUtility.GetMainCamera();

            if (cullingMode == CullingMode.StartPositionDistance)
                position = transform.position;
            else
                position = mainCamera.transform.position;

            if (mainCamera != null)
            {
                distanceFromMainCamera = Vector3.Distance(transform.position, position);
                scale = (distanceFromMainCamera * multipler) * (mainCamera.fieldOfView / 360);
            }

            foreach (Renderer r in renderers)
            {
                r.transform.localScale = Vector3.one * scale;
            }

            foreach (TrailRenderer r in trailRenderers)
            {
                r.widthMultiplier = scale;
            }
        }

        private void OnEnable()
        {
            ZeroAll();
        }

        private void OnDisable()
        {
            ZeroAll();
        }

        public void ZeroAll()
        {
            renderers = GetComponentsInChildren<Renderer>();
            trailRenderers = GetComponentsInChildren<TrailRenderer>();

            transform.localScale = Vector3.zero;

            foreach (Renderer r in renderers)
            {
                r.transform.localScale = Vector3.zero;
            }

            foreach (TrailRenderer r in trailRenderers)
            {
                r.widthMultiplier = 0;
                r.Clear();
            }
        }
    }
}