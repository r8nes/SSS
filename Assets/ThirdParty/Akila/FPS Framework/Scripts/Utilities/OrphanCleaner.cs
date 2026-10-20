using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace Akila.FPSFramework
{
    public class OrphanCleaner : MonoBehaviour
    {
        public Transform targetParent;
        public float cleanDelay = 3;
        [Tooltip("If on, the game object will be destroyed if it's not parented to anything.")]
        public bool destroyObject = true;
        [Tooltip("Actions to be invoked when game object is not a parent to anything.")]
        public UnityEvent onBecomeOrphan;
        [Tooltip("Actions to be invoked after delay when game object is not a parent to anything.")]
        public UnityEvent onBecomeOrphanDelayed;

        public bool invokeRequsted { get; private set; }

        private void Awake()
        {
            onBecomeOrphanDelayed.AddListener(() => Destroy(gameObject));
        }

        private void Update()
        {
            if (invokeRequsted)
                return;

            if (targetParent == null)
            {
                invokeRequsted = true;

                Invoke(nameof(RequestClean), cleanDelay);

                onBecomeOrphan?.Invoke();
            }
        }

        private void RequestClean()
        {
            onBecomeOrphanDelayed?.Invoke();
        }
    }
}