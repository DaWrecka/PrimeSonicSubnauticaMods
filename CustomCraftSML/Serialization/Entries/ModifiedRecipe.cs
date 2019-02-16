﻿namespace CustomCraft2SML.Serialization.Entries
{
    using System;
    using System.Collections.Generic;
    using Common;
    using Common.EasyMarkup;
    using CustomCraft2SML.Interfaces;
    using CustomCraft2SML.Serialization.Components;
    using CustomCraft2SML.Serialization.Lists;
    using SMLHelper.V2.Crafting;
    using SMLHelper.V2.Handlers;

    internal class ModifiedRecipe : EmTechTyped, IModifiedRecipe
    {
        internal static readonly string[] TutorialText = new[]
        {
           $"{ModifiedRecipeList.ListKey}: Modify an existing crafting recipe. ",
           $"    {IngredientsKey}: Completely replace a recipe's required ingredients." +
            "        This is optional if you don't want to change the required ingredients.",
           $"    {AmountCraftedKey}: Change how many copies of the item are created when you craft the recipe.",
            "        This is optional if you don't want to change how many copies of the item are created at once.",
           $"    {LinkedItemsIdsKey}: Add or modify the linked items that are created when the recipe is crafted.",
            "        This is optional if you don't want to change what items are crafted with this one.",
           $"    {UnlocksKey}: Set other recipes to be unlocked when you analyze or craft this one.",
            "        This is optional if you don't want to change what gets unlocked when you scan or craft this item.",
           $"    {ForceUnlockAtStartKey}: You can also set if this recipe should be unlocked at the start or not. Make sure you have a recipe unlocking this one.",
            "        This is optional. For Added Recipes, this defaults to 'YES'.",
        };

        protected const string AmountCraftedKey = "AmountCrafted";
        protected const string IngredientsKey = "Ingredients";
        protected const string LinkedItemsIdsKey = "LinkedItemIDs";
        protected const string ForceUnlockAtStartKey = "ForceUnlockAtStart";
        protected const string UnlocksKey = "Unlocks";

        protected readonly EmProperty<short> amountCrafted;
        protected readonly EmPropertyCollectionList<EmIngredient> ingredients;
        protected readonly EmPropertyList<string> linkedItems;
        protected readonly EmYesNo unlockedAtStart;
        protected readonly EmPropertyList<string> unlocks;

        public string ID => this.ItemID;

        public short? AmountCrafted
        {
            get
            {
                if (amountCrafted.HasValue && amountCrafted.Value > -1)
                    return amountCrafted.Value;

                return null;
            }
            set
            {
                if (amountCrafted.HasValue = value.HasValue)
                    amountCrafted.Value = value.Value;
            }
        }

        protected bool DefaultForceUnlock = false;

        public bool ForceUnlockAtStart
        {
            get
            {
                if (unlockedAtStart.HasValue)
                    return unlockedAtStart.Value;

                return DefaultForceUnlock;
            }

            set => unlockedAtStart.Value = value;
        }

        public IEnumerable<string> Unlocks => unlocks.Values;

        public int? UnlocksCount
        {
            get
            {
                if (unlocks.HasValue)
                    return unlocks.Count;

                return null;
            }
        }

        public IEnumerable<EmIngredient> Ingredients => ingredients.Values;

        public int? IngredientsCount
        {
            get
            {
                if (ingredients.HasValue)
                    return ingredients.Count;

                return null;
            }
        }

        public IEnumerable<string> LinkedItems => linkedItems.Values;

        public int? LinkedItemsCount
        {
            get
            {
                if (linkedItems.HasValue)
                    return linkedItems.Count;

                return null;
            }
        }

        public void AddIngredient(string techType, short count = 1) => ingredients.Add(new EmIngredient() { ItemID = techType, Required = count });

        public void AddIngredient(TechType techType, short count = 1) => AddIngredient(techType.ToString(), count);

        public void AddLinkedItem(string linkedItem) => linkedItems.Add(linkedItem);

        public void AddLinkedItem(TechType linkedItem) => AddLinkedItem(linkedItem.ToString());

        public void AddUnlock(string unlock) => unlocks.Add(unlock);

        protected static List<EmProperty> ModifiedRecipeProperties => new List<EmProperty>(TechTypedProperties)
        {
            new EmProperty<short>(AmountCraftedKey, 1) { Optional = true },
            new EmPropertyCollectionList<EmIngredient>(IngredientsKey, new EmIngredient()) { Optional = true },
            new EmPropertyList<string>(LinkedItemsIdsKey) { Optional = true },
            new EmYesNo(ForceUnlockAtStartKey) { Optional = true },
            new EmPropertyList<string>(UnlocksKey) { Optional = true },
        };

        internal ModifiedRecipe(TechType origTechType) : this()
        {
            ITechData origRecipe = CraftData.Get(origTechType);
            this.ItemID = origTechType.ToString();
            this.AmountCrafted = (short)origRecipe.craftAmount;

            for (int i = 0; i < origRecipe.ingredientCount; i++)
            {
                IIngredient origIngredient = origRecipe.GetIngredient(i);
                AddIngredient(origIngredient.techType.ToString(), (short)origIngredient.amount);
            }

            for (int i = 0; i < origRecipe.linkedItemCount; i++)
                linkedItems.Add(origRecipe.GetLinkedItem(i).ToString());
        }

        public ModifiedRecipe() : this("ModifiedRecipe", ModifiedRecipeProperties)
        {
        }

        protected ModifiedRecipe(string key) : this(key, ModifiedRecipeProperties)
        {
        }

        protected ModifiedRecipe(string key, ICollection<EmProperty> definitions) : base(key, definitions)
        {
            amountCrafted = (EmProperty<short>)Properties[AmountCraftedKey];
            ingredients = (EmPropertyCollectionList<EmIngredient>)Properties[IngredientsKey];
            linkedItems = (EmPropertyList<string>)Properties[LinkedItemsIdsKey];
            unlockedAtStart = (EmYesNo)Properties[ForceUnlockAtStartKey];
            unlocks = (EmPropertyList<string>)Properties[UnlocksKey];

            OnValueExtractedEvent += ValueExtracted;
        }

        private void ValueExtracted()
        {
            foreach (EmIngredient ingredient in ingredients)
            {
                string itemID = (ingredient["ItemID"] as EmProperty<string>).Value;
                short required = (ingredient["Required"] as EmProperty<short>).Value;
            }
        }

        internal override EmProperty Copy() => new ModifiedRecipe(this.Key, this.CopyDefinitions);

        public EmIngredient GetIngredient(int index) => ingredients[index];

        public string GetLinkedItem(int index) => linkedItems[index];

        public string GetUnlock(int index) => unlocks[index];

        public override bool PassesPreValidation() => base.PassesPreValidation() && InnerItemsAreValid();

        protected bool InnerItemsAreValid()
        {
            // Sanity check of the blueprints ingredients and linked items to be sure that it only contains known items
            // Modded items are okay, but they must be for mods the player already has installed
            bool internalItemsPassCheck = true;

            foreach (EmIngredient ingredient in this.Ingredients)
            {
                TechType ingredientID = GetTechType(ingredient.ItemID);

                if (ingredientID == TechType.None)
                {
                    QuickLogger.Warning($"{this.Key} entry with ID of '{this.ItemID}' contained an unknown ingredient '{ingredient.ItemID}'.  Entry will be discarded.");
                    internalItemsPassCheck = false;
                    continue;
                }

                ingredient.TechType = ingredientID;
            }

            foreach (string linkedItem in this.LinkedItems)
            {
                TechType linkedItemID = GetTechType(linkedItem);

                if (linkedItemID == TechType.None)
                {
                    QuickLogger.Warning($"{this.Key} entry with ID of '{this.ItemID}' contained an unknown linked item '{linkedItem}'. Entry will be discarded.");
                    internalItemsPassCheck = false;
                    continue;
                }
            }

            return internalItemsPassCheck;
        }

        public virtual bool SendToSMLHelper()
        {
            try
            {
                return
                    HandleModifiedRecipe() &&
                    HandleUnlocks();
            }
            catch (Exception ex)
            {
                QuickLogger.Error($"Exception thrown while handling {this.Key} entry '{this.ItemID}'{Environment.NewLine}{ex}");
                return false;
            }
        }

        protected bool HandleModifiedRecipe()
        {
            ITechData original = CraftData.Get(this.TechType, skipWarnings: true);

            if (original == null) // Possibly a mod recipe
                original = CraftDataHandler.GetModdedTechData(this.TechType);

            if (original == null)
                return false;  // Unknown recipe

            var replacement = new TechData();

            bool overrideRecipe = false;
            string changes = "";
            // Amount
            if (this.AmountCrafted.HasValue)
            {
                overrideRecipe |= true;
                changes += $" {AmountCraftedKey} ";
                replacement.craftAmount = this.AmountCrafted.Value;
            }
            else
            {
                replacement.craftAmount = original.craftAmount;
            }

            // Ingredients
            if (this.IngredientsCount.HasValue)
            {
                overrideRecipe |= true;
                changes += $" {IngredientsKey} ";
                foreach (EmIngredient ingredient in this.Ingredients)
                {
                    replacement.Ingredients.Add(
                        new Ingredient(
                            GetTechType(ingredient.ItemID),
                            ingredient.Required));
                }
            }
            else
            {
                for (int i = 0; i < original.ingredientCount; i++)
                    replacement.Ingredients.Add(
                        new Ingredient(
                        original.GetIngredient(i).techType,
                        original.GetIngredient(i).amount));
            }

            // Linked Items
            if (this.LinkedItemsCount.HasValue)
            {
                overrideRecipe |= true;
                changes += $" {LinkedItemsIdsKey}";
                foreach (string linkedItem in this.LinkedItems)
                    replacement.LinkedItems.Add(GetTechType(linkedItem));
            }
            else
            {
                for (int i = 0; i < original.linkedItemCount; i++)
                    replacement.LinkedItems.Add(original.GetLinkedItem(i));
            }

            if (overrideRecipe)
            {
                CraftDataHandler.SetTechData(this.TechType, replacement);
                QuickLogger.Message($"Modifying recipe for '{this.ItemID}' withnew values in: {changes}");
            }

            return true;
        }

        protected bool HandleUnlocks()
        {
            if (this.ForceUnlockAtStart)
            {
                KnownTechHandler.UnlockOnStart(this.TechType);
                QuickLogger.Message($"{this.Key} for '{this.ItemID}' will be a unlocked at the start of the game");
            }

            if (this.UnlocksCount.HasValue && this.UnlocksCount > 0)
            {
                var unlocks = new List<TechType>();

                foreach (string value in this.Unlocks)
                {
                    unlocks.Add(GetTechType(value));
                    QuickLogger.Message($"{this.Key} for '{value}' will be a unlocked when '{this.ItemID}' is scanned or picked up");
                }

                KnownTechHandler.SetAnalysisTechEntry(this.TechType, unlocks);
            }

            return true;
        }
    }
}
