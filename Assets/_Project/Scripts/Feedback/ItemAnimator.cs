using PrimeTween;
using UnityEngine;

namespace ShooterShop.Feedback
{
    public sealed class ItemAnimator
    {
        private readonly FeedbackConfig _config;

        public ItemAnimator(FeedbackConfig config)
        {
            _config = config;
        }

        public void FlyInto(Transform item, Vector3 fromWorld)
        {
            Tween.StopAll(item);

            var start = item.parent.InverseTransformPoint(fromWorld);
            var end = Vector3.zero;
            var lift = item.parent.InverseTransformVector(Vector3.up * _config.PlaceArcHeight);

            item.localPosition = start;
            Tween.Custom(item, 0f, 1f, _config.PlaceDuration, (target, t) =>
                    target.localPosition = Vector3.Lerp(start, end, t) + lift * (4f * t * (1f - t)),
                _config.PlaceEase);
            Tween.Scale(item, Vector3.one * _config.PlaceStartScale, Vector3.one, _config.PlaceDuration, Ease.OutBack);
            Tween.Delay(item, _config.PlaceDuration, Settle);
        }

        public void Settle(Transform item)
        {
            var strength = _config.SettleStrength;
            Tween.PunchScale(item, new Vector3(strength, -strength, strength), _config.SettleDuration, 6f);
        }

        public void PopIn(Transform item, Vector3 targetScale)
        {
            Tween.StopAll(item);
            Tween.Scale(item, Vector3.zero, targetScale, _config.PopDuration, _config.PopEase);
            Tween.Delay(item, _config.PopDuration, Settle);
        }

        public void Vanish(GameObject item)
        {
            var target = item.transform;
            Tween.StopAll(target);
            Tween.Scale(target, Vector3.zero, _config.VanishDuration, Ease.InBack)
                .OnComplete(item, Object.Destroy);
        }

        public void Punch(Transform item)
        {
            Tween.PunchScale(item, item.localScale * _config.PunchStrength, _config.PunchDuration);
        }

        public void Throw(Transform item, Vector3 from, Vector3 to, float duration)
        {
            Tween.StopAll(item);

            var lift = Vector3.up * _config.ThrowArcHeight;
            item.position = from;
            Tween.Custom(item, 0f, 1f, duration, (target, t) =>
                target.position = Vector3.Lerp(from, to, t) + lift * (4f * t * (1f - t)), Ease.Linear);
        }

        public void Land(Transform item, Vector3 from, Vector3 to)
        {
            Tween.Position(item, from, to, _config.LandDuration, _config.LandEase);
        }
    }
}
