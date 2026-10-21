using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

namespace ShooterShop.Street
{
    public sealed class PedestrianRegistry : ITickable
    {
        private readonly List<Pedestrian> _pedestrians = new();

        public void Add(Pedestrian pedestrian) => _pedestrians.Add(pedestrian);

        public void Remove(Pedestrian pedestrian) => _pedestrians.Remove(pedestrian);

        public void Tick()
        {
            var deltaTime = Time.deltaTime;

            for (var i = _pedestrians.Count - 1; i >= 0; i--)
            {
                if (_pedestrians[i] == null)
                    _pedestrians.RemoveAt(i);
                else
                    _pedestrians[i].ClientTick(deltaTime);
            }
        }
    }
}
