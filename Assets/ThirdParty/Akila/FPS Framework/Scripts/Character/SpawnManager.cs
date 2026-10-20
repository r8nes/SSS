using System.Collections;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.Serialization;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Managers/Spwan Manager")]
    public class SpawnManager : MonoBehaviour
    {
        [FormerlySerializedAs("spwanableObjects")]
        public List<SpwanableObject> spawnableObjects = new List<SpwanableObject>();

        
        public float spawnRadius = 5;
        public float respawnDelay = 5;

        [Separator]
        public List<SpwanSide> sides;

        public static SpawnManager Instance;

        public bool isActive { get; set; } = true;

        public UnityEvent<GameObject> onPlayerSpwanWithObj { get; set; } = new UnityEvent<GameObject>();
        public UnityEvent<string> onPlayerSpwanWithObjName { get; set; } = new UnityEvent<string>();
        
        private CancellationTokenSource cancellationTokenSource;

        private void Awake()
        {
            cancellationTokenSource = new CancellationTokenSource();

            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        private void OnDestroy()
        {
            cancellationTokenSource?.Cancel();
            cancellationTokenSource?.Dispose();
        }

        private Coroutine spawnRoutine;

        public void SpawnActor(ActorData actorData, string actorObjName, float delay)
        {
            StartCoroutine(SpawnRoutine(actorData, actorObjName, delay));
        }

        private IEnumerator SpawnRoutine(ActorData actorData, string actorObjName, float delay)
        {
            yield return new WaitForSeconds(delay);

            if (this == null)
                yield break;

            SpawnActor(actorData, actorObjName);
        }

        public void SpawnActor(ActorData actorData, string actorObjName)
        {
            GameObject obj = spawnableObjects.Find(x => x.name == actorObjName).obj;

            GameObject newPlayer = SpawnActor(actorData, obj);

            Actor newPlayerActorComponent = newPlayer.GetComponent<Actor>();
            newPlayerActorComponent.isFirstSpawn = false;

            if (newPlayerActorComponent)
            {
                newPlayerActorComponent.actorData.Update(actorData);
            }

            onPlayerSpwanWithObjName?.Invoke(actorObjName);
        }

        public GameObject SpawnActor(ActorData actorData, GameObject actorObj)
        {
            onPlayerSpwanWithObj?.Invoke(actorObj);

            if(!isActive) return null;

            Vector3 actorPosition = GetPlayerPosition(actorData.teamID);
            Quaternion actorRotation = GetPlayerRotation(actorData.teamID);

            GameObject newActorObject = Instantiate(actorObj, actorPosition, actorRotation);

            Actor newPlayerActorComponent = newActorObject.GetComponent<Actor>();
            newPlayerActorComponent.isFirstSpawn = false;

            if (newPlayerActorComponent)
            {
                newPlayerActorComponent.actorData.Update(actorData);
            }

            Vector3 position = GetPlayerPosition(actorData.teamID);
            Quaternion rotation = GetPlayerRotation(actorData.teamID);

            newActorObject.transform.SetPositionAndRotation(position, rotation);

            return newActorObject;
        }

        public Transform GetPlayerSpawnPoint(int sideId)
        {
            int pointIndex = Random.Range(0, sides[sideId].points.Length);

            return sides[sideId].points[pointIndex];
        }

        public Vector3 GetPlayerPosition(int sideId)
        {
            Vector3 addedPosition = Random.insideUnitCircle * spawnRadius;

            addedPosition.z = addedPosition.y;

            addedPosition.y = 0;

            return GetPlayerSpawnPoint(sideId).position + addedPosition;
        }

        public Quaternion GetPlayerRotation(int sideId)
        {
            return GetPlayerSpawnPoint(sideId).rotation;
        }

        private void OnDrawGizmosSelected()
        {
            foreach (SpwanSide point in sides)
            {
                foreach (Transform transform in point.points)
                {
                    Gizmos.color = Color.white;
                    Gizmos.DrawWireSphere(transform.position, spawnRadius * transform.lossyScale.magnitude);
                }
            }
        }

        [ContextMenu("Setup/Network Components")]
        private void SetupNetworkComponents()
        {
            FPSFrameworkCore.InvokeConvertMethod("ConvertSpawnManager", this, new object[] { this });
        }

        [System.Serializable]
        public class SpwanSide
        {
            public Transform[] points;
        }

        [System.Serializable]
        public class SpwanableObject
        {
            public string name;
            public GameObject obj;
        }
    }
}