using UnityEngine;

namespace ShooterShop.Feedback
{
    public interface IScreenLabels
    {
        ScreenLabel Create(Transform anchor, Vector3 offset, ScreenLabelStyle style);
        void Release(ScreenLabel label);
    }
}
