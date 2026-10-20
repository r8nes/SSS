using UnityEngine;

namespace Akila.FPSFramework.Animation
{
    public class WallAvoidanceAnimationModifier : ProceduralAnimationModifier
    {
        [Header("Detection")]
        public float range = 1f;
        public LayerMask layerMask = ~0;
        [SerializeField] private int maxHits = 8;

        [Header("Offset When Near Wall")]
        public Vector3 position;
        public Vector3 rotation;

        private Vector3 posVel;
        private Vector3 rotVel;

        private Vector3 pos;
        private Vector3 rot;

        private RaycastHit[] raycastHits;
        private RaycastHit hit;
        private int hitsCount;
        private bool isHit;

        private Camera cam;

        private void Awake()
        {
            raycastHits = new RaycastHit[maxHits];
            cam = Camera.main;
        }

        private void Update()
        {
            if (cam == null)
            {
                cam = Camera.main;
                if (cam == null) return;
            }

            Vector3 origin = cam.transform.position;
            Vector3 direction = cam.transform.forward;

            hitsCount = Physics.RaycastNonAlloc(
                origin,
                direction,
                raycastHits,
                range,
                layerMask,
                QueryTriggerInteraction.Ignore
            );

            isHit = false;
            float closestDistance = float.MaxValue;

            for (int i = 0; i < hitsCount; i++)
            {
                RaycastHit currentHit = raycastHits[i];

                if (currentHit.collider == null)
                    continue;

                if (currentHit.collider.TryGetComponent<Ignore>(out Ignore ignore))
                {
                    if (ignore.ignoreWallAvoidance)
                        continue;
                }

                if (currentHit.distance < closestDistance)
                {
                    closestDistance = currentHit.distance;
                    hit = currentHit;
                    isHit = true;
                }
            }

            if (isHit)
            {
                float t = 1f - Mathf.Clamp01(hit.distance / range);

                targetAnimation.progress = t;
                targetAnimation.IsPlaying = true;

                pos = Vector3.Lerp(Vector3.zero, position, t);
                rot = Vector3.Lerp(Vector3.zero, rotation, t);
            }
            else
            {
                targetAnimation.progress = 0f;
                targetAnimation.IsPlaying = false;

                pos = Vector3.zero;
                rot = Vector3.zero;
            }

            float smoothTime = Mathf.Max(0.01f, targetAnimation.length / Mathf.Max(0.01f, globalSpeed));

            targetPosition = Vector3.SmoothDamp(targetPosition, pos, ref posVel, smoothTime);
            targetRotation = Vector3.SmoothDamp(targetRotation, rot, ref rotVel, smoothTime);
        }
    }
}