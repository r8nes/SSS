using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Akila.FPSFramework {
    [RequireComponent(typeof(MinimapObject))]
    public class MinimapObjectFOVScaler : MonoBehaviour
    {
        public Camera mainCamera;
        public MinimapObject targetMinimapObject;
        public Vector2 minSize = new Vector2(15, 30);
        public Vector2 maxSize = new Vector2(160, 40);
        public float scaleFactor = 1;

        private void Start()
        {
            if(targetMinimapObject == null)
                targetMinimapObject = GetComponent<MinimapObject>();

            if (mainCamera == null)
                mainCamera = Camera.main;
        }

        private void Update()
        {
            if(mainCamera == null || targetMinimapObject == null)
                return;

            targetMinimapObject.Size = Vector2.Lerp(minSize * scaleFactor, maxSize * scaleFactor, mainCamera.fieldOfView / 180);
        }
    }
}