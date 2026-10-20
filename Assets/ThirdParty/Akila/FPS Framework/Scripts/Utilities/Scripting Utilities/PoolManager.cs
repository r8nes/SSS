using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Akila.FPSFramework
{
    /// <summary>
    /// Centralized object pooling manager.
    /// </summary>
    public class PoolManager : MonoBehaviour
    {
        private static PoolManager instance;

        public static PoolManager Instance
        {
            get
            {
                if (instance == null)
                {
                    GameObject go = new("[Pool Manager]");
                    instance = go.AddComponent<PoolManager>();

                    if (instance.dontDestoryOnLoad)
                        DontDestroyOnLoad(go);
                }

                return instance;
            }
        }

        [System.Serializable]
        public class PoolEntry
        {
            public GameObject prefab;
            public int defaultCapacity = 10;
            public int maxSize = 100;
        }

        public bool dontDestoryOnLoad = true;

        private PoolEntry[] pools = { };

        /// <summary>
        /// Maps a prefab to its corresponding object pool.
        /// </summary>
        private readonly Dictionary<GameObject, ObjectPool<GameObject>> objectPools = new();

        /// <summary>
        /// Maps spawned instances back to their original prefab.
        /// </summary>
        private readonly Dictionary<GameObject, GameObject> instanceToPrefab = new();

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;

            foreach (var entry in pools)
            {
                if (entry.prefab == null)
                    continue;

                objectPools[entry.prefab] = CreatePool(
                    entry.prefab,
                    entry.defaultCapacity,
                    entry.maxSize);
            }
        }

        public static GameObject Get(GameObject prefab)
        {
            return Instance.GetInternal(prefab);
        }

        public static GameObject Get(GameObject prefab, float releaseAfter)
        {
            GameObject obj = Get(prefab);

            if (obj != null && obj.activeInHierarchy)
            {
                if (obj.TryGetComponent<PooledObjectAutoRelease>(out PooledObjectAutoRelease autoRelease))
                {
                    autoRelease.StartReturnSequence(releaseAfter);
                }
            }
            return obj;
        }

        public static void Release(GameObject obj)
        {
            Instance.ReleaseInternal(obj);
        }

        private GameObject GetInternal(GameObject prefab)
        {
            if (!objectPools.TryGetValue(prefab, out var pool))
            {
                pool = CreatePool(prefab, 10, 100);
                objectPools.Add(prefab, pool);
            }

            return pool.Get();
        }

        private void ReleaseInternal(GameObject obj)
        {
            if (obj == null)
                return;

            if (!instanceToPrefab.TryGetValue(obj, out var prefab))
            {
                Destroy(obj);
                return;
            }

            if (objectPools.TryGetValue(prefab, out var pool))
                pool.Release(obj);
            else
                Destroy(obj);
        }

        private ObjectPool<GameObject> CreatePool(
            GameObject prefab,
            int defaultCapacity,
            int maxSize)
        {
            return new ObjectPool<GameObject>(
                createFunc: () =>
                {
                    var obj = Instantiate(prefab);

                    obj.SetActive(false);
                    obj.transform.SetParent(transform, false);

                    PooledObjectAutoRelease autoRelease = obj.AddComponent<PooledObjectAutoRelease>();

                    autoRelease.releaseOnEnable = false;
                    autoRelease.releaseDelay = 60;

                    instanceToPrefab[obj] = prefab;

                    return obj;
                },

                actionOnGet: obj =>
                {
                    if (obj != null)
                        obj.SetActive(true);
                },

                actionOnRelease: obj =>
                {
                    obj.transform.SetParent(transform, false);
                    obj.SetActive(false);
                },

                actionOnDestroy: obj =>
                {
                    instanceToPrefab.Remove(obj);

                    if (Application.isEditor)
                        DestroyImmediate(obj);
                    else
                        Destroy(obj);
                },

                collectionCheck: false,
                defaultCapacity: defaultCapacity,
                maxSize: maxSize
            );
        }
    }
}