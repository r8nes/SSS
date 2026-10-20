using System.Net.Mail;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Weapons/Attachments/Laser Sight")]
    public class LaserSight : MonoBehaviour
    {
        [Tooltip("Input action to toggle the laser on/off.")]
        public InputAction toggleLaserInputAction = new InputAction("ToggleLaser", InputActionType.Button, "<Keyboard>/l");

        [Tooltip("Layers the laser can hit.")]
        public LayerMask interactableLayers = ~0;

        [Tooltip("Transform representing the source of the laser.")]
        public Transform laserSource;

        [Tooltip("Transform representing the laser dot at the hit point.")]
        public Transform laserDot;

        [Tooltip("Transform to define the maximum range of the laser.")]
        public Transform laserRange;

        [Tooltip("LineRenderer used to draw the laser beam.")]
        public LineRenderer laserLine;

        [Tooltip("Whether the laser is currently active.")]
        public bool isLaserOn = true;

        private Firearm firearm;
        private RaycastHit hitInfo;
        private bool isStopped;

        public bool isActive { get; set; } = true;

        private Attachment attachment; // store it

        private void Awake()
        {
            if (!laserSource)
                Debug.LogError("[LaserSight] Laser Source is not assigned.", this);

            if (!laserLine)
                Debug.LogError("[LaserSight] LineRenderer is not assigned.", this);
            else
                laserLine.useWorldSpace = true;

            toggleLaserInputAction.Enable();
            toggleLaserInputAction.performed += OnToggleLaser;

            firearm = GetComponentInParent<Firearm>();
            attachment = GetComponent<Attachment>();
        }

        private void OnToggleLaser(InputAction.CallbackContext ctx)
        {
            isLaserOn = !isLaserOn;
        }

        public void Stop()
        {
            isStopped = true;
        }

        private void LateUpdate()
        {
            if(!isActive)
            {
                laserDot.gameObject.SetActive(false);
                laserLine.enabled = false;

                return;
            }

            // Disable but do NOT clear state if attachment is inactive
            if (attachment != null && attachment.isActiveCached == false)
            {
                if (laserLine) laserLine.enabled = false;
                if (laserDot) laserDot.gameObject.SetActive(false);
                return;
            }

            if (isStopped)
            {
                if (laserLine) laserLine.enabled = false;
                if (laserDot) laserDot.gameObject.SetActive(false);
                return;
            }

            if (laserLine)
                laserLine.enabled = isLaserOn;

            if (!isLaserOn)
            {
                if (laserDot) laserDot.gameObject.SetActive(false);
                return;
            }

            laserLine.SetPosition(0, laserSource.position);

            if (Physics.Raycast(laserSource.position, laserSource.forward, out hitInfo, Mathf.Infinity, interactableLayers))
            {
                if (hitInfo.collider.TryGetComponent(out Ignore ignore) && ignore.ignoreLaserDetection)
                    DisableLaser();
                else
                    EnableLaser(hitInfo);
            }
            else
            {
                DisableLaser();
            }
        }

        private void EnableLaser(RaycastHit hit)
        {
            laserLine.SetPosition(1, hit.point);

            if (laserDot)
            {
                laserDot.gameObject.SetActive(true);
                laserDot.position = hit.point;
                laserDot.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal);
            }
        }

        private void DisableLaser()
        {
            if (laserDot) laserDot.gameObject.SetActive(false);

            if (laserLine)
            {
                if (laserRange)
                    laserLine.SetPosition(1, laserRange.position);
                else
                    laserLine.SetPosition(1, laserSource.position + laserSource.forward * 50f);
            }
        }

        private void OnDestroy()
        {
            toggleLaserInputAction.performed -= OnToggleLaser;
        }
    }
}
