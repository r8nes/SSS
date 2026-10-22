using UnityEngine;

namespace ShooterShop.Economy
{
    public static class Money
    {
        public static int Round(float amount)
        {
            if (amount <= 0f)
                return 0;

            var step = amount >= 1000f ? 50 : amount >= 100f ? 10 : 5;
            return Mathf.Max(step, Mathf.RoundToInt(amount / step) * step);
        }
    }
}
