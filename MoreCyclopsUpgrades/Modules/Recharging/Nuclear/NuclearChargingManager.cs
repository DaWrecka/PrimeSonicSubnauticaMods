﻿namespace MoreCyclopsUpgrades
{
    using UnityEngine;

    /// <summary>
    /// This class handles keeping track of the nuclear batteries.
    /// </summary>
    internal static class NuclearChargingManager
    {
        internal const float BatteryDrainRate = 0.15f;
        internal const float MaxCharge = 6000f; // Less than the normal 20k for balance        

        /// <summary>
        /// Replaces a nuclear battery modules with Depleted Reactor Rods when they fully drained.
        /// </summary>
        public static void HandleBatteryDepletion(Equipment modules, string slotName, Battery nuclearBattery)
        {
            if (nuclearBattery.charge <= 0f) // Drained nuclear batteries are handled just like how the Nuclear Reactor handles depleated reactor rods
            {
                InventoryItem inventoryItem = modules.RemoveItem(slotName, true, false);
                Object.Destroy(inventoryItem.item.gameObject);
                modules.AddItem(slotName, SpawnDepletedModule(), true);
            }
        }
 
        private static InventoryItem SpawnDepletedModule()
        {
            GameObject prefab = CraftData.GetPrefabForTechType(TechType.DepletedReactorRod);
            GameObject gameObject = GameObject.Instantiate(prefab);

            gameObject.GetComponent<PrefabIdentifier>().ClassId = DepletedNuclearModule.DepletedNameID;
            gameObject.AddComponent<TechTag>().type = CyclopsModule.DepletedNuclearModuleID;

            Pickupable pickupable = gameObject.GetComponent<Pickupable>().Pickup(false);
            return new InventoryItem(pickupable);
        }
    }

}