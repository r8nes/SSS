using System;
using Akila.FPSFramework;
using UnityEngine;

namespace ShooterShop.Range
{
    public sealed class ShootingTarget : MonoBehaviour, IOnHit
    {
        public event Action<HitInfo> Hit;

        public void OnHit(HitInfo hitInfo) => Hit?.Invoke(hitInfo);
    }
}
