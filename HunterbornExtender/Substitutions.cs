﻿namespace HunterbornExtender;
using System;
using System.Collections.Generic;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using static HunterbornExtender.FormKeys;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Plugins.Cache;

/// <summary>
/// Storage class for big hard-coded Dictionaries that replace one thing with another.
/// 
/// </summary>
sealed public class Substitutions
{

    /// <summary>
    /// Creates a substituter for Hunterborn to CACO items, using IItemGetters.
    /// </summary>
    /// <param name="cacoInstalled">If false, no substitution will be performed.</param>
    /// <returns>A delegate that performs item substitutions on IItemGetters.</returns>
    /// 
    static public Func<IItemGetter, IItemGetter> GetCACOSubResolved(bool cacoInstalled, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        if (cacoInstalled)
        {
            var resolved = HunterbornToCaco.ToDictionary(m => m.Key.Resolve<IItemGetter>(linkCache), m => m.Value.Resolve<IItemGetter>(linkCache));
            return item => resolved.GetValueOrDefault(item, item);
        }
        else return item => item;
    }

    /// <summary>
    /// Creates a substituter for Hunterborn to CACO items, using FormLinkGetters.
    /// </summary>
    /// <param name="cacoInstalled">If false, no substitution will be performed.</param>
    /// <returns>A delegate that performs item substitutions on FormLinkGetters.</returns>
    /// 
    static public Func<IFormLinkGetter<IItemGetter>, IFormLinkGetter<IItemGetter>> GetCACOSub(bool cacoInstalled) => item
        => cacoInstalled ? HunterbornToCaco.GetValueOrDefault(item, item) : item;

    /// <summary>
    /// Item substitutions to use when Complete Alchemy and Cooking Overhaul is installed.
    /// </summary>
    static private Dictionary<IFormLinkGetter<IItemGetter>, IFormLinkGetter<IItemGetter>> HunterbornToCaco = new()
    {
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x14795)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x190b54)) }, // _DS_Food_Raw_Bear -> CACO_FoodMeatBear
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x27783)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x190b63)) }, // _DS_Food_Raw_Chaurus -> CACO_FoodMeatChaurusMeat
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x14798)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x190b50)) }, // _DS_Food_Raw_Fox -> CACO_FoodMeatFox
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x1479a)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x49cdc7)) }, // _DS_Food_Raw_Goat -> CACO_FoodMeatGoatPortionRaw
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x1479e)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x190b56)) }, // _DS_Food_Raw_Mammoth -> CACO_FoodMeatMammoth
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x147a0)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x190b53)) }, // _DS_Food_Raw_Sabrecat -> CACO_FoodMeatSabre
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x14796)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x49cda3)) }, // _DS_Food_Raw_Skeever -> CACO_FoodMeatSkeeverRaw
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x14d24)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x190b58)) }, // _DS_Food_Raw_Slaughterfish -> CACO_FoodSeaSlaughterfishRaw
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x29847)), new FormLink<IItemGetter>(new FormKey(CACO_MODKEY, 0x48da5a)) }, // _DS_Food_Raw_Troll -> CACO_FoodMeatTroll
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x14d21)), Skyrim.Ingestible.FoodVenison }, // _DS_Food_Raw_Elk -> FoodMeatVenison
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x1479c)), Skyrim.Ingestible.FoodDogMeat }, // _DS_Food_Raw_Wolf -> FoodDogMeat
        { new FormLink<IItemGetter>(new FormKey(HUNTERBORN_MODKEY, 0x28ccfa)), new FormLink<IItemGetter>(new FormKey(Update.ModKey, 0xcca100)) }, // _DS_Food_Water -> FoodWaterToken_CACO
        { new FormLink<IItemGetter>(new FormKey(Skyrim.ModKey, 0x1016B3)), new FormLink<IItemGetter>(new FormKey(Update.ModKey, 0xcca130)) }, // HumanFlesh -> CACO_FoodMeatHumanoidFlesh
        { new FormLink<IItemGetter>(new FormKey(Skyrim.ModKey, 0x034cdf)), new FormLink<IItemGetter>(new FormKey(Update.ModKey, 0xcca101)) } // SaltPile -> FoodSaltToken_CACO
    };

    /// <summary>
    /// Translates known DeathItem editorIDs to proper names and sorting names.
    /// </summary>
    static readonly public Dictionary<string, List<string>> EditorToNames = new() {
        { "BearCave", new() {"Bear", "Cave"} },
        { "BearSnow", new() {"Bear", "Snow"} },
        { "CharusHunter", new() {"Chaurus", "Hunter"} },
        { "ElkFemale", new() {"Elk", "Female"} },
        { "ElkMale", new() {"Elk", "Male"} },
        { "FoxIce", new() {"Fox", "Snow"} },
        { "MudCrab01", new() {"MudCrab", "Small"} },
        { "MudCrab02", new() {"MudCrab", "Large"} },
        { "MudCrab03", new() {"MudCrab", "Giant"} },
        { "SabrecatSnow", new() {"Sabrecat", "Snow"} },
        { "FrostbiteSpider", new() {"Spider", "Frostbite", "Medium"} },
        { "FrostbiteSpiderGiant", new() {"Spider", "Frostbite", "Giant"} },
        { "SprigganBurnt", new() {"Spriggan", "Burnt"} },
        { "DeerVale", new() {"Deer", "Vale"} },
        { "SabrecatVale", new() {"Sabrecat", "Vale"} },
        { "WolfIce", new() {"Wolf", "Ice"} },
        { "TrollFrost", new() {"Troll", "Frost"} },
        { "Boar", new() {"Boar", "Bristleback" } }
    };

}