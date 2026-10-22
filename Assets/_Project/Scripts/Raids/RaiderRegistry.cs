using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Raids
{
    public sealed class RaiderRegistry : ITickable, IPostLateTickable
    {
        private readonly List<Raider> _raiders = new();

        public void Add(Raider raider) => _raiders.Add(raider);

        public void Remove(Raider raider) => _raiders.Remove(raider);

        public void Tick()
        {
            var deltaTime = Time.deltaTime;

            for (var i = _raiders.Count - 1; i >= 0; i--)
            {
                if (_raiders[i] == null)
                    _raiders.RemoveAt(i);
                else
                    _raiders[i].ClientTick(deltaTime);
            }
        }

        public void PostLateTick()
        {
            foreach (var raider in _raiders)
                if (raider != null)
                    raider.ClientPostAnimation();
        }
    }
}
