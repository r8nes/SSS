using System;
using System.Collections.Generic;
using UnityEngine;

namespace Aetheria.Runtime.Components.Volumetrics
{
    internal sealed class ActivePriorityRegistry<T> where T : Behaviour
    {
        private readonly List<T> _items = new();
        private readonly Func<T, int> _priority;
        
        private int _sortedStateHash = int.MinValue;

        public ActivePriorityRegistry(Func<T, int> priority) => _priority = priority;

        public void Register(T item)
        {
            Prune();
            
            if (!_items.Contains(item))
                _items.Add(item);
            
            _sortedStateHash = int.MinValue;
        }

        public void Unregister(T item)
        {
            if (_items.Remove(item))
                _sortedStateHash = int.MinValue;
        }

        public void Clear()
        {
            _items.Clear();
            _sortedStateHash = int.MinValue;
        }

        public void Collect(List<T> destination)
        {
            destination.Clear();
            Prune();
            EnsureSorted();
            destination.AddRange(_items);
        }

        private void Prune()
        {
            bool changed = false;
            
            for (int i = _items.Count - 1; i >= 0; --i)
            {
                Behaviour component = _items[i];
                
                if (component && component.isActiveAndEnabled && component.gameObject.scene.IsValid())
                    continue;
                
                _items.RemoveAt(i);
                changed = true;
            }

            if (changed)
                _sortedStateHash = int.MinValue;
        }

        private void EnsureSorted()
        {
            int hash = ComputeSortStateHash();
            
            if (hash == _sortedStateHash)
                return;

            _items.Sort(Compare);
            _sortedStateHash = ComputeSortStateHash();
        }

        private int ComputeSortStateHash()
        {
            unchecked
            {
                int hash = 17;
                hash = hash * 31 + _items.Count;
                
                for (int i = 0; i < _items.Count; ++i)
                {
                    var item = _items[i];
                    Behaviour component = item;
                    hash = hash * 31 + (component ? component.GetInstanceID() : 0);
                    hash = hash * 31 + (component ? _priority(item) : 0);
                }

                return hash;
            }
        }

        private int Compare(T a, T b)
        {
            Behaviour ca = a;
            Behaviour cb = b;
            
            if (ReferenceEquals(a, b)) return 0;
            if (!ca) return 1;
            if (!cb) return -1;
            
            int compare = _priority(b).CompareTo(_priority(a));
            
            return compare != 0 ? compare : ca.GetInstanceID().CompareTo(cb.GetInstanceID());
        }
    }
}