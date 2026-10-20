using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFramework.Animation
{ 
    [RequireComponent(typeof(ProceduralAnimator))]
#if MIRROR
    public class NetworkProceduralAnimator : NetworkBehaviour
#else
    public class NetworkProceduralAnimator : MonoBehaviour
    #endif
    {
        #if MIRROR
        public float updateInterval = 0.1f;

        private ProceduralAnimator targetAnimator;

        [SyncVar]
        private Vector3 currentPosition;
        [SyncVar]
        private Vector3 currentRotation;

        private void Start()
        {
            targetAnimator = GetComponent<ProceduralAnimator>();

            InvokeRepeating(nameof(UpdateValues), 0, 1 / updateInterval);
        }

        private void UpdateValues()
        {
            if (isLocalPlayer)
            {
                CmdUpdateOnServer(targetAnimator.targetPosition, targetAnimator.targetRotation);
            }
            else
            {
                targetAnimator.isActive = false;
                targetAnimator.targetPosition = currentPosition;
                targetAnimator.targetRotation = currentRotation;
            }
        }

        [Command]
        private void CmdUpdateOnServer(Vector3 pos, Vector3 rot)
        {
            currentPosition = pos;
            currentRotation = rot;
        }
#endif
    }
}