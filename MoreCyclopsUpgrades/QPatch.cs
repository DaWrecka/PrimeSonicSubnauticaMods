﻿namespace MoreCyclopsUpgrades
{
    using System;
    using System.IO;
    using System.Reflection;
    using Buildables;
    using Caching;
    using Common;
    using Harmony;
    using Modules;
    using MoreCyclopsUpgrades.CyclopsUpgrades;
    using MoreCyclopsUpgrades.Managers;
    using MoreCyclopsUpgrades.Modules.Enhancement;
    using SaveData;

    /// <summary>
    /// Entry point class for patching. For use by QModManager only.
    /// </summary>
    public class QPatch
    {
        /// <summary>
        /// Main patching method. For use by QModManager only.
        /// </summary>
        public static void Patch()
        {
#if RELEASE
            QuickLogger.DebugLogsEnabled = false;
#endif

#if DEBUG
            QuickLogger.DebugLogsEnabled = true;
#endif

            try
            {
                QuickLogger.Info("Started patching " + QuickLogger.GetAssemblyVersion());

                ModConfig.Initialize();

                QuickLogger.Info($"Difficult set to {ModConfig.Settings.PowerLevel}");

                OtherMods.VehicleUpgradesInCyclops = Directory.Exists(@"./QMods/VehicleUpgradesInCyclops");

                if (OtherMods.VehicleUpgradesInCyclops)
                    QuickLogger.Debug("VehicleUpgradesInCyclops detected. Correcting placement of craft nodes in Cyclops Fabricator.");

                // TODO - Configure cyclops power levels

                RegisterOriginalUpgrades();

                PatchUpgradeModules(ModConfig.Settings.EnableNewUpgradeModules);

                PatchAuxUpgradeConsole(ModConfig.Settings.EnableAuxiliaryUpgradeConsoles);

                PatchBioEnergy(ModConfig.Settings.EnableBioReactors);

                RegisterPowerManagerUpgrades();

                var harmony = HarmonyInstance.Create("com.morecyclopsupgrades.psmod");
                harmony.PatchAll(Assembly.GetExecutingAssembly());

                QuickLogger.Info("Finished Patching");

            }
            catch (Exception ex)
            {
                QuickLogger.Error(ex);
            }
        }

        private static void PatchUpgradeModules(bool enableNewUpgradeModules)
        {
            if (enableNewUpgradeModules)
                QuickLogger.Info("Patching new upgrade modules");
            else
                QuickLogger.Info("New upgrade modules disabled by config settings");

            CyclopsModule.PatchAllModules(enableNewUpgradeModules);
        }

        private static void PatchAuxUpgradeConsole(bool enableAuxiliaryUpgradeConsoles)
        {
            if (enableAuxiliaryUpgradeConsoles)
                QuickLogger.Info("Patching Auxiliary Upgrade Console");
            else
                QuickLogger.Info("Auxiliary Upgrade Console disabled by config settings");

            CyUpgradeConsole.PatchAuxUpgradeConsole(enableAuxiliaryUpgradeConsoles);
        }

        private static void PatchBioEnergy(bool enableBioreactors)
        {
            if (enableBioreactors)
                QuickLogger.Info("Patching Cyclops Bioreactor");
            else
                QuickLogger.Info("Cyclops Bioreactor disabled by config settings");

            CyBioReactor.PatchCyBioReactor(enableBioreactors);
        }

        private static void RegisterOriginalUpgrades()
        {
            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: Depth Upgrades Collection");
                return new CrushDepthUpgradesHandler(cyclops);
            });

            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: CyclopsShieldModule");
                return new UpgradeHandler(TechType.CyclopsShieldModule, cyclops)
                {
                    OnClearUpgrades = () => { cyclops.shieldUpgrade = false; },
                    OnUpgradeCounted = (Equipment modules, string slot) => { cyclops.shieldUpgrade = true; },
                };
            });

            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: CyclopsSonarModule");
                return new UpgradeHandler(TechType.CyclopsSonarModule, cyclops)
                {
                    OnClearUpgrades = () => { cyclops.sonarUpgrade = false; },
                    OnUpgradeCounted = (Equipment modules, string slot) => { cyclops.sonarUpgrade = true; },
                };
            });

            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: CyclopsSeamothRepairModule");
                return new UpgradeHandler(TechType.CyclopsSeamothRepairModule, cyclops)
                {
                    OnClearUpgrades = () => { cyclops.vehicleRepairUpgrade = false; },
                    OnUpgradeCounted = (Equipment modules, string slot) => { cyclops.vehicleRepairUpgrade = true; },
                };
            });

            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: CyclopsDecoyModule");
                return new UpgradeHandler(TechType.CyclopsDecoyModule, cyclops)
                {
                    OnClearUpgrades = () => { cyclops.decoyTubeSizeIncreaseUpgrade = false; },
                    OnUpgradeCounted = (Equipment modules, string slot) => { cyclops.decoyTubeSizeIncreaseUpgrade = true; },
                };
            });

            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: CyclopsFireSuppressionModule");
                return new UpgradeHandler(TechType.CyclopsFireSuppressionModule, cyclops)
                {
                    OnClearUpgrades = () =>
                    {
                        CyclopsHolographicHUD fss = cyclops.GetComponentInChildren<CyclopsHolographicHUD>();
                        if (fss != null)
                            fss.fireSuppressionSystem.SetActive(false);
                    },
                    OnUpgradeCounted = (Equipment modules, string slot) =>
                    {
                        CyclopsHolographicHUD fss = cyclops.GetComponentInChildren<CyclopsHolographicHUD>();
                        if (fss != null)
                            fss.fireSuppressionSystem.SetActive(true);
                    },
                };
            });
        }

        private static void RegisterPowerManagerUpgrades()
        {
            int maxModules = ModConfig.Settings.MaxChargingModules();

            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: Engine Upgrades Collection");
                PowerManager powerManager = CyclopsManager.GetPowerManager(cyclops);

                var efficiencyUpgrades = new TieredUpgradesHandlerCollection<int>(0, cyclops)
                {
                    OnFinishedUpgrades = () =>
                    {
                        powerManager.UpdatePowerSpeedRating();
                    }
                };

                QuickLogger.Debug("UpgradeHandler Registered: Engine Upgrade Mk1");
                TieredUpgradeHandler<int> engine1 = efficiencyUpgrades.CreateTier(TechType.PowerUpgradeModule, 1);

                QuickLogger.Debug("UpgradeHandler Registered: Engine Upgrade Mk2");
                TieredUpgradeHandler<int> engine2 = efficiencyUpgrades.CreateTier(CyclopsModule.PowerUpgradeMk2ID, 2);

                QuickLogger.Debug("UpgradeHandler Registered: Engine Upgrade Mk3");
                TieredUpgradeHandler<int> engine3 = efficiencyUpgrades.CreateTier(CyclopsModule.PowerUpgradeMk3ID, 3);

                powerManager.EngineEfficientyUpgrades = efficiencyUpgrades;
                return efficiencyUpgrades;
            });

            UpgradeManager.RegisterHandlerCreator((SubRoot cyclops) =>
            {
                QuickLogger.Debug("UpgradeHandler Registered: SpeedBooster Upgrade");
                PowerManager powerManager = CyclopsManager.GetPowerManager(cyclops);

                var speed = new UpgradeHandler(CyclopsModule.SpeedBoosterModuleID, cyclops)
                {
                    MaxCount = maxModules,
                    OnFirstTimeMaxCountReached = () =>
                    {
                        ErrorMessage.AddMessage(CyclopsSpeedBooster.MaxRatingAchived);
                    },
                    OnFinishedUpgrades = () =>
                    {
                        powerManager.UpdatePowerSpeedRating();
                    }
                };

                return speed;
            });
        }
    }
}
