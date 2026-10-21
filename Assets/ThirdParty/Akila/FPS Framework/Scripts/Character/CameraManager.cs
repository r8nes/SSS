using Akila.FPSFramework.Animation;
using System;
using UnityEngine;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Player/Camera Manager")]
    public class CameraManager : MonoBehaviour
    {
        public Camera mainCamera;
        public Camera overlayCamera;
        public CameraShaker cameraShaker;

        [Separator]
        public float fieldOfViewSmoothTime = 0.15f;

        private float mainTargetFOV;
        private float overlayTargetFOV;

        private float mainCurrentFOV;
        private float overlayCurrentFOV;
        private float mainCurrentFOVVelcoity;
        private float overlayCurrentFOVVelcoity;

        public float defaultMainFOV { get; protected set; }
        public float defaultOverlayFOV { get; protected set; }
        public ProceduralAnimation cameraKickAnimation { get; protected set; }

        private float fovSmoothTimeMultiplier;

        private void Start()
        {
            if (mainCamera == null)
                mainCamera = Camera.main;

            if (cameraShaker == null)
                cameraShaker = transform.SearchFor<CameraShaker>();

            if (mainCamera != null && FPSFrameworkCore.IsFieldOfViewOverridden)
                mainCamera.fieldOfView = FPSFrameworkCore.FieldOfView;

            if (overlayCamera != null && FPSFrameworkCore.IsWeaponFieldOfViewOverridden)
                overlayCamera.fieldOfView = FPSFrameworkCore.WeaponFieldOfView;

            if (mainCamera)
                defaultMainFOV = mainCamera.fieldOfView;

            if (overlayCamera)
                defaultOverlayFOV = overlayCamera.fieldOfView;


            mainCurrentFOV = defaultMainFOV;
            overlayCurrentFOV = defaultOverlayFOV;

            mainTargetFOV = defaultMainFOV;
            overlayTargetFOV = defaultOverlayFOV;

            if (transform.SearchFor<ProceduralAnimator>())
            {
                cameraKickAnimation = transform.SearchFor<ProceduralAnimator>().GetAnimation("Camera Kick");
            }
        }

        private void Update()
        {
            if (FPSFrameworkCore.IsFieldOfViewOverridden)
                defaultMainFOV = FPSFrameworkCore.FieldOfView;

            if (FPSFrameworkCore.IsWeaponFieldOfViewOverridden)
                defaultOverlayFOV = FPSFrameworkCore.WeaponFieldOfView;

            mainCurrentFOV = Mathf.SmoothDamp(mainCurrentFOV, mainTargetFOV, ref mainCurrentFOVVelcoity, fieldOfViewSmoothTime * fovSmoothTimeMultiplier);
            overlayCurrentFOV = Mathf.SmoothDamp(overlayCurrentFOV, overlayTargetFOV, ref overlayCurrentFOVVelcoity, fieldOfViewSmoothTime * fovSmoothTimeMultiplier);

            if (mainCamera)
                mainCamera.fieldOfView = mainCurrentFOV;

            if (overlayCamera)
                overlayCamera.fieldOfView = overlayCurrentFOV;
        }

        public void SetFieldOfView(float mainCamera, float weaponCamera, float fovSmoothTimeMultiplier = 1)
        {
            mainTargetFOV = mainCamera;
            overlayTargetFOV = weaponCamera;

            this.fovSmoothTimeMultiplier = fovSmoothTimeMultiplier;
        }

        public void ResetFieldOfView()
        {
            mainTargetFOV = defaultMainFOV;
            overlayTargetFOV = defaultOverlayFOV;
            fovSmoothTimeMultiplier = 1;
        }

        public void ShakeCameras(float multiplier)
        {
            if (cameraShaker == null)
                return;

            cameraShaker.Shake(multiplier);
        }

        public void ShakeCameras(float multiplier, float roughness, float fadeOutTime)
        {
            if (cameraShaker == null)
                return;

            cameraShaker.Shake(multiplier, roughness, fadeOutTime, cameraShaker.defaultFadeInTime);
        }
    }
}