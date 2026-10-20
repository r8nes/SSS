using UnityEngine;

namespace Akila.FPSFramework
{
    public class ProximityCuller : MonoBehaviour
    {
        public enum CullingMode
        {
            CameraDistance,
            StartPositionDistance
        }

        public CullingMode cullingMode;
        public float minDistance = 1;
        public bool reverse;

        bool isEnabled;
        Renderer[] renderers;
        float distance;
        Vector3 position;

        private void Awake()
        {
            renderers = GetComponentsInChildren<Renderer>();
        }

        private void Start()
        {
            foreach (Renderer renderer in renderers)
                renderer.enabled = !reverse;

            if (cullingMode == CullingMode.StartPositionDistance)
                position = transform.position;
            else
                position = Camera.main.transform.position;

        }

        private void Update()
        {
            distance = Vector3.Distance(position, transform.position);

            isEnabled = reverse ? distance > minDistance : distance < minDistance;

            foreach (Renderer renderer in renderers)
                renderer.enabled = isEnabled;
        }
    }
}