using Akila.FPSFramework;
using UnityEditor;
using UnityEngine;

#if MIRROR
using Mirror;
#endif

namespace Akila.FPSFrameworkPro
{
    public class FPSFrameworkProCore
    {
#if UNITY_EDITOR
        #region Components Wizards
        public static void ConvertPlayer(FirstPersonController controller)
        {
            GameObject obj = controller.gameObject;

            AddNetIdentity(obj);
            AddNetTransformReliable(obj);

            if (obj.GetComponent<NetworkFirstPersonController>() == null)
            {
                Undo.AddComponent<NetworkFirstPersonController>(obj);
            }

            if (obj.GetComponent<Actor>() && obj.GetComponent<NetworkActor>() == null)
            {
                Undo.AddComponent<NetworkActor>(obj);
            }

            if (obj.GetComponent<Damageable>() && obj.GetComponent<NetworkDamageable>() == null)
            {
                Undo.AddComponent<NetworkDamageable>(obj);
            }

            if (obj.GetComponentInChildren<Inventory>())
            {
                ConvertInventory(obj.GetComponentInChildren<Inventory>());
            }

            if (obj.GetComponentInChildren<InteractionsManager>())
            {
                ConvertInteractionsManager(obj.GetComponentInChildren<InteractionsManager>());
            }
        }

        public static void ConvertProjectile(Projectile projectile)
        {
            GameObject obj = projectile.gameObject;

            AddNetIdentity(obj);
            AddNetRigidbodyReliable(obj);

            if (obj.GetComponent<NetworkProjectile>() == null)
            {
                Undo.AddComponent<NetworkProjectile>(obj);
            }
        }

        public static void ConvertExplosive(Explosive explosion)
        {
            GameObject obj = explosion.gameObject;

            AddNetIdentity(obj);
            AddNetRigidbodyReliable(obj);
            ConvertDamageable(explosion);

            if(obj.GetComponent<NetworkExplosive>() == null)
            {
                Undo.AddComponent<NetworkExplosive>(obj);
            }
        }

        public static void ConvertDamageable(IDamageable damageable)
        {
            GameObject obj = damageable.gameObject;

            AddNetIdentity(obj.gameObject);

            if (obj.GetComponent<NetworkDamageable>() == null)
            {
                Undo.AddComponent<NetworkDamageable>(obj);
            }
        }

        public static void ConvertPickable(Pickable pickable)
        {
            GameObject obj = pickable.gameObject;

            AddNetIdentity(obj);
            AddNetRigidbodyReliable(obj);

            if (obj.GetComponent<NetworkPickable>() == null)
            {
                Undo.AddComponent<NetworkPickable>(obj);
            }
        }

        public static void ConvertInventory(Inventory inventory)
        {
            GameObject obj = inventory.gameObject;

            if (obj.GetComponent<NetworkInventory>() == null)
            {
                Undo.AddComponent<NetworkInventory>(obj);
            }

            if (obj.GetComponent<NetworkItemsManager>() == null)
            {
                Undo.AddComponent<NetworkItemsManager>(obj);
            }
        }

        public static void ConvertInteractionsManager(InteractionsManager interactionsManager)
        {
            GameObject obj = interactionsManager.gameObject;

            if (obj.GetComponent<NetworkInteractionsManager>() == null)
            {
                Undo.AddComponent<NetworkInteractionsManager>(obj);
            }
        }

        public static void ConvertFirearm(Firearm firearm)
        {
            GameObject obj = firearm.gameObject;

            if (obj.GetComponent<NetworkFirearm>() == null)
            {
                Undo.AddComponent<NetworkFirearm>(obj);
            }
        }

        private static void AddNetIdentity(GameObject obj)
        {
#if MIRROR
            if (obj.GetComponent<NetworkIdentity>() == null)
            {
                Undo.AddComponent<NetworkIdentity>(obj);
            }
#endif
        }
        private static void AddNetTransformReliable(GameObject obj)
        {
#if MIRROR
            if (obj.GetComponent<NetworkTransformReliable>() == null)
            {
                Undo.AddComponent<NetworkTransformReliable>(obj);
            }
#endif
        }

        private static void AddNetRigidbodyReliable(GameObject obj)
        {
#if MIRROR
            if (obj.GetComponent<NetworkRigidbodyReliable>() == null)
            {
                NetworkRigidbodyReliable networkRigidbodyReliable = Undo.AddComponent<NetworkRigidbodyReliable>(obj);

                networkRigidbodyReliable.syncDirection = SyncDirection.ServerToClient;
            }
#endif
        }
#endregion
#endif
        }
    }