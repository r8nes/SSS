using System;
using System.Collections.Generic;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEngine;

namespace ShooterShop.Days
{
    [Serializable]
    public sealed class ScheduledDayEvent
    {
        [MinValue(1)]
        [SerializeField] private int _day = 2;

        [Required, AssetsOnly]
        [SerializeField] private DayEventConfig _event;

        public int Day => _day;
        public DayEventConfig Event => _event;
    }

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

        [ListDrawerSettings(ShowFoldout = false)]
        [InfoBox("Events that always happen on a given day instead of a random one")]
        [SerializeField] private List<ScheduledDayEvent> _scheduled = new();

        public int IndexOf(DayEventConfig dayEvent) => _events.IndexOf(dayEvent);

        public bool TryGet(int index, out DayEventConfig dayEvent)
        {
            dayEvent = index >= 0 && index < _events.Count ? _events[index] : null;
            return dayEvent != null;
        }

        public int PickIndex(int day)
        {
            var scheduled = _scheduled.Find(s => s.Day == day);

            if (scheduled != null && _events.Contains(scheduled.Event))
                return _events.IndexOf(scheduled.Event);

            var random = _events.Where(e => !_scheduled.Exists(s => s.Event == e)).ToList();
            return day >= _firstEventDay && random.Count > 0 && UnityEngine.Random.value < _eventChance ? _events.IndexOf(random[UnityEngine.Random.Range(0, random.Count)]) : -1;
        }
    }
}
