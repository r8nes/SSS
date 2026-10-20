using System;
using System.Collections.Generic;
using Akila.FPSFramework;
using UnityEngine;

namespace ShooterShop.Input
{
    public sealed class AkilaPlayerInputLock : IPlayerInputLock, IDisposable
    {
        private readonly HashSet<object> _owners = new();

        public bool IsLocked => _owners.Count > 0;

        public void Acquire(object owner)
        {
            if (_owners.Add(owner) && _owners.Count == 1)
                Apply(true);
        }

        public void Release(object owner)
        {
            if (_owners.Remove(owner) && _owners.Count == 0)
                Apply(false);
        }

        public void Dispose()
        {
            if (IsLocked)
            {
                _owners.Clear();
                Apply(false);
            }
        }

        private static void Apply(bool locked)
        {
            FPSFrameworkCore.IsInputActive = !locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }
    }
}
