using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Weapons/Attachments/Muzzle")]
    public class AttachmentMuzzle : FirearmAttachment
    {
        public Audio fireAudio = new Audio();
        public ParticleSystem[] muzzleEffects;


        private void Start()
        {
            fireAudio.Setup(gameObject);
        }

        private void Update()
        {
            if (firearm && attachment.isActiveCached)
                firearm.currentFireAudio = fireAudio;
        }

        private void OnDisable()
        {
            if (firearm)
                firearm.currentFireAudio = firearm.fireAudio;
        }
    }
}