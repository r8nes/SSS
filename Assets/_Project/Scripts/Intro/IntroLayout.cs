using System;
using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Intro
{
    [Serializable]
    public sealed class IntroDoor
    {
        [Required]
        [SerializeField] private Transform _pivot;

        [InfoBox("Local rotation added to the closed pose when the door opens")]
        [SerializeField] private Vector3 _openRotation = new(0f, -90f, 0f);

        public Transform Pivot => _pivot;
        public Vector3 OpenRotation => _openRotation;
    }

    public sealed class IntroLayout : MonoBehaviour
    {
        [BoxGroup("Lights"), SceneObjectsOnly, ListDrawerSettings(ShowFoldout = false, NumberOfItemsPerPage = 20)]
        [SerializeField] private List<Light> _lights = new();

        [BoxGroup("Doors"), ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<IntroDoor> _doors = new();

        [BoxGroup("Boss"), Required, SceneObjectsOnly]
        [SerializeField] private ShopBoss _boss;

        [BoxGroup("Boss"), Required, SceneObjectsOnly]
        [InfoBox("Where the boss waits for the player on the days he visits")]
        [SerializeField] private Transform _bossSpawn;

        [BoxGroup("Boss"), Required, SceneObjectsOnly]
        [SerializeField] private Transform _bossExit;

        public IReadOnlyList<Light> Lights => _lights;
        public IReadOnlyList<IntroDoor> Doors => _doors;
        public ShopBoss Boss => _boss;
        public Transform BossExit => _bossExit;
        public Transform BossSpawn => _bossSpawn;

#if UNITY_EDITOR
        public void EditorSetLights(IEnumerable<Light> lights)
        {
            _lights = new List<Light>(lights);
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif
    }
}
