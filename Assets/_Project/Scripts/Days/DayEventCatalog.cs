using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Days
{
    [CreateAssetMenu(menuName = "Shooter Shop/Days/Day Event Catalog", fileName = "DayEventCatalog")]
    public sealed class DayEventCatalog : ScriptableObject
    {
        [PropertyRange(0, 1)]
        [InfoBox("Chance that a day gets a random event")]
        [SerializeField] private float _eventChance = 0.6f;

        [MinValue(1)]
        [InfoBox("Days before events start to happen")]
        [SerializeField] private int _firstEventDay = 2;

        [Required, AssetsOnly, ListDrawerSettings(ShowFoldout = false)]
        [SerializeField] private List<DayEventConfig> _events = new();

        public int IndexOf(DayEventConfig dayEvent) => _events.IndexOf(dayEvent);

        public bool TryGet(int index, out DayEventConfig dayEvent)
        {
            dayEvent = index >= 0 && index < _events.Count ? _events[index] : null;
            return dayEvent != null;
        }

        public int PickIndex(int day) =>
            day >= _firstEventDay && _events.Count > 0 && Random.value < _eventChance ? Random.Range(0, _events.Count) : -1;
    }
}
