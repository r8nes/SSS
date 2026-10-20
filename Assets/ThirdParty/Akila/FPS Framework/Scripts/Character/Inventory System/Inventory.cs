using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Akila.FPSFramework
{
    [AddComponentMenu("Akila/FPS Framework/Player/Inventory")]
    public class Inventory : MonoBehaviour, IInventory
    {
        public List<InventoryItem> startItems = new List<InventoryItem>();
        public List<InventoryItem> items = new List<InventoryItem>();
        public List<InventoryCollectable> collectables = new List<InventoryCollectable>();

        [Space]
        public InventoryItem defaultItem;
        public int maxSlots = 3;
        public float dropForce = 1;
        public Transform dropLocation;

        public CharacterManager characterManager { get; set; }

        public CharacterInput characterInput { get; set; }

        public bool isActive { get; set; } = true;

        private int previousItemIndex { get; set; }
        public int currentItemIndex { get; set; }


        List<InventoryItem> IInventory.items { get => items; set => items = value; }
        int IInventory.maxSlots { get => maxSlots; }
        float IInventory.dropForce {  get => dropForce; }
        Transform IInventory.dropPoint { get => dropLocation; }
        public bool isInputActive { get; set; } = true;

        public InventoryItem _defaultItem { get; set; }

        public Transform defaultItemParent { get; protected set; }

        public InventoryItem currentDefaultItem { get => _defaultItem; set => _defaultItem = value; }
        List<InventoryCollectable> IInventory.collectables { get => collectables; }
        public InventoryItem currentItem { get => activeItem; }

        private void Start()
        {
            if (items.Count > 0)
            {
                Debug.LogWarning($"[Warning] Items list has {items.Count} elements, but it should be empty. This may indicate an unexpected state. Emptying the list now.", this);

                items.Clear();
            }

            characterManager = GetComponentInParent<CharacterManager>();

            characterInput = GetComponentInParent<CharacterInput>();

            if (isActive)
            {
                foreach (InventoryItem startItem in startItems)
                {
                    InventoryItem newItem = Instantiate(startItem, transform);
                }

                if (defaultItem)
                {
                    defaultItemParent = new GameObject("Isolated Inventory Holder").transform;

                    defaultItemParent.SetParent(transform.parent);

                    defaultItemParent.SetPositionAndRotation(transform.position, transform.rotation);

                    _defaultItem = Instantiate(defaultItem, defaultItemParent);

                    _defaultItem.isDroppingActive = false;
                }
            }

            Switch(0);

            InvokeRepeating(nameof(PerformSwitch), 0, 0.1f);
        }

        private void RefreshItemsList()
        {
            List<InventoryItem> _items = GetComponentsInChildren<InventoryItem>(true).ToList();

            if (defaultItem == null)
            {
                items = _items;

                return;
            }

            InventoryItem itemToRemove = _items.Find(e => e == _defaultItem);

            _items.Remove(itemToRemove);
            
            items = _items;
        }

        private void Update()
        {
            if (currentDefaultItem != null)
            {
                currentDefaultItem.transform.SetSiblingIndex(transform.childCount);
            }

            GetInput();

            RefreshItemsList();

            foreach(InventoryItem item in items)
            {
                if(item.quickSwitch && item.quickSwitchInputAction.triggered)
                {
                    int itemIndex = items.IndexOf(item);

                    currentItemIndex = itemIndex;
                }
            }

            if(currentDefaultItem != null)
            {
                if(currentDefaultItem.quickSwitch && currentDefaultItem.quickSwitchInputAction.triggered)
                {
                    currentItemIndex = -1;
                }
                
                if (items.Count == 0) currentItemIndex = -1;
            }


            //Switch item if item index changes
            if (previousItemIndex != currentItemIndex)
            {
                //Empty
                lastItemIndex = previousItemIndex;
            }

            if(currentItemIndex == -1)
            {
                activeItem = currentDefaultItem;
            }
            else
            {
                activeItem = items.Count > 0 ? items[currentItemIndex] : null;
            }


            //Update the item index
            previousItemIndex = currentItemIndex;
        }

        private void PerformSwitch()
        {
            Switch(currentItemIndex);
        }

        private void GetInput()
        {
            if (!FPSFrameworkCore.IsActive || !FPSFrameworkCore.IsInputActive || !isInputActive) return;

            if (characterInput == null)
                return;

            if (characterInput.item1) currentItemIndex = 0;
            if (characterInput.item2 && items.Count > 1) currentItemIndex = 1;
            if (characterInput.item3 && items.Count > 2) currentItemIndex = 2;
            if (characterInput.item4 && items.Count > 3) currentItemIndex = 3;
            if (characterInput.item5 && items.Count > 4) currentItemIndex = 4;
            if (characterInput.item6 && items.Count > 5) currentItemIndex = 5;
            if (characterInput.item7 && items.Count > 6) currentItemIndex = 6;
            if (characterInput.item8 && items.Count > 7) currentItemIndex = 7;
            if (characterInput.item9 && items.Count > 8) currentItemIndex = 8;
            
            if (characterInput.itemUp && items.Count > 1)
            {
                currentItemIndex = (currentItemIndex + 1) % maxSlots;
            }

            if (characterInput.itemDown && items.Count > 1)
            {
                currentItemIndex = (currentItemIndex - 1) % maxSlots;
            }

            if(characterInput.itemNext) currentItemIndex++;
            if (characterInput.itemDown) currentItemIndex = lastItemIndex;
        }

        private int lastItemIndex;
        private InventoryItem activeItem;

        /// <summary>
        /// Switches the currently active inventory item by index.
        /// Pass -1 to activate the default item instead.
        /// </summary>
        /// <param name="index">Index of the item to activate. Use -1 to activate the default item.</param>
        /// <param name="immediate">Currently unused. Intended for future transition logic.</param>
        public void Switch(int index, bool immediate = true)
        {
            // Refresh the list of inventory items (in case it has changed)
            RefreshItemsList();

            // Store the current index for reference
            currentItemIndex = index;

            // Determine if we should activate the default item
            bool useDefault = (index == -1);

            if (useDefault)
            {
                // If a default item is defined, activate it and deactivate all others
                if (_defaultItem)
                {
                    _defaultItem.gameObject.SetActive(true);
                }

                // Deactivate all inventory items
                for (int i = 0; i < items.Count; i++)
                {
                    items[i].gameObject.SetActive(false);
                }

                return; // Done with default item switching
            }

            // If not using default item, deactivate the default if it exists
            if (_defaultItem)
            {
                _defaultItem.gameObject.SetActive(false);
            }

            // Activate the selected item by index, deactivate the others
            for (int i = 0; i < items.Count; i++)
            {
                items[i].gameObject.SetActive(i == index);
            }
        }


        public void DropAllItems()
        {
            items = transform.GetComponentsInChildren<InventoryItem>(true).ToList();

            foreach (InventoryItem item in items)
            {
                if (item != null)
                {
                    item.Drop(false);
                }
            }

            items.Clear();
        }

        /// <summary>
        /// Editor-only context menu option to set up network components.
        /// </summary>
        [ContextMenu("Setup/Network Components")]
        private void SetupNetworkComponents()
        {
            FPSFrameworkCore.InvokeConvertMethod("ConvertInventory", this, new object[] { this });
        }
    }
}