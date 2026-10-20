using UnityEngine;

namespace Akila.FPSFramework
{
    [AddComponentMenu("")]
    public class PooledObjectAutoRelease : MonoBehaviour
    {
        public bool releaseOnEnable = false;
        public float releaseDelay = 5;

        private void OnEnable()
        {
            if (releaseOnEnable)
                StartReturnSequence(releaseDelay);
        }

        public void StartReturnSequence(float time)
        {
            if (IsInvoking(nameof(ReturnToPool)))
                CancelInvoke(nameof(ReturnToPool));

            Invoke(nameof(ReturnToPool), time);
        }

        private void ReturnToPool()
        {
            PoolManager.Release(gameObject);
        }
    }
}