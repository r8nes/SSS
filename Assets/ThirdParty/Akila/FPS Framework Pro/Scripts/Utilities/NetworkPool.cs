
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
#if MIRROR
    [RequireComponent(typeof(NetworkManager))]
#endif
    public class NetworkPool : MonoBehaviour
    {
#if MIRROR
        [Header("Settings")]
        public GameObject prefab;
        public int capacity = 5;

        private int currentCount;
        public Pool<GameObject> pool;

        private void Awake()
        {
            NetworkManager nm = GetComponent<NetworkManager>();

            if (nm.spawnPrefabs.Contains(prefab))
            {
                nm.spawnPrefabs.Remove(prefab);
            }
        }

        private void Start()
        {
            pool = new Pool<GameObject>(CreateNew, capacity);

            NetworkClient.RegisterPrefab(prefab,
                (SpawnMessage msg) => Get(msg.position, msg.rotation),
                (GameObject spawned) => Return(spawned));
        }

        private GameObject CreateNew()
        {
            GameObject next = Instantiate(prefab, transform);
            next.name = $"{prefab.name}_pooled_{currentCount}";
            next.SetActive(false);
            currentCount++;
            return next;
        }

        public GameObject Get(Vector3 position, Quaternion rotation)
        {
            GameObject next = pool.Get();

            next.transform.position = position;
            next.transform.rotation = rotation;
            next.SetActive(true);
            return next;
        }

        public void Return(GameObject spawned)
        {
            spawned.SetActive(false);
            pool.Return(spawned);
        }

        private void OnDestroy()
        {
            NetworkClient.UnregisterPrefab(prefab);
        }
#endif
    }
}