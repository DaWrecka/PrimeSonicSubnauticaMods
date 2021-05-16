﻿namespace CustomCraft2SML
{
    using System.Collections;
    using CustomCraft2SML.Interfaces;
    using SMLHelper.V2.Assets;
    using UnityEngine;

    internal class FunctionalClone : Spawnable
    {
        internal readonly TechType BaseItem;
        public FunctionalClone(IAliasRecipe aliasRecipe, TechType baseItem)
            : base(aliasRecipe.ItemID, $"{aliasRecipe.ItemID}Prefab", aliasRecipe.Tooltip)
        {
            BaseItem = baseItem;
            this.TechType = aliasRecipe.TechType; // TechType already handled by this point
        }

        public override string AssetsFolder { get; } = FileLocations.RootModName + "/Assets";

        public override IEnumerator GetGameObjectAsync(IOut<GameObject> gameObject)
        {
            CoroutineTask<GameObject> task = CraftData.GetPrefabForTechTypeAsync(BaseItem);
            yield return task;
            GameObject prefab = task.GetResult();
            GameObject obj = Object.Instantiate(prefab);

            gameObject.Set(obj);
        }
    }
}
