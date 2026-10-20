using static Akila.FPSFramework.Door;
using Akila.FPSFramework;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
#if MIRROR
    [RequireComponent(typeof(Door)), DisallowMultipleComponent]
    public class NetworkDoor : NetworkBehaviour
    {
        public Door targetDoor { get; private set; }

        [SyncVar]
        public int currentStateIndex;
        [SyncVar]
        public float interactionSide;
        [SyncVar]
        Vector3 directionToInteratingEntity;

        private void Start()
        {
            targetDoor = GetComponent<Door>();

            targetDoor.onInteraction.AddListener(Interact);

            targetDoor.IsActive = false;
        }

        private void Update()
        {
            targetDoor.currentState = (DoorState)currentStateIndex;
            targetDoor.interactionSide = interactionSide;
        }

        private void Interact(Transform t)
        {
            CmdInteract(t.position);
        }

        [Command(requiresAuthority = false)]
        private void CmdInteract(Vector3 interactingEntityPosition)
        {
            directionToInteratingEntity = (interactingEntityPosition - transform.position).normalized;

            interactionSide = -Mathf.Sign(Vector3.Dot(transform.right, directionToInteratingEntity));

            if (targetDoor.useTwoStages)
            {
                if (currentStateIndex == 0)
                    currentStateIndex = 1;
                else if (currentStateIndex == 1)
                    currentStateIndex = 2;
                else if (currentStateIndex == 2)
                    currentStateIndex = 0;
            }
            else
            {
                if (currentStateIndex == 0)
                    currentStateIndex = 2;
                else if (currentStateIndex == 2)
                    currentStateIndex = 0;
            }
        }
    }
#else
    public class NetworkDoor : MonoBehaviour
    {

    }
#endif
}