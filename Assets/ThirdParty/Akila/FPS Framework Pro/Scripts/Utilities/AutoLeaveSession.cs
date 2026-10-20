using Akila.FPSFramework;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    public class AutoLeaveSession : MonoBehaviour
    {
#if MIRROR
        public void LeaveSessionAndLoadMenu(string name)
        {
            if (NetworkServer.activeHost)
                NetworkManager.singleton.StopHost();
            else
                NetworkManager.singleton.StopClient();

            LoadingScreen.LoadScene(name);
        }
#endif
    }
}