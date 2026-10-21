using UnityEngine;

namespace ShooterShop.Street
{
    public interface IPedestrianDamage
    {
        void ServerHit(Pedestrian pedestrian, Vector3 direction, Vector3 point);
    }
}
