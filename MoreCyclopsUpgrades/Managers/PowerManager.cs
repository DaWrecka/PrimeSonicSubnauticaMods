﻿namespace MoreCyclopsUpgrades.Managers
{
    using Common;
    using CyclopsUpgrades;
    using Modules.Enhancement;
    using Monobehaviors;
    using MoreCyclopsUpgrades.CyclopsUpgrades.CyclopsCharging;
    using System;
    using System.Collections.Generic;
    using UnityEngine;

    /// <summary>
    /// Handles recharging the Cyclops and other power related tasks.
    /// </summary>
    public class PowerManager
    {
        private static readonly ICollection<ChargerCreator> OneTimeUseCyclopsChargers = new List<ChargerCreator>();

        /// <summary>
        /// <para>This event happens right before the PowerManager starts initializing a the registered <see cref="ICyclopsCharger"/>s.</para>
        /// <para>Use this if you need a way to know when you should call <see cref="RegisterOneTimeUseChargerCreator"/> for <see cref="ChargerCreator"/>s that cannot be created from a static context.</para>
        /// </summary>
        public static Action CyclopsChargersInitializing;

        /// <summary>
        /// Registers a <see cref="ChargerCreator"/> method that creates returns a new <see cref="UpgradeHandler"/> on demand and is only used once.
        /// </summary>
        /// <param name="createEvent">A method that takes no parameters a returns a new instance of an <see cref="ChargerCreator"/>.</param>
        public static void RegisterOneTimeUseChargerCreator(ChargerCreator createEvent)
        {
            OneTimeUseCyclopsChargers.Add(createEvent);
        }

        internal const float BatteryDrainRate = 0.01f;
        internal const float Mk2ChargeRateModifier = 1.15f; // The MK2 charging modules get a 15% bonus to their charge rate.

        private const float EnginePowerPenalty = 0.7f;

        internal const int MaxSpeedBoosters = 6;
        private const int PowerIndexCount = 4;

        /// <summary>
        /// "Practically zero" for all intents and purposes. Any energy value lower than this should be considered zero.
        /// </summary>
        public const float MinimalPowerValue = 0.001f;

        private static readonly float[] SlowSpeedBonuses = new float[MaxSpeedBoosters]
        {
            0.25f, 0.15f, 0.10f, 0.10f, 0.05f, 0.05f // Diminishing returns on speed modules
            // Max +70%
        };

        private static readonly float[] StandardSpeedBonuses = new float[MaxSpeedBoosters]
        {
            0.40f, 0.30f, 0.20f, 0.15f, 0.10f, 0.05f // Diminishing returns on speed modules
            // Max +120%
        };

        private static readonly float[] FlankSpeedBonuses = new float[MaxSpeedBoosters]
        {
            0.45f, 0.20f, 0.10f, 0.10f, 0.05f, 0.05f // Diminishing returns on speed modules
            // Max +95%
        };

        private static readonly float[] EnginePowerRatings = new float[PowerIndexCount]
        {
            1f, 3f, 5f, 6f
        };

        private static readonly float[] SilentRunningPowerCosts = new float[PowerIndexCount]
        {
            5f, 5f, 4f, 3f // Lower costs here don't show up until the Mk2
        };

        private static readonly float[] SonarPowerCosts = new float[PowerIndexCount]
        {
            10f, 10f, 8f, 7f // Lower costs here don't show up until the Mk2
        };

        private static readonly float[] ShieldPowerCosts = new float[PowerIndexCount]
        {
            50f, 50f, 42f, 34f // Lower costs here don't show up until the Mk2
        };

        internal readonly List<ICyclopsCharger> PowerChargers = new List<ICyclopsCharger>();

        internal readonly List<CyBioReactorMono> CyBioReactors = new List<CyBioReactorMono>();
        private readonly List<CyBioReactorMono> TempCache = new List<CyBioReactorMono>();

        internal UpgradeHandler SpeedBoosters;
        internal ChargingUpgradeHandler SolarCharger;
        internal ChargingUpgradeHandler ThermalCharger;
        internal BatteryUpgradeHandler SolarChargerMk2;
        internal BatteryUpgradeHandler ThermalChargerMk2;
        internal BatteryUpgradeHandler NuclearCharger;
        internal TieredUpgradesHandlerCollection<int> EngineEfficientyUpgrades;
        internal BioBoosterUpgradeHandler BioBoosters;

        internal SolarChargeHandler SolarCharging;
        internal ThermalChargeHandler ThermalCharging;
        internal BioChargeHandler BioCharging;
        internal NuclearChargeHandler NuclearCharging;

        internal CyclopsManager Manager;

        internal readonly SubRoot Cyclops;

        internal int MaxModules = MaxSpeedBoosters;

        private CyclopsMotorMode motorMode;
        private CyclopsMotorMode MotorMode => motorMode ?? (motorMode = Cyclops.GetComponentInChildren<CyclopsMotorMode>());

        private SubControl subControl;
        private SubControl SubControl => subControl ?? (subControl = Cyclops.GetComponentInChildren<SubControl>());

        private float lastKnownPowerRating = -1f;
        private int lastKnownSpeedBoosters = -1;
        private int lastKnownPowerIndex = -1;
        private int speedBoosterSkip = -1;

        internal PowerManager(SubRoot cyclops)
        {
            Cyclops = cyclops;
        }

        private float[] OriginalSpeeds { get; } = new float[3];

        internal bool Initialize(CyclopsManager manager)
        {
            if (Manager != null)
                return false; // Already initialized

            Manager = manager;

            InitializeChargingHandlers();

            // Store the original values before we start to change them
            this.OriginalSpeeds[0] = this.MotorMode.motorModeSpeeds[0];
            this.OriginalSpeeds[1] = this.MotorMode.motorModeSpeeds[1];
            this.OriginalSpeeds[2] = this.MotorMode.motorModeSpeeds[2];

            SyncBioReactors();

            return true;
        }

        internal void InitializeChargingHandlers()
        {
            CyclopsChargersInitializing?.Invoke();

            foreach (ChargerCreator method in OneTimeUseCyclopsChargers)
                PowerChargers.Add(method.Invoke(Cyclops));

            OneTimeUseCyclopsChargers.Clear();
        }

        internal void SyncBioReactors()
        {
            TempCache.Clear();

            SubRoot cyclops = Cyclops;

            CyBioReactorMono[] cyBioReactors = cyclops.GetAllComponentsInChildren<CyBioReactorMono>();

            foreach (CyBioReactorMono cyBioReactor in cyBioReactors)
            {
                if (TempCache.Contains(cyBioReactor))
                    continue; // This is a workaround because of the object references being returned twice in this array.

                TempCache.Add(cyBioReactor);

                if (cyBioReactor.ParentCyclops == null)
                {
                    QuickLogger.Debug("CyBioReactorMono synced externally");
                    // This is a workaround to get a reference to the Cyclops into the AuxUpgradeConsole
                    cyBioReactor.ConnectToCyclops(cyclops, Manager);
                }
            }

            if (TempCache.Count != CyBioReactors.Count)
            {
                CyBioReactors.Clear();
                CyBioReactors.AddRange(TempCache);
            }
        }

        /// <summary>
        /// Updates the Cyclops power and speed rating.
        /// Power Rating manages engine efficiency as well as the power cost of using Silent Running, Sonar, and Defense Shield.
        /// Speed rating manages bonus speed across all motor modes.
        /// </summary>
        internal void UpdatePowerSpeedRating()
        {
            int powerIndex = EngineEfficientyUpgrades.HighestValue;
            int speedBoosters = SpeedBoosters.Count;

            if (lastKnownPowerIndex != powerIndex)
            {
                lastKnownPowerIndex = powerIndex;

                Cyclops.silentRunningPowerCost = SilentRunningPowerCosts[powerIndex];
                Cyclops.sonarPowerCost = SonarPowerCosts[powerIndex];
                Cyclops.shieldPowerCost = ShieldPowerCosts[powerIndex];
            }

            // Speed modules can affect power rating too
            float efficiencyBonus = EnginePowerRatings[powerIndex];

            for (int i = 0; i < speedBoosters; i++)
            {
                efficiencyBonus *= EnginePowerPenalty;
            }

            int cleanRating = Mathf.CeilToInt(100f * efficiencyBonus);

            while (cleanRating % 5 != 0)
                cleanRating--;

            float powerRating = cleanRating / 100f;

            if (lastKnownPowerRating != powerRating)
            {
                lastKnownPowerRating = powerRating;

                Cyclops.currPowerRating = powerRating;

                // Inform the new power rating just like the original method would.
                ErrorMessage.AddMessage(Language.main.GetFormat("PowerRatingNowFormat", powerRating));
            }

            if (speedBoosters > MaxModules)
                return; // Exit here

            if (lastKnownSpeedBoosters != speedBoosters)
            {
                lastKnownSpeedBoosters = speedBoosters;

                float SlowMultiplier = 1f;
                float StandardMultiplier = 1f;
                float FlankMultiplier = 1f;

                // Calculate the speed multiplier with diminishing returns
                while (--speedBoosters > -1)
                {
                    SlowMultiplier += SlowSpeedBonuses[speedBoosters];
                    StandardMultiplier += StandardSpeedBonuses[speedBoosters];
                    FlankMultiplier += FlankSpeedBonuses[speedBoosters];
                }

                // These will apply when changing speed modes
                this.MotorMode.motorModeSpeeds[0] = this.OriginalSpeeds[0] * SlowMultiplier;
                this.MotorMode.motorModeSpeeds[1] = this.OriginalSpeeds[1] * StandardMultiplier;
                this.MotorMode.motorModeSpeeds[2] = this.OriginalSpeeds[2] * FlankMultiplier;

                // These will apply immediately
                CyclopsMotorMode.CyclopsMotorModes currentMode = this.MotorMode.cyclopsMotorMode;
                this.SubControl.BaseForwardAccel = this.MotorMode.motorModeSpeeds[(int)currentMode];

                ErrorMessage.AddMessage(CyclopsSpeedBooster.SpeedRatingText(lastKnownSpeedBoosters, Mathf.RoundToInt(StandardMultiplier * 100)));
            }
        }

        /// <summary>
        /// Recharges the cyclops' power cells using all charging modules across all upgrade consoles.
        /// </summary>
        internal void RechargeCyclops()
        {
            if (Time.timeScale == 0f) // Is the game paused?
                return;

            if (speedBoosterSkip < lastKnownSpeedBoosters)
            {
                speedBoosterSkip++; // Slightly slows down recharging with more speed boosters
                return;
            }

            speedBoosterSkip = 0;

            float powerDeficit = Cyclops.powerRelay.GetMaxPower() - Cyclops.powerRelay.GetPower();

            Manager.HUDManager.UpdateTextVisibility();

            float power = 0f;
            foreach (ICyclopsCharger charger in PowerChargers)
                power += charger.ProducePower(powerDeficit);

            ChargeCyclops(power, ref powerDeficit);
        }

        /// <summary>
        /// Gets the total available reserve power across all equipment upgrade modules.
        /// </summary>
        /// <returns>The <see cref="int"/> value of the total available reserve power.</returns>
        internal int GetTotalReservePower()
        {
            float availableReservePower = 0f;
            availableReservePower += SolarChargerMk2.TotalBatteryCharge;
            availableReservePower += ThermalChargerMk2.TotalBatteryCharge;
            availableReservePower += NuclearCharger.TotalBatteryCharge;

            foreach (CyBioReactorMono reactor in CyBioReactors)
                availableReservePower += reactor.Battery._charge;

            return Mathf.FloorToInt(availableReservePower);
        }

        private void ChargeCyclops(float availablePower, ref float powerDeficit)
        {
            if (powerDeficit < MinimalPowerValue)
                return; // No need to charge

            if (availablePower < MinimalPowerValue)
                return; // No power available

            Cyclops.powerRelay.AddEnergy(availablePower, out float amtStored);
            powerDeficit = Mathf.Max(0f, powerDeficit - availablePower);
        }
    }
}
