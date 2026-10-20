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
            Time.timeScale = 1f;
            FPSFrameworkCore.IsPaused = false;

            if (LoadingScreen.Initialized)
                LoadingScreen.Instance.Enable();

            if (NetworkServer.activeHost)
                NetworkManager.singleton.StopHost();
            else
                NetworkManager.singleton.StopClient();
        }
#endif
    }
}