﻿namespace MoreCyclopsUpgrades
{
    using System.Collections.Generic;
    using Common.EasyMarkup;
    using SMLHelper.V2.Utility;
    using System.IO;

    public class AuxUpgradeConsoleSaveData : EmPropertyCollectionList<EmModuleSaveData>
    {
        private readonly string ID;

        private string SaveDirectory => Path.Combine(SaveUtils.GetCurrentSaveDataDir(), "AuxUpgradeConsole");
        private string SaveFile => Path.Combine(SaveDirectory, ID + ".txt");

        public IEnumerable<EmModuleSaveData> SavedModules
        {
            get
            {
                yield return (EmModuleSaveData)Collections[0];
                yield return (EmModuleSaveData)Collections[1];
                yield return (EmModuleSaveData)Collections[2];
                yield return (EmModuleSaveData)Collections[3];
                yield return (EmModuleSaveData)Collections[4];
                yield return (EmModuleSaveData)Collections[5];
            }
        }

        public EmModuleSaveData this[string slot]
        {
            get
            {
                switch (slot)
                {
                    case "Module1": return (EmModuleSaveData)Collections[0];
                    case "Module2": return (EmModuleSaveData)Collections[1];
                    case "Module3": return (EmModuleSaveData)Collections[2];
                    case "Module4": return (EmModuleSaveData)Collections[3];
                    case "Module5": return (EmModuleSaveData)Collections[4];
                    case "Module6": return (EmModuleSaveData)Collections[5];
                    default: return null;
                }
            }
        }

        public AuxUpgradeConsoleSaveData(string id) : base("AuxUpgradeConsoleSaveData", new EmModuleSaveData())
        {
            ID = id;
        }

        public void Save()
        {
            if (!Directory.Exists(SaveDirectory))
            {
                Directory.CreateDirectory(SaveDirectory);
            }

            File.WriteAllLines(SaveFile, new[]
            {
                "# This save data was generated by EasyMarkup #",
                this.PrintyPrint(),
            });
        }

        public bool Load()
        {
            string saveDir = SaveFile;
            if (!File.Exists(saveDir))
            {
                Save();
                return false;
            }

            string serializedData = File.ReadAllText(saveDir);

            bool validData = this.FromString(serializedData);

            if (!validData)
            {
                Save();
                return false;
            }

            return true;
        }
    }
}

