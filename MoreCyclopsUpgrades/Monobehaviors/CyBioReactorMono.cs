﻿namespace MoreCyclopsUpgrades.Monobehaviors
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using Common;
    using MoreCyclopsUpgrades.SaveData;
    using ProtoBuf;
    using UnityEngine;

    [ProtoContract]
    internal class CyBioReactorMono : HandTarget, IHandTarget, IProtoEventListener, IProtoTreeEventListener, ISubRootConnection
    {
        internal const int StorageWidth = 2;
        internal const int StorageHeight = 2;
        internal const int TotalContainerSpaces = StorageHeight * StorageWidth;
        internal const float ChargePerSecondPerItem = 0.80f / TotalContainerSpaces;
        internal const float MaxPower = 200;

        public SubRoot ParentCyclops { get; private set; }
        public Constructable Buildable { get; private set; }
        internal ItemsContainer Container { get; private set; }
        public Battery Battery { get; private set; }

        private static Dictionary<TechType, float> _bioReactorCharges;
        internal static readonly FieldInfo BioEnergyLookupInfo = typeof(BaseBioReactor).GetField("charge", BindingFlags.Static | BindingFlags.NonPublic);
        internal static Dictionary<TechType, float> BioReactorCharges
        {
            get
            {
                if (_bioReactorCharges is null)
                {
                    _bioReactorCharges = (Dictionary<TechType, float>)BioEnergyLookupInfo.GetValue(null);
                }

                return _bioReactorCharges;
            }
        }
        public static float GetChargeValue(TechType techType) => BioReactorCharges.GetOrDefault(techType, -1f);

        public bool ProducingPower => _constructed >= 1f && this.MaterialsProcessing.Count > 0;

        public int CurrentPower => Mathf.RoundToInt(this.Battery.charge);

        public string PowerString => $"{this.CurrentPower}/{MaxPower}";

        public override void Awake()
        {
            base.Awake();
            InitializeBattery();
            InitializeConstructible();
            InitializeStorageRoot();
            InitializeContainer();            
            InitializeSaveData();
        }

        private void InitializeContainer()
        {
            if (this.Container is null)
            {
                this.Container = new ItemsContainer(StorageWidth, StorageHeight, storageRoot.transform, "CyBioReactorStorageLabel", null);

                this.Container.isAllowedToAdd += IsAllowedToAdd;
                this.Container.isAllowedToRemove += IsAllowedToRemove;

                this.Container.onAddItem += OnAddItem;
                this.Container.onRemoveItem += OnRemoveItem;
            }
        }

        private void InitializeSaveData()
        {
            if (SaveData is null)
            {
                string id = GetComponentInParent<PrefabIdentifier>().Id;
                SaveData = new CyBioReactorSaveData(id);
            }
        }

        private void InitializeBattery()
        {
            if (this.Battery is null)
            {
                this.Battery = this.GetComponent<Battery>();

                this.Battery._capacity = MaxPower;
                this.Battery._charge = 0; // Starts empty
            }
        }

        private void InitializeStorageRoot()
        {
            if (storageRoot is null)
            {
                var storeRoot = new GameObject("StorageRoot");
                storeRoot.transform.SetParent(this.transform, false);
                storageRoot = storeRoot.AddComponent<ChildObjectIdentifier>();
            }
        }

        private void InitializeConstructible()
        {
            if (this.Buildable is null)
            {
                this.Buildable = this.gameObject.GetComponent<Constructable>();
            }
        }

        private void Update()
        {
            if (this.ProducingPower)
            {
                float powerDeficit = this.Battery.capacity - this.Battery.charge;

                if (powerDeficit > 0f)
                {
                    float chargeOverTime = ChargePerSecondPerItem * DayNightCycle.main.deltaTime;

                    float powerProduced = ProducePower(Mathf.Min(powerDeficit, chargeOverTime));

                    this.Battery.charge += powerProduced;
                }
            }
        }

        public void OnHandHover(GUIHand guiHand)
        {
            if (!this.Buildable.constructed)
                return;

            HandReticle main = HandReticle.main;
            main.SetInteractText($"Use Cyclops BioReacactor {this.PowerString}");
            main.SetIcon(HandReticle.IconType.Hand, 1f);
        }

        public void OnHandClick(GUIHand guiHand)
        {
            PDA pda = Player.main.GetPDA();
            Inventory.main.SetUsedStorage(this.Container, false);
            pda.Open(PDATab.Inventory, null, null, 4f);
        }

        private void OnAddItem(InventoryItem item)
        {
            item.isEnabled = false;

            if (isLoadingSaveData)
            {
                return;
            }

            if (BioReactorCharges.TryGetValue(item.item.GetTechType(), out float bioEnergyValue) && bioEnergyValue > 0f)
            {
                var bioenergy = new BioEnergy(item.item, bioEnergyValue, bioEnergyValue);

                this.MaterialsProcessing.Add(bioenergy);
            }
            else
            {
                Destroy(item.item.gameObject); // Failsafe
            }

        }

        private void OnRemoveItem(InventoryItem item)
        {

        }

        private bool IsAllowedToAdd(Pickupable pickupable, bool verbose)
        {
            if (isLoadingSaveData)
                return true;

            bool canAdd = false;
            if (pickupable != null)
            {
                TechType techType = pickupable.GetTechType();

                if (BioReactorCharges.ContainsKey(techType))
                {
                    canAdd = true;
                }
            }

            if (!canAdd && verbose)
            {
                ErrorMessage.AddMessage(Language.main.Get("BaseBioReactorCantAddItem"));
            }

            return canAdd;
        }

        private bool IsAllowedToRemove(Pickupable pickupable, bool verbose) => false;

        private float ProducePower(float powerDrawnPerItem)
        {
            float powerProduced = 0f;

            if (powerDrawnPerItem > 0f && // More than zero energy being produced per item per time delta
                this.MaterialsProcessing.Count > 0) // There should be materials in the reactor to process
            {
                foreach (BioEnergy material in this.MaterialsProcessing)
                {
                    float availablePowerPerItem = Mathf.Min(material.RemainingEnergy, powerDrawnPerItem);

                    material.RemainingEnergy -= availablePowerPerItem;
                    powerProduced += availablePowerPerItem;

                    if (material.FullyConsumed)
                        this.FullyConsumed.Add(material);
                }
            }

            if (this.FullyConsumed.Count > 1)
            {
                foreach (BioEnergy material in this.FullyConsumed)
                {
                    this.MaterialsProcessing.Remove(material);
                    this.Container.RemoveItem(material.Pickupable, true);
                    Destroy(material.Pickupable.gameObject);
                }

                this.FullyConsumed.Clear();
            }

            return powerProduced;
        }

        public float Constructed
        {
            get => _constructed;
            set
            {
                value = Mathf.Clamp01(value);
                if (_constructed != value)
                {
                    _constructed = value;
                    if (_constructed < 1f)
                    {
                        if (_constructed <= 0f)
                        {
                            Destroy(this.gameObject);
                        }
                    }
                }
            }
        }

        public void OnProtoSerialize(ProtobufSerializer serializer)
        {
            SaveData.ReactorBatterCharge = this.Battery.charge;
            SaveData.SaveMaterialsProcessing(this.MaterialsProcessing);

            SaveData.Save();
        }

        public void OnProtoDeserialize(ProtobufSerializer serializer)
        {
            isLoadingSaveData = true;

            InitializeStorageRoot();

            this.Container.Clear(false);

            isLoadingSaveData = false;
        }

        public void OnProtoSerializeObjectTree(ProtobufSerializer serializer)
        {
        }

        public void OnProtoDeserializeObjectTree(ProtobufSerializer serializer)
        {
            isLoadingSaveData = true;

            bool hasSaveData = SaveData.Load();

            if (hasSaveData)
            {
                this.Container.Clear();

                this.Battery.charge = SaveData.ReactorBatterCharge;

                List<BioEnergy> materials = SaveData.GetMaterialsInProcessing();

                foreach (BioEnergy item in materials)
                {
                    this.Container.AddItem(item.Pickupable);
                    this.MaterialsProcessing.Add(item);
                }
            }

            isLoadingSaveData = false;
        }

        public void ConnectToCyclops(SubRoot parentCyclops)
        {
            QuickLogger.Debug("BioReactor has been connected to Cyclops", true);
            this.ParentCyclops = parentCyclops;
            this.transform.SetParent(parentCyclops.transform);
        }

        [ProtoMember(3)]
        [NonSerialized]
        public float _constructed = 1f;

        [AssertNotNull]
        public ChildObjectIdentifier storageRoot;

        internal List<BioEnergy> MaterialsProcessing { get; } = new List<BioEnergy>();
        private List<BioEnergy> FullyConsumed { get; } = new List<BioEnergy>(TotalContainerSpaces);

        private bool isLoadingSaveData = false;
        private CyBioReactorSaveData SaveData;
    }
}