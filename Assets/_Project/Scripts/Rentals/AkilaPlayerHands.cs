using System.Linq;
using Akila.FPSFramework;
using Mirror;
using UnityEngine;

namespace ShooterShop.Rentals
{
    public sealed class AkilaPlayerHands : IPlayerHands
    {
        private readonly RentalWeaponCatalog _catalog;
        private NetworkIdentity _player;
        private IInventory _inventory;

        public AkilaPlayerHands(RentalWeaponCatalog catalog)
        {
            _catalog = catalog;
        }

        public RentalWeaponConfig HeldRental
        {
            get
            {
                var item = Inventory?.currentItem;
                return item != null ? _catalog.FindByItemName(item.Name) : null;
            }
        }

        private IInventory Inventory
        {
            get
            {
                var player = NetworkClient.localPlayer;

                if (player != _player)
                {
                    _player = player;
                    _inventory = player != null ? player.transform.SearchFor<IInventory>(true) : null;
                }

                return _inventory;
            }
        }

        public void Give(RentalWeaponConfig weapon)
        {
            var inventory = Inventory;

            if (inventory == null || weapon == null)
                return;

            var item = Object.Instantiate(weapon.Item, inventory.transform);
            Refresh(inventory);

            var index = inventory.items.IndexOf(item);

            if (index >= 0)
                inventory.Switch(index);
        }

        public void RemoveHeld()
        {
            var inventory = Inventory;
            var item = inventory?.currentItem;

            if (item == null)
                return;

            item.gameObject.SetActive(false);
            Object.Destroy(item.gameObject);

            inventory.items = inventory.items.Where(other => other != null && other != item).ToList();

            if (inventory.items.Count > 0)
                inventory.Switch(0);
        }

        private static void Refresh(IInventory inventory) =>
            inventory.items = inventory.transform.GetComponentsInChildren<InventoryItem>(true).ToList();
    }
}
