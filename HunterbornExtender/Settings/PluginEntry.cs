﻿namespace HunterbornExtender.Settings;

using System.Collections.Generic;
using Newtonsoft.Json;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins;
using Noggog;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda;

public class PluginEntry
{
    static readonly public PluginEntry SKIP = Skip.Instance;
    public EntryType Type { get; set; } = EntryType.Animal;
    public string Name { get; set; } = "Critter";
    public string ProperName { get; set; } = "Critter";
    public string SortName { get; set; } = "Critter";
    public IFormLinkGetter<IGlobalGetter> Toggle { get; set; } = new FormLink<IGlobalGetter>();
    public IFormLinkGetter<IMessageGetter> CarcassMessageBox { get; set; } = new FormLink<IMessageGetter>();
    public IFormLinkGetter<IItemGetter> Meat { get; set; } = new FormLink<IItemGetter>();
    public int CarcassSize { get; set; } = 1;
    public int CarcassWeight { get; set; } = 10;
    public int CarcassValue { get; set; } = 10;
    public int[] PeltCount { get; set; } = new int[] { 2, 2, 2, 2 };
    public int[] FurPlateCount { get; set; } = new int[] { 1, 2, 4, 8 };
    public List<MaterialLevel> Materials { get; set; } = new();
    public List<IFormLinkGetter<IItemGetter>> Discard { get; set; } = new();
    public IFormLinkGetter<IFormListGetter> SharedDeathItems { get; set; } = new FormLink<IFormListGetter>();
    public IFormLinkGetter<IItemGetter> BloodType { get; set; } = new FormLink<IItemGetter>();
    public IFormLinkGetter<IItemGetter> Venom { get; set; } = new FormLink<IItemGetter>();
    public IFormLinkGetter<IVoiceTypeGetter> Voice { get; set; } = new FormLink<IVoiceTypeGetter>();

    /// <summary>
    /// Not added to plugins yet. Remove [JsonIgnore] once this functionality is implemented.
    /// </summary>
    [JsonIgnore] public IFormLinkGetter<IMiscItemGetter> DefaultPelt { get; set; } = new FormLink<IMiscItemGetter>();

    /// <summary>
    /// Stores the recipes for meat, pelts, and furs.
    /// </summary>
    [JsonIgnore] public Dictionary<RecipeType, IConstructibleObjectGetter> Recipes { get; set; } = new();

    /// <summary>
    /// Not added to plugins yet. Remove [JsonIgnore] once this functionality is implemented.
    /// </summary>
    [JsonIgnore] public bool CreateDefaultMeat { get; set; } = false;

    /// <summary>
    /// Not added to plugins yet. Remove [JsonIgnore] once this functionality is implemented.
    /// </summary>
    [JsonIgnore] public bool CreateDefaultPelt { get; set; } = true;

    /// <summary>
    /// Not added to plugins yet. Remove [JsonIgnore] once this functionality is implemented.
    /// </summary>
    //[JsonIgnore] public LeatherType LeatherRecipeType { get; set; } = LeatherType.NORMAL;

    /// <summary>
    /// Data for heuristics. Never persist this.
    /// </summary>
    [JsonIgnore] public HashSet<string> Tokens { get; set; } = new();

    public PluginEntry() // Json import appears to require a parameterless default constructor
    {

    }

    public PluginEntry(EntryType type, string name)
    {
        Type = type;
        Name = name;
        ProperName = name;
        SortName = name;
    }

    override public string ToString()
    {
        if (!ProperName.IsNullOrWhitespace()) return ProperName;
        if (!SortName.IsNullOrWhitespace()) return SortName;
        return Name;
    }

    public bool HasRequiredMods(ILoadOrderGetter<IModListingGetter<ISkyrimModGetter>> loadOrder)
    {
        return RequiredMods().All(mod => loadOrder.ModExists(mod));
    }
    
    public List<ModKey> RequiredMods()
    {
        HashSet<ModKey> mods = new()
        {
            Toggle.FormKey.ModKey,
            CarcassMessageBox.FormKey.ModKey,
            Meat.FormKey.ModKey,
            SharedDeathItems.FormKey.ModKey,
            BloodType.FormKey.ModKey,
            Venom.FormKey.ModKey,
            Voice.FormKey.ModKey,
            DefaultPelt.FormKey.ModKey
        };

        Recipes.Values.Select(r => r.FormKey.ModKey).ForEach(mod => mods.Add(mod));
        Materials.SelectMany(level => level.Items).Select(mat => mat.Item.FormKey.ModKey).Select(mod => mods.Add(mod));
        return mods.Where(x => !x.IsNull).ToList();
    }
}

/// <summary>
/// The skip plugin.
/// 
/// </summary>
class Skip : PluginEntry
{
    static internal Skip Instance { get { return instance; } }

    static readonly private Skip instance = new();


    private Skip() : base(EntryType.Animal, "__Skip__") { }
    public override bool Equals(object? obj) => obj is Skip;
    public override int GetHashCode() => "SKIP PLUGIN".GetHashCode();
}

/// <summary>
/// Used to describe plugins that get loaded from JSon files.
/// They each have a name and can have required mods.
/// 
/// </summary>
public class AddonPluginEntry : PluginEntry
{
    public AddonPluginEntry() { }

    public AddonPluginEntry(EntryType type, string name) : base(type, name) { }

}

/// <summary>
/// Used to describe the hard-coded plugins from Hunterborn.esp.
/// They each have a KnownDeathItem used as a prototype.
/// </summary>
public class InternalPluginEntry : PluginEntry
{
    public FormKey KnownDeathItem { get; set; } = new();

    public InternalPluginEntry() { }

    public InternalPluginEntry(EntryType type, string name, FormKey deathItem) : base(type, name) { 
        KnownDeathItem = deathItem;
    }

}

/// <summary>
/// Describes the amounts of materials that can be harvested from a creature at a single skill level.
/// </summary>
public class MaterialLevel
{
    public List<MaterialItem> Items { get; set; } = new();

    public void Add(IFormLinkGetter<IItemGetter> item, int count)
    {
        Items.Add(new() { Item = item, Count = count });
    }

    public Dictionary<IFormLinkGetter<IItemGetter>, int> ToDict()
    {
        Dictionary<IFormLinkGetter<IItemGetter>, int> dict = new();
        foreach (var item in Items) dict[item.Item] = item.Count;
        return dict;
    }

    static public MaterialLevel FromDict(Dictionary<IFormLinkGetter<IItemGetter>, int> dict)
    {
        MaterialLevel result = new();
        foreach (var item in dict)
        {
            result.Add(item.Key, item.Value);
        }
        return result;
    }
}

public class MaterialItem
{
    public IFormLinkGetter<IItemGetter> Item { get; set; } = FormLinkGetter<IItemGetter>.Null;
    public int Count { get; set; } = 1;
}

/// <summary>
/// Enumerates recipe types used to recreate the internal plugins.
/// </summary>
public enum RecipeType { PeltPoor, PeltStandard, PeltFine, PeltFlawless, FurPoor, FurStandard, FurFine, MeatCooked, MeatCampfire, MeatPrimitive, MeatJerky };

/// <summary>
/// If CCOR is installed, this will control what kind of leather is produced by pelt recipes.
/// </summary>
//public enum LeatherType { NORMAL, LIGHT, DARK };

