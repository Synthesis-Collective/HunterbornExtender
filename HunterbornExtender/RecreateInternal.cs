﻿namespace HunterbornExtender;
using DynamicData;
using HunterbornExtender.Settings;
using Microsoft.CodeAnalysis;
using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Aspects;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Exceptions;
using Mutagen.Bethesda.Plugins.Records;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using DeathItemGetter = Mutagen.Bethesda.Skyrim.ILeveledItemGetter;
using PeltSet = ValueTuple<Mutagen.Bethesda.Skyrim.IMiscItemGetter, Mutagen.Bethesda.Skyrim.IMiscItemGetter, Mutagen.Bethesda.Skyrim.IMiscItemGetter, Mutagen.Bethesda.Skyrim.IMiscItemGetter>;
using ViewingRecords = StandardRecords<Mutagen.Bethesda.Skyrim.ISkyrimModGetter, Mutagen.Bethesda.Skyrim.IFormListGetter>;

sealed public class RecreateInternal
{
    /// <summary>
    /// 
    /// </summary>
    /// <param internalName="std"></param>
    /// <returns></returns>
    static public List<InternalPluginEntry> RecreateInternalPluginsUI(ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode = false)
    {
        (var plugins, _, _, _) = RecreateInternalPlugins(linkCache, debuggingMode);
        return plugins;
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param internalName="std"></param>
    /// <returns></returns>
    static public (List<InternalPluginEntry>, OrderedDictionary<DeathItemGetter, PluginEntry>, Dictionary<PluginEntry, IMiscItemGetter>, Dictionary<PluginEntry, PeltSet>) 
        RecreateInternalPlugins(ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode = false)
    {
        OrderedDictionary<DeathItemGetter, PluginEntry> KnownDeathItems = new();
        Dictionary<PluginEntry, IMiscItemGetter> KnownCarcasses = new();
        Dictionary<PluginEntry, PeltSet> KnownPelts = new();
        List<InternalPluginEntry> plugins = new();

        var std = ViewingRecords.CreateViewingInstance(linkCache);

        foreach (EntryType type in Enum.GetValues(typeof(EntryType)))
        {
            int count = std.GetCCFor(type).RaceIndex.Data.Count;
            Write.Action(1, $"Recreating {count} {type} plugin entries.");

            if (debuggingMode)
            {
                if (type == EntryType.Animal) Console.WriteLine($"\tChecks: names={std.GetCCFor(type)._DS_FL_DeathItems.Items.Count}, pelts={std.GetCCFor(type)._DS_FL_PeltLists.Items.Count}, carcasses={std.Animals._DS_FL_CarcassObjects.Items.Count}");
                else Write.Action(1, $"Checks: names={std.GetCCFor(type)._DS_FL_DeathItems.Items.Count}, pelts={std.GetCCFor(type)._DS_FL_PeltLists.Items.Count}");
            }

            for (int index = 0; index < count; index++)
            {
                try
                {
                    InternalPluginEntry entry = RecreateCorePluginEntry(type, index, KnownDeathItems, KnownCarcasses, KnownPelts, std, linkCache, debuggingMode);
                    plugins.Add(entry);
                }
                catch (DataConsistencyError ex)
                {
                    Write.Fail(0, "WARNING: inconsistent data detected. This may be the result of some other mod patching Hunterborn.");
                    Write.Fail(0, ex.Message);
                }
            }
        }

        return (plugins, KnownDeathItems, KnownCarcasses, KnownPelts);
    }

    static private InternalPluginEntry RecreateCorePluginEntry(EntryType type, int index, OrderedDictionary<DeathItemGetter, PluginEntry> knownDeathItems, 
        Dictionary<PluginEntry, IMiscItemGetter> knownCarcasses, Dictionary<PluginEntry, PeltSet> knownPelts, ViewingRecords std, 
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode)
    {
        string internalName = std.GetCCFor(type).RaceIndex.Data[index];
        if (internalName.IsNullOrWhitespace()) throw new DataConsistencyError(type, internalName, index, "No name.");

        try
        {
            var deathItemLink = std.GetCCFor(type)._DS_FL_DeathItems.Items[index];
            if (deathItemLink.IsNull) throw new DataConsistencyError(type, internalName, index, "No DeathItem.");

            deathItemLink.TryResolve<DeathItemGetter>(linkCache, out var deathItem);
            if (deathItem == null) throw new DataConsistencyError(type, internalName, index, "DeathItem could not be resolved.");
            if (deathItem.EditorID == null) throw new DataConsistencyError(type, internalName, index, $"DeathItem {deathItem.FormKey} has no editor id.");

            InternalPluginEntry plugin = new(type, internalName, deathItem.FormKey);
            knownDeathItems.Add(deathItem, plugin);
            var renaming = RecreatePluginName(plugin, deathItem);

            if (!renaming.Equals(NoRename)) (internalName, plugin.ProperName, plugin.SortName) = renaming;
            if (debuggingMode) Write.Action(2, $"Recreating {plugin.Name} from {DeathItemNamer(deathItem)} (proper name '{plugin.ProperName}', sort name '{plugin.SortName}')");

            var toggle = std.GetCCFor(type).Switches.Objects[index].Object;
            if (toggle.IsNull) plugin.Toggle = new FormLink<IGlobalGetter>();
            else plugin.Toggle = toggle.Resolve<IGlobalGetter>(linkCache).ToLink();

            var meat = std.GetCCFor(type).MeatType.Objects[index].Object;
            if (meat.IsNull) plugin.Meat = new FormLink<IItemGetter>();
            else plugin.Meat = meat.Resolve<IItemGetter>(linkCache).ToLink();

            var shared = std.GetCCFor(type).SharedDeathItems.Objects[index].Object;
            if (shared.IsNull) plugin.SharedDeathItems = new FormLink<IFormListGetter>();
            else plugin.SharedDeathItems = shared.Resolve<IFormListGetter>(linkCache).ToLink();

            plugin.CarcassSize = std.GetCCFor(type).CarcassSizes.Data[index];

            if (type == EntryType.Monster)
            {
                var venom = std.Monsters.VenomItems.Objects[index].Object;
                if (venom.IsNull) plugin.Venom = new FormLink<IIngestibleGetter>();
                else plugin.Venom = venom.Resolve<IIngestibleGetter>(linkCache).ToLink();

                var blood = std.Monsters.BloodItems.Objects[index].Object;
                if (blood.IsNull) plugin.BloodType = new FormLink<IItemGetter>();
                else plugin.BloodType = blood.Resolve<IItemGetter>(linkCache).ToLink();

                plugin.CarcassWeight = 0;
                plugin.CarcassValue = 0;
            }
            else if (type == EntryType.Animal)
            {
                var msg = std.Animals.CarcassMessages.Objects[index].Object;
                if (msg.IsNull) plugin.CarcassMessageBox = new FormLink<IMessageGetter>();
                else plugin.CarcassMessageBox = msg.Resolve<IMessageGetter>(linkCache).ToLink();

                plugin.Venom = new FormLink<IIngestibleGetter>();
                plugin.BloodType = new FormLink<IItemGetter>();

                var carcass = std.Animals._DS_FL_CarcassObjects.Items[index].Resolve<IMiscItemGetter>(linkCache);
                plugin.CarcassWeight = (int)carcass.Weight;
                plugin.CarcassValue = (int)carcass.Value;
                knownCarcasses.Add(plugin, carcass);
            }

            plugin.Discard = type != EntryType.Monster
                ? new()
                : std.Monsters.Discards.Objects[index].Object
                .Resolve<IFormListGetter>(linkCache).Items
                .Select(item => item as IFormLinkGetter<IItemGetter>)
                .Where(item => item is not null)
                .Select(item => item!).ToList();

            var mats = std.GetCCFor(type)._DS_FL_Mats__Lists.Items[index].Resolve<IFormListGetter>(linkCache);
            plugin.Materials = RecreateMaterials(mats, linkCache);
            // Console.WriteLine($"===RECREATED MATS FOR {plugin.ProperName}: {plugin.Materials.Pretty()}");

            plugin.PeltCount = Array.Empty<int>();
            plugin.FurPlateCount = Array.Empty<int>();

            IFormListGetter peltList = std.GetCCFor(type)._DS_FL_PeltLists.Items[index].Resolve<IFormListGetter>(linkCache);
            if (peltList.Items.Count == 4)
            {
                PeltSet pelts = (
                    peltList.Items[0].Resolve<IMiscItemGetter>(linkCache),
                    peltList.Items[1].Resolve<IMiscItemGetter>(linkCache),
                    peltList.Items[2].Resolve<IMiscItemGetter>(linkCache),
                    peltList.Items[3].Resolve<IMiscItemGetter>(linkCache));
                knownPelts[plugin] = pelts;
            }

            // The voice field is unnecessary because the core voices are hard-coded.
            // But it's nice to have it just in case.
            // 
            // We could scan through all NPCs looking for the matching DeathItem and grab the voice of
            // the first match.
            //
            // BUT
            // 
            // Vanilla voices are named very predictably, so just use that.
            //
            string voiceEdid = $"Cr{plugin.Name}Voice";
            linkCache.TryResolve<IVoiceTypeGetter>(voiceEdid, out var voice);
            plugin.Voice = voice == null ? new FormLink<IVoiceTypeGetter>() : voice.ToLink();
            //if (debuggingMode) Write.Action(2, $"Internal plugin {plugin.Name} searching for voice {voiceEdid}: found {plugin.Voice}.");

            FindRecipes(plugin, internalName, deathItem, knownPelts, linkCache, debuggingMode);

            // @TODO Find jerky and charred recipes.
            return plugin;

        }
        catch (RecordException ex)
        {
            Write.Title(0, $"Problem with {type} {internalName} {ex.FormKey} {ex.EditorID}");
            Write.Fail(1, $"Problem with {type} {internalName} {ex.FormKey} {ex.EditorID}");
            Write.Fail(1, ex.Message);
            Console.WriteLine(ex.StackTrace);
            throw ex;
        }
    }

    /// <summary>
    /// Fill in recipe-related data for the internal plugins.
    /// 
    /// </summary>
    /// 
    static private void FindRecipes(PluginEntry plugin, string internalName, DeathItemGetter deathItem, Dictionary<PluginEntry, PeltSet> knownPelts, 
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode)
    {
        // Search for the standard recipes using naming conventions in the order of
        // CACO->CCOR->Campfire->Hunterborn->Vanilla.
        // If nothing is found, try again using the plugin name instead of the internal name.

        Dictionary<RecipeType, List<string>> patterns = new() {
            { RecipeType.PeltPoor, new() { "_DS_Recipe_Pelt_{0}_00" } },
            { RecipeType.PeltStandard, new() { "_DS_Recipe_Pelt_{0}_01", "RecipeLeather{0}Hide" } },
            { RecipeType.PeltFine, new() { "_DS_Recipe_Pelt_{0}_02" } },
            { RecipeType.PeltFlawless, new() { "_DS_Recipe_Pelt_{0}_03" } },
            { RecipeType.FurPoor, new() { "HB_Recipe_FurPlate_{0}_00" } },
            { RecipeType.FurStandard, new() { "CCOR_RecipeFurPlate{0}Hide", "_Camp_RecipeTanningLeather{0}Hide", "HB_Recipe_FurPlate_{0}_01" } },
            { RecipeType.FurFine, new() { "HB_Recipe_FurPlate_{0}_02" } },
            { RecipeType.MeatCooked, new() { "CACO_RecipeFood{0}Cooked", "CACO_RecipeFoodMeat{0}Cooked", "_DS_Recipe_Food_CharredMeat_{0}", "_DS_Recipe_Food_Seared{0}" } },
            { RecipeType.MeatCampfire, new() { "CACO_RecipeFood{0}Cooked_Campfire", "HB_Recipe_FireFood_CharredMeat_{0}", "HB_Recipe_FireFood_Seared{0}" } },
            { RecipeType.MeatPrimitive, new() { "HB_CACO_RecipeFood{0}Cooked_PrimCook", "_DS_Recipe_Food_Primitive_CharredMeat_{0}", "_DS_Recipe_Food_Primitive_Seared{0}" } },
            { RecipeType.MeatJerky, new() { "CACO_RecipeJerky{0}", "_DS_Food_{0}Jerky", "_DS_Recipe_Food_{0}Jerky"} } };

        // Some corrections for vanilla and hunterborn recipes with non-standard names.
        List<string> names = new() { internalName, plugin.Name };
        if (plugin.Name.EqualsIgnoreCase("Cow")) names.Add("Beef");
        if (plugin.Name.EqualsIgnoreCase("Deer")) names.Add("Venison");
        if (plugin.Name.ContainsInsensitive("Elk")) names.Add("Venison");
        if (plugin.Name.EqualsIgnoreCase("Dog")) names.Add("DogCookedWhole");
        if (plugin.Name.ContainsInsensitive("Mudcrab")) names.Add("Mudcrab");
        if (plugin.Name.ContainsInsensitive("Bristleback")) names.Add("Boar");
        if (plugin.Name.ContainsInsensitive("FoxIce")) names.Insert(0, "FoxSnow");
        if (plugin.Name.ContainsInsensitive("WolfIce")) names.Insert(0, "IceWolf");
        if (plugin.Name.ContainsInsensitive("DeerVale")) names.Insert(0, "ValeDeer");
        if (plugin.Name.ContainsInsensitive("SabrecatVale")) names.Insert(0, "ValeSabrecat");

        var recipes = Edid_Lookups_Fallbacks(names, patterns, linkCache, false || internalName.ContainsInsensitive("fox"));//debuggingMode);

        // Extract the results to nicely named variables.
        var peltRecipe0 = recipes.GetValueOrDefault(RecipeType.PeltPoor);
        var peltRecipe1 = recipes.GetValueOrDefault(RecipeType.PeltStandard);
        var peltRecipe2 = recipes.GetValueOrDefault(RecipeType.PeltFine);
        var peltRecipe3 = recipes.GetValueOrDefault(RecipeType.PeltFlawless);
        var furRecipe0 = recipes.GetValueOrDefault(RecipeType.FurPoor);
        var furRecipe1 = recipes.GetValueOrDefault(RecipeType.PeltStandard);
        var furRecipe2 = recipes.GetValueOrDefault(RecipeType.PeltFine);
        var meatCooked = recipes.GetValueOrDefault(RecipeType.MeatCooked);
        var meatCampfire = recipes.GetValueOrDefault(RecipeType.MeatCampfire);
        var meatPrimitive = recipes.GetValueOrDefault(RecipeType.MeatPrimitive);
        var meatJerky = recipes.GetValueOrDefault(RecipeType.MeatJerky);

        // If a standard pelt recipe is found, there must be a default pelt.
        // Try to get it. Use the result of the GetDefaultPelt function otherwise, which 
        // searches the creature's inventory.
        if (peltRecipe1 is not null && peltRecipe1.Items is IReadOnlyList<IContainerEntryGetter> containerEntries
            && containerEntries.Count > 0 && containerEntries[0] is IContainerEntryGetter containerEntry
            && containerEntry.Item is IContainerItemGetter containerItem
            && containerItem.Item is IFormLink<IItemGetter> foundPelt)
        {
            if (foundPelt is not null && !foundPelt.IsNull)
            {
                if (debuggingMode) Write.Success(2, $"Found default pelt from tanning recipe {peltRecipe1.EditorID}.");
                plugin.DefaultPelt = foundPelt.FormKey.ToLink<IMiscItemGetter>();
            }
        }
        else
        {
            var defaultPelt = FindDefaultPelt(deathItem, linkCache, debuggingMode);
            if (defaultPelt is not null)
            {
                if (debuggingMode) Write.Success(2, $"Found default pelt from DeathItem {deathItem.EditorID}.");
                plugin.DefaultPelt = defaultPelt.ToLink();
            }
        }

        plugin.Recipes = recipes;

        // Pack it all up and finish filling in the Plugin's properties.
        // Print debugging messages about what was found.

        if (peltRecipe0 is not null && peltRecipe1 is not null && peltRecipe2 is not null && peltRecipe3 is not null)
        {
            if (debuggingMode) Write.Success(2, "Found a full set of leather-making recipes.");
            //var peltRecipeSet = (peltRecipe0, peltRecipe1, peltRecipe2, peltRecipe3);
            //plugin.Recipes.PeltRecipes = peltRecipeSet;

            plugin.PeltCount = new int[] { peltRecipe0.CreatedObjectCount ?? 2, peltRecipe1.CreatedObjectCount ?? 2, peltRecipe2.CreatedObjectCount ?? 2, peltRecipe3.CreatedObjectCount ?? 2 };

            IMiscItemGetter? pelt0 = null;
            IMiscItemGetter? pelt1 = null;
            IMiscItemGetter? pelt2 = null;
            IMiscItemGetter? pelt3 = null;

            peltRecipe0.Items?[0].Item.Item.TryResolve<IMiscItemGetter>(linkCache, out pelt0);
            peltRecipe1.Items?[0].Item.Item.TryResolve<IMiscItemGetter>(linkCache, out pelt1);
            peltRecipe2.Items?[0].Item.Item.TryResolve<IMiscItemGetter>(linkCache, out pelt2);
            peltRecipe3.Items?[0].Item.Item.TryResolve<IMiscItemGetter>(linkCache, out pelt3);

            if (pelt0 is not null && pelt1 is not null && pelt2 is not null && pelt3 is not null)
            {
                PeltSet found = (pelt0, pelt1, pelt2, pelt3);

                if (knownPelts.ContainsKey(plugin))
                {
                    var known = knownPelts[plugin];
                    if (found.Equals(known)) Write.Success(2, "Recipe pelts and name-lookup pelts are a match.");
                    else
                    {
                        Write.Fail(2, "Recipe pelts and deathItem name-match pelts do not match, which is weird but not disastrous.");
                        if (debuggingMode) 
                        {
                            Write.Fail(3, $"Recipe pelts: {found}");
                            Write.Fail(3, $"Name-match pelts: {known}");
                            Write.Fail(3, $"Recipe: {known.Item1.FormKey,-20} Name: ${found.Item1.FormKey,-20} - {known.Item1.FormKey.Equals(found.Item1.FormKey)}");
                            Write.Fail(3, $"Recipe: {known.Item2.FormKey,-20} Name: ${found.Item2.FormKey,-20} - {known.Item2.FormKey.Equals(found.Item2.FormKey)}");
                            Write.Fail(3, $"Recipe: {known.Item3.FormKey,-20} Name: ${found.Item3.FormKey,-20} - {known.Item3.FormKey.Equals(found.Item3.FormKey)}");
                            Write.Fail(3, $"Recipe: {known.Item4.FormKey,-20} Name: ${found.Item4.FormKey,-20} - {known.Item4.FormKey.Equals(found.Item4.FormKey)}");
                        }
                    }
                }
                else
                {
                    knownPelts[plugin] = (pelt0, pelt1, pelt2, pelt3);
                    Write.Fail(2, "HOW CAN THIS EVEN HAPPEN?");
                }
            }

        }
        else if (peltRecipe0 is not null || peltRecipe1 is not null || peltRecipe2 is not null || peltRecipe3 is not null)
        {
            if (debuggingMode) Write.Fail(2, "Found inconsistent set of leather-making recipes.");
        }

        if (furRecipe0 is not null && furRecipe1 is not null && furRecipe2 is not null)
        {
            if (debuggingMode) Write.Success(2, "Found a full set of fur-plating recipes.");
            plugin.FurPlateCount = new int[] { furRecipe0.CreatedObjectCount ?? 1, furRecipe1.CreatedObjectCount ?? 2, furRecipe2.CreatedObjectCount ?? 4 };
        }
        else if (furRecipe0 is not null || furRecipe1 is not null || furRecipe2 is not null)
        {
            if (debuggingMode) Write.Fail(2, "Found inconsistent set of fur-plating recipes.");
        }

        if (debuggingMode)
        {
            if (!plugin.DefaultPelt.IsNull) Write.Success(2, $"Found standard pelt: {ItemNamer(plugin.DefaultPelt.Resolve(linkCache))}");
            else if (plugin.PeltCount.Length > 0) Write.Fail(2, $"No pelt found but pelt counts are specified.");
        }

        if (meatCooked is not null || meatCampfire is not null || meatPrimitive is not null || meatJerky is not null)
        {
            var meatRecipes = (meatCooked, meatCooked, meatPrimitive, meatJerky);
            if (debuggingMode) Write.Success(2, $"Found meat recipes: {meatRecipes}");
        }
        else if (!plugin.Meat.IsNull && debuggingMode) Write.Fail(2, $"No meat recipes found.");

    }

    static private Dictionary<T, IConstructibleObjectGetter> Edid_Lookups_Fallbacks<T>(List<string> names, Dictionary<T, List<string>> patterns, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode) where T : Enum
    {
        Dictionary<T, IConstructibleObjectGetter> results = new();

        foreach (var group in patterns)
        {
            IConstructibleObjectGetter? result = null;
            foreach (var name in names)
            {
                foreach (var pattern in group.Value)
                {
                    string editorID = string.Format(pattern, name);
                    linkCache.TryResolve<IConstructibleObjectGetter>(editorID, out result);
                    if (debuggingMode) 
                        if (result is not null) Write.Success(1, $"Found {group.Key,-14}: {editorID}");
                        else Write.Fail(1, $"Miss  {group.Key,-14}: {editorID}");
                    if (result is not null) break;
                }
                if (result is not null) break;
            }

            if (result is not null) results.Add(group.Key, result);
        }

        return results;
    }

    /// <summary>
    /// Attempts to recreate the useful names for the plugin.
    /// </summary>
    /// <returns>A tuple of (deathItemName, ProperName, SortName)</returns>
    static private (string, string, string) RecreatePluginName(PluginEntry plugin, DeathItemGetter deathItem)
    {
        if (deathItem.EditorID is string deathItemEdid && DeathItemPrefix.IsMatch(deathItemEdid))
        {
            string deathItemName = DeathItemPrefix.Replace(deathItemEdid, "");
            if (SpecialCases.EditorToNames.ContainsKey(deathItemName) && SpecialCases.EditorToNames[deathItemName].Count > 0)
            {
                var parts = SpecialCases.EditorToNames[deathItemName];

                if (parts.Count == 1) return (deathItemName, parts[0], parts[0]);
                else if (parts.Count == 2) return (deathItemName, $"{parts[1]} {parts[0]}", $"{parts[0]} - {parts[1]}");
                else if (parts.Count == 3) return (deathItemName, $"{parts[2]} {parts[1]} {parts[0]}", $"{parts[0]} - {parts[1]}, {parts[2]}");
            }
            else if (!deathItemName.EqualsIgnoreCase(plugin.Name))
            {
                string subtype = deathItemName.Replace(plugin.Name, "", StringComparison.InvariantCultureIgnoreCase);
                if (!subtype.IsNullOrWhitespace())
                    return (deathItemName, TextInfo.ToTitleCase($"{subtype} {plugin.Name}"), TextInfo.ToTitleCase($"{plugin.Name} - {subtype}"));
            }
        }

        return NoRename;
    }

    /// <summary>
    /// Turns a FormList of LeveledItems into a list of MaterialLevels.
    /// The inverse of CreateMaterials.
    /// </summary>
    /// 
    private static List<MaterialLevel> RecreateMaterials(IFormListGetter matsFormList, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        List<MaterialLevel> materials = new();

        foreach (var level in matsFormList.Items)
        {
            var asLeveled = level.FormKey.ToLinkGetter<ILeveledItemGetter>().Resolve(linkCache);

            MaterialLevel skillLevel = new();
            materials.Add(skillLevel);

            var entries = asLeveled.Entries;
            if (entries is not null)
            {
                foreach (var entry in entries)
                {
                    if (entry.Data is not null && entry.Data.Reference is not null)
                        skillLevel.Items[entry.Data.Reference] = entry.Data.Count;
                }
            }
        }

        return materials;
    }

    /// <summary>
    /// Checks the creature's DeathItem for anything whose internalName or editorID contains a string-match
    /// that indicates it's probably a pelt. Returns the pelt.
    /// 
    /// If no default pelt was found, one will be created and added to the patch.
    /// 
    /// </summary>
    /// 
    static public IMiscItemGetter? FindDefaultPelt(DeathItemGetter deathItem, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode)
    {
        var entries = deathItem.Entries;
        if (entries == null) return null;

        foreach (var entry in entries)
        {
            var entryItem = entry.Data?.Reference.TryResolve(linkCache);
            var edid = entryItem?.EditorID ?? "";
            if (entryItem == null || edid == null) continue;

            if (DefaultPeltRegex.Matches(edid).Any())
            {
                if (entryItem is DeathItemGetter lvld)
                {
                    if (debuggingMode) Write.Action(3, $"Pelt search recursing into {DeathItemNamer(lvld)}");
                    if (lvld.Entries is not null && lvld.Entries.Count == 1 && FindDefaultPelt(lvld, linkCache, debuggingMode) is IMiscItemGetter subItem)
                        return subItem;
                }
                else if (entryItem is IMiscItemGetter item)
                {
                    if (debuggingMode) Write.Action(3, $"Pelt search found {ItemNamer(item)} in {DeathItemNamer(deathItem)}");
                    return item;
                }
            }
        }

        return null;
    }

    static private string DeathItemNamer(DeathItemGetter deathItem)
        => deathItem.EditorID ?? deathItem.ToStandardizedIdentifier().ToString() ?? deathItem.FormKey.ToString();

    static private string ItemNamer(IItemGetter item)
        => (item is INamedGetter named ? named.Name?.ToString() : null) ?? item.EditorID ?? item.ToStandardizedIdentifier().ToString() ?? item.FormKey.ToString();

    /// <summary>
    /// Flag object indicating that RecreatePluginName did not have a result.
    /// </summary>
    static private (string, string, string) NoRename = ("", "", "");

    /// <summary>
    /// Regular expression used to turn the names of vanilla DeathItems into useful names.
    /// </summary>
    static private readonly Regex DeathItemPrefix = new(".*DeathItem", RegexOptions.IgnoreCase);

    /// <summary>
    /// Used to make nice names.
    /// </summary>
    static private readonly TextInfo TextInfo = CultureInfo.CurrentCulture.TextInfo;

    /// <summary>
    /// Matcher for pre-existing pelts items.
    /// </summary>
    static readonly private Regex DefaultPeltRegex = new("Pelt|Hide|Skin|Fur|Wool|Leather", RegexOptions.IgnoreCase);

}
