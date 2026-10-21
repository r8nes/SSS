using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class ViewCamera : IViewCamera
    {
        private Camera _cached;

        public Camera Current
        {
            get
            {
                if (_cached == null || !_cached.isActiveAndEnabled)
                    _cached = Camera.main;

                return _cached;
            }
        }
    }
}
