using UnityEngine;

namespace ShooterShop.Feedback
{
    public interface IShopFeed
    {
        void Notify(string message, NotificationKind kind = NotificationKind.Info);
        void Popup(Vector3 position, string text, NotificationKind kind = NotificationKind.Good);
    }
}
