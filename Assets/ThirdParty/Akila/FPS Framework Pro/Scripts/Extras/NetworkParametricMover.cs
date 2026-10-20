using Akila.FPSFramework;
using Akila.FPSFramework.Experimental;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro.Experimental
{
    [RequireComponent(typeof(ParametricMover))]
#if MIRROR
    public class NetworkParametricMover : NetworkBehaviour
#else
    public class NetworkParametricMover : MonoBehaviour
    #endif
    {
        #if MIRROR
        [SyncVar]
        public float travelTime = 0;

        [SyncVar]
        public Vector3 startPosition;

        [SyncVar]
        public Vector3 maxVelocity;

        private ParametricMover parametricMover;

        private void Start()
        {
            parametricMover = GetComponent<ParametricMover>();
        }

        private void OnEnable()
        {
            Tick();
        }

        private void Update()
        {
            Tick();
        }

        public void Tick()
        {
            if(parametricMover == null)
                parametricMover = transform.GetOrAddComponent<ParametricMover>();

            parametricMover.isActive = false;

            //Synchronize values only on the server
            if (isServer && parametricMover.autoMove)
            {
                travelTime += Time.deltaTime;
            }

            //Update the ParametricMover with the synchronized values
            parametricMover.travelTime = travelTime;
            parametricMover.startPosition = startPosition;
            parametricMover.velocity = maxVelocity;

            transform.position = parametricMover.GetPositionAtTime(travelTime);
        }
#endif
    }
}