using System;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public interface IShopFeedEvents
    {
        event Action<string, NotificationKind> Notified;
        event Action<Vector3, string, NotificationKind> PoppedUp;
    }
}
