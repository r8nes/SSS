using System;

namespace Akila.FPSFramework
{
    public class ActorData
    {
        public string name;
        public int kills;
        public int deaths;
        public int teamID;

        public Action onUpdated;

        public virtual void Update(ActorData actorData)
        {
            name = actorData.name;
            kills = actorData.kills;
            deaths = actorData.deaths;
            teamID = actorData.teamID;

            onUpdated?.Invoke();
        }
    }
}