using System;

namespace ShooterShop.Saves
{
    public interface ISaveEvents
    {
        event Action Saved;
    }
}
