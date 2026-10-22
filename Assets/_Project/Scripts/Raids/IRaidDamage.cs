using UnityEngine;

namespace ShooterShop.Raids
{
    public interface IRaidDamage
    {
        void ServerHit(Raider raider, Vector3 direction, Vector3 point);
    }
}
