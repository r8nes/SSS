using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Serialization;

namespace Akila.FPSFramework.Experimental
{
    public class ParametricMover : MonoBehaviour
    {
        [Header("Physics")]
        [SerializeField] float gravity = 9.81f;
        [SerializeField] float drag = 0.1f;

        [Header("State")]
        public Vector3 startPosition = Vector3.zero;
        public bool autoMove = true;
        public bool autoReset = true;

        [FormerlySerializedAs("time")]
        public float travelTime;

        public Vector3 velocity { get; set; }
        public bool isActive { get; set; } = true;

        private readonly List<Vector3> pastPositions = new();
        private Vector3 currentPos;

#if UNITY_EDITOR
        bool isDebugActive = true;
        float futureDuration = 2f;
        int futureSteps = 50;
#endif

        public void StartMoving(Vector3 startPosition, Vector3 velocity)
        {
            travelTime = 0;
            this.startPosition = startPosition;
            this.velocity = velocity;
            transform.position = startPosition;
        }

        private void OnEnable()
        {
            pastPositions.Clear();
            travelTime = 0;
            velocity = Vector3.zero;
        }

        private void OnDisable()
        {
            pastPositions.Clear();
        }

        private void Update()
        {
            if (!isActive)
                return;

            currentPos = GetPositionAtTime(travelTime);
            transform.position = currentPos;

#if UNITY_EDITOR
            if (isDebugActive)
            {
                RecordPastPath(currentPos);
                DrawPastPath();
                DrawFuturePath(travelTime, futureDuration);
            }
#endif

            if (autoMove)
                travelTime += Time.deltaTime;
        }

#if UNITY_EDITOR

        void RecordPastPath(Vector3 pos)
        {
            if (pastPositions.Count == 0 ||
                Vector3.Distance(pastPositions[^1], pos) > 0.01f)
            {
                pastPositions.Add(pos);
            }
        }

        void DrawPastPath()
        {
            for (int i = 1; i < pastPositions.Count; i++)
            {
                Debug.DrawLine(pastPositions[i - 1], pastPositions[i], Color.red);
            }
        }

        void DrawFuturePath(float startTime, float duration)
        {
            if (futureSteps <= 0) return;

            float step = duration / futureSteps;
            Vector3 prev = GetPositionAtTime(startTime);

            for (int i = 1; i <= futureSteps; i++)
            {
                float t = startTime + step * i;
                Vector3 next = GetPositionAtTime(t);

                Debug.DrawLine(prev, next, Color.green);
                prev = next;
            }
        }

        [ContextMenu("Toggle Debug")]
        void ToggleDebug()
        {
            isDebugActive = !isDebugActive;
        }

#endif

        public Vector3 GetPositionAtTime(float t)
        {
            float k = Mathf.Max(drag, 0.0001f);
            float exp = Mathf.Exp(-k * t);

            Vector3 gravityVec = Vector3.down * gravity;

            Vector3 term1 = (velocity / k) * (1f - exp);
            Vector3 term2 = (gravityVec / k) * t;
            Vector3 term3 = (gravityVec / (k * k)) * (exp - 1f);

            return startPosition + term1 + term2 + term3;
        }
    }
}