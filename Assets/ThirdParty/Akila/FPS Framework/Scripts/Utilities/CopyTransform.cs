using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Akila.FPSFramework
{
    [ExecuteAlways]
    [AddComponentMenu("Akila/FPS Framework/Utility/Copy Transform")]
    [DisallowMultipleComponent]
    public class CopyTransform : MonoBehaviour
    {
        public UpdateMode updateMode = UpdateMode.LateUpdate;
        public Transform target;
        public bool executeInEditMode = true;

        [Space]
        public bool position = true;
        public bool rotation = true;
        public bool scale = false;

        public Vector3 positionOffset;
        public Vector3 rotationOffset;

        protected virtual void Update()
        {
            if (updateMode == UpdateMode.Update) ApplyTransform();
        }

        protected virtual void FixedUpdate()
        {
            if (updateMode == UpdateMode.FixedUpdate) ApplyTransform();
        }

        protected virtual void LateUpdate()
        {
            if (updateMode == UpdateMode.LateUpdate) ApplyTransform();
        }

        protected virtual void ApplyTransform()
        {
            if ((!Application.isPlaying && !executeInEditMode) || target == null)
                return;

            if (position)
                transform.position = target.position + positionOffset;

            if (rotation)
                transform.rotation = target.rotation * Quaternion.Euler(rotationOffset);

            if (scale)
                transform.localScale = target.localScale;
        }
    }

    public enum UpdateMode
    {
        Update,
        FixedUpdate,
        LateUpdate
    }
}
