using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Profiling;

namespace Akila.FPSFramework
{
    /// <summary>
    /// Manages firearm attachments, such as sights, muzzles, and stocks, and their effects on weapon performance.
    /// </summary>
    [AddComponentMenu("Akila/FPS Framework/Weapons/Attachments/Firearm Attachments Manager")]
    public class FirearmAttachmentsManager : MonoBehaviour
    {
        /// <summary>
        /// Represents data for a specific attachment, including its type and name.
        /// </summary>
        [Serializable]
        public class AttachmentData
        {
            public string type = "Default"; // The type of the attachment (e.g., "Sight", "Muzzle").
            public string name = "Default"; // The specific name of the attachment.

            public AttachmentData(string type)
            {
                this.type = type;
                this.name = null;
            }

            public AttachmentData(string type, string name)
            {
                this.type = type;
                this.name = name;
            }
        }

        /// <summary>
        /// List of active attachment types and their names.
        /// </summary>
        public List<AttachmentData> activeAttachments = new List<AttachmentData>
        {
            new AttachmentData("Sight"),
            new AttachmentData("Muzzle"),
            new AttachmentData("Stock"),
            new AttachmentData("Laser")
        };


        public UnityEvent<string> onActiveAttachmentsChange = new UnityEvent<string>();

        /// <summary>
        /// The actual attachment objects currently equipped.
        /// </summary>
        [HideInInspector]
        public List<Attachment> attachments = new List<Attachment>();

        /// <summary>
        /// The firearm this manager is attached to.
        /// </summary>
        public Firearm targetFirearm { get; protected set; }

        /// <summary>
        /// Manages the camera properties for the firearm.
        /// </summary>
        public CameraManager cameraManager { get; protected set; }

        /// <summary>
        /// Returns the total number of attachments.
        /// </summary>
        public int GetAttachmentsCount() => attachments.Count;

        #region Attachment Modifiers
        /// <summary>
        /// Calculates the cumulative damage modifier from all active attachments.
        /// </summary>
        public float damage { get; protected set; }

        /// <summary>
        /// Calculates the cumulative spread modifier from all active attachments.
        /// </summary>
        public float spread { get; protected set; }

        /// <summary>
        /// Calculates the cumulative fire rate modifier from all active attachments.
        /// </summary>
        public float fireRate { get; protected set; }

        /// <summary>
        /// Calculates the cumulative range modifier from all active attachments.
        /// </summary>
        public float range { get; protected set; }

        /// <summary>
        /// Calculates the cumulative muzzle velocity modifier from all active attachments.
        /// </summary>
        public float muzzleVelocity { get; protected set; }

        /// <summary>
        /// Calculates the cumulative recoil modifier from all active attachments.
        /// </summary>
        public float recoil { get; protected set; }

        /// <summary>
        /// Calculates the cumulative visual recoil modifier from all active attachments.
        /// </summary>
        public float visualRecoil { get; protected set; }

        /// <summary>
        /// Calculates the cumulative aim speed modifier from all active attachments.
        /// </summary>
        public float aimSpeed { get; protected set; }
        #endregion

        /// <summary>
        /// Calculates a cumulative modifier based on the provided function.
        /// </summary>
        private void CalculateModifier()
        {
            damage = 1;
            spread = 1;
            fireRate = 1;
            range = 1;
            muzzleVelocity = 1;
            recoil = 1;
            visualRecoil = 1;
            aimSpeed = 1;

            if (attachments == null || attachments.Count == 0)
            {
                return;
            }

            foreach (var attachment in attachments)
            {
                if (attachment.isActiveCached)
                {
                    damage *= attachment.Damage();
                    spread *= attachment.Spread();
                    fireRate *= attachment.FireRate();
                    range *= attachment.Range();
                    muzzleVelocity *= attachment.MuzzleVelocity();
                    recoil *= attachment.Recoil();
                    visualRecoil *= attachment.VisualRecoil();
                    aimSpeed *= attachment.AimSpeed();
                }
            }
        }

        /// <summary>
        /// Initializes the manager and assigns the target firearm.
        /// </summary>
        protected void Start()
        {
            targetFirearm = GetComponent<Firearm>();

            attachments = GetComponentsInChildren<Attachment>().ToList();
        }

        /// <summary>
        /// Updates the camera's field of view for smooth transitions.
        /// </summary>
        /// <param name="mainCameraFOV">Field of view for the main camera.</param>
        /// <param name="overlayCameraFOV">Field of view for the overlay camera.</param>
        /// <param name="t">Transition factor (0 to 1).</param>
        public void UpdateCameraFOV(float mainCameraFOV, float overlayCameraFOV, float t)
        {
            cameraManager = GetComponentInParent<CameraManager>();

            if (cameraManager != null)
            {
                if (targetFirearm && targetFirearm.isAiming && targetFirearm.aimingAnimation)
                    cameraManager.SetFieldOfView(mainCameraFOV, overlayCameraFOV, targetFirearm.aimingAnimation.length / 0.15f);
                else
                    cameraManager.ResetFieldOfView();
            }
            else
            {
                Debug.LogWarning("FirearmAttachmentsManager: CameraManager not found in parent hierarchy.");
            }
        }

        /// <summary>
        /// Subscribes to the attachment switching event.
        /// </summary>
        private void OnEnable()
        {
            Examples.AttachmentSwitching.OnSwitch += SwitchAttachment;

            ApplyAllAttachments();

            onActiveAttachmentsChange.AddListener(ApplyAll);
        }

        private void Update()
        {
            // code
            CalculateModifier();

            ApplyAllAttachments();
        }

        private void ApplyAll(string placeholderText) => ApplyAllAttachments();

        public void ApplyAllAttachments()
        {
            foreach (Attachment attachment in attachments)
            {
                attachment.RefreshState();
            }
        }

        /// <summary>
        /// Unsubscribes from the attachment switching event.
        /// </summary>
        private void OnDisable()
        {
            Examples.AttachmentSwitching.OnSwitch -= SwitchAttachment;

            onActiveAttachmentsChange.RemoveListener(ApplyAll);

            cameraManager?.ResetFieldOfView();
        }

        private void OnDestroy()
        {
            cameraManager?.ResetFieldOfView();
        }

        public void SetActiveAttachments(List<FirearmAttachmentsManager.AttachmentData> activeAttachments)
        {
            this.activeAttachments = activeAttachments;
        }

        /// <summary>
        /// Switches the attachment for a given type and name.
        /// </summary>
        /// <param name="typeAndName">Format: "AttachmentType/AttachmentName".</param>
        public void SwitchAttachment(string typeAndName)
        {
            Firearm firearm = this.SearchFor<Firearm>();

            if (firearm == null)
            {
                //Don't switch as the FirearmAttachmentsManager is most likely on a Pickable.
                return;
            }

            if (firearm.inventory.currentItem != firearm) return;

            string[] pathParts = typeAndName.Split('/');

            if (pathParts.Length < 2)
            {
                Debug.LogError("FirearmAttachmentsManager: Invalid path format. Use 'AttachmentType/AttachmentName'.");
                return;
            }

            string type = pathParts[0];
            string name = pathParts[1];

            foreach (AttachmentData attachment in activeAttachments)
            {
                if (attachment.type == type)
                {
                    attachment.name = name;

                    onActiveAttachmentsChange?.Invoke(typeAndName);
                    return;
                }
            }

            Debug.LogWarning($"FirearmAttachmentsManager: No attachment of type '{type}' found.");
        }
    }
}
