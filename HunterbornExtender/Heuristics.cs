﻿namespace HunterbornExtender;
using HunterbornExtender.Settings;
using Microsoft.CodeAnalysis;
using Mutagen.Bethesda;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using System;
using System.Collections.Generic;
using System.Linq;
using DeathItemGetter = Mutagen.Bethesda.Skyrim.ILeveledItemGetter;

sealed public class Heuristics
{
    /// <summary>
    /// Scans a list of NPCs and tries to assign a PluginEntry to each DeathItem that belongs to a creature.
    /// </summary>
    /// 
    /// <param name="plugins">The list of plugins to match with DeathItems.</param>
    /// <param name="npcs">The npcs to process.</param>
    /// <param name="previousSelections">The previous selections, so that user choices can persist from run to run.</param>
    /// <returns></returns>
    /// <exception cref="HeuristicsError">Indicates that something went wrong during recreation. Using the InnerException field to retrieve the cause.</exception>
    /// 
    static public DeathItemSelection[] MakeHeuristicSelections(IEnumerable<INpcGetter> npcs, List<PluginEntry> plugins, DeathItemSelection[] previousSelections, 
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode = false)
    {
        try
        {
            // 
            // Import allowed and forbidden values from plugins.
            //
            foreach (var plugin in plugins)
            {
                if (!plugin.Voice.IsNull) SpecialCases.Lists.AllowedVoices.Add(plugin.Voice);
            }

            // For each DeathItem, there will be a weighted set of plausible Plugins.
            // HeuristicMatcher assigns the weights.
            Dictionary<DeathItemSelection, Dictionary<PluginEntry, double>> selectionWeights = new();
            Dictionary<DeathItemGetter, DeathItemSelection> indexer = new();

            // Tokenize the names of the plugins.
            foreach (var plugin in plugins) plugin.Tokens = Tokenizer.Tokenize(plugin.Name, plugin.SortName, plugin.ProperName);
            if (debuggingMode)
            {
                Write.Title(1, "Tokenizing plugin names.");
                plugins.ForEach(p => Write.Action(2, $"Plugin: {p.Name} -> {p.Tokens.Pretty()}"));
                Write.Title(1, "Analyzing NPCs.");
            }

            // Scan the list of npcs.
            foreach (var npc in npcs.Where(n => IsValidCreature(n, linkCache, debuggingMode)))
            {
                GetDeathItem(npc, linkCache).TryResolve(linkCache, out var deathItem);
                if (deathItem is null) continue;

                // If there is no DeathItemSelection record for the NPC's DeathItem, create it.
                // Try as hard as possible to give the DeathItemSelection a internalName. Fallbacks on fallbacks.
                if (!indexer.ContainsKey(deathItem))
                {
                    indexer[deathItem] = new DeathItemSelection() { DeathItem = deathItem.FormKey, CreatureEntryName = Naming.DeathItemFB(deathItem) };
                    selectionWeights[indexer[deathItem]] = new();
                }

                // Add the NPC to the assigned NPCs of the DeathItemSelection.
                var deathItemSelection = indexer[deathItem];
                deathItemSelection.AssignedNPCs.Add(npc);

                // Run the heuristic matcher.
                var npcWeights = HeuristicNpcMatcher(npc, plugins, linkCache, debuggingMode);
                var itemWeights = selectionWeights[deathItemSelection];

                foreach (PluginEntry plugin in npcWeights.Keys)
                    itemWeights[plugin] = itemWeights.GetValueOrDefault(plugin, 0.0) + npcWeights[plugin];
            }

            DeathItemSelection[] selections = selectionWeights.Keys.ToArray();
            Dictionary<FormKey, PluginEntry> savedSelections = previousSelections.ToDictionary(v => v.DeathItem, v => v.Selection ?? PluginEntry.SKIP);

            foreach (var selection in selections)
            {
                if (savedSelections.ContainsKey(selection.DeathItem))
                {
                    selection.Selection = savedSelections[selection.DeathItem];
                    if (debuggingMode) Write.Action(3, $"Previously selected {selection.Selection?.ProperName}.");
                }
                else
                {
                    var itemWeights = selectionWeights[selection];
                    List<PluginEntry> options = new(itemWeights.Keys);
                    if (options.Count == 0) continue;

                    options.Sort((a, b) => itemWeights[b].CompareTo(itemWeights[a]));
                    selection.Selection = options.First();

                    if (debuggingMode && !selection.DeathItem.IsNull)
                    {
                        selection.DeathItem.ToLink<DeathItemGetter>().TryResolve(linkCache, out var deathItem);
                        Write.Action(2, $"{deathItem?.EditorID ?? deathItem?.ToString() ?? "NO DEATH ITEM"}: heuristic selected {selection.Selection?.SortName}.");
                        Write.Action(3, $"From: {itemWeights.Pretty()}");
                        Write.Action(2, $"Archetypes: ");
                        foreach (var npc in selection.AssignedNPCs.Take(6)) Write.Action(3, Naming.NpcFB(npc));
                    }
                }
            }

            return selections;
        }
        catch (Exception ex)
        {
            throw new HeuristicsError(ex);
        }
    }

    /// <summary>
    /// 
    /// </summary>
    /// <returns></returns>
    static private Dictionary<PluginEntry, double> HeuristicNpcMatcher(INpcGetter npc, List<PluginEntry> plugins, 
        ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode = false)
    {
        Dictionary<PluginEntry, double> candidates = new();
        string description = Naming.NpcFB(npc);

        var clicker = DictionaryIncrementer(candidates);

        // Try to match the voice.
        var voice = GetVoice(npc, linkCache);
        if (!voice.IsNull)
        {
            foreach (var plugin in plugins)
            {
                if (plugin.Voice.Equals(voice)) clicker(10.0)(plugin);
            }
        }

        // Match the creature's editorId, internalName, and race internalName to the names of plugins.
        var nameMatches = new HashSet<PluginEntry>();
        
        GetRace(npc, linkCache).TryResolve(linkCache, out var race);
        GetDeathItem(npc, linkCache).TryResolve(linkCache, out var deathItem);

        if (npc.EditorID is string npcEditorId) plugins.Where(PluginNameMatch(npcEditorId)).ForEach(clicker(1));
        if (npc.Name?.ToString() is string npcName) plugins.Where(PluginNameMatch(npcName)).ForEach(clicker(1));
        if (race?.EditorID is string raceEditorId) plugins.Where(PluginNameMatch(raceEditorId)).ForEach(clicker(1));
        if (race?.Name?.ToString() is string raceName) plugins.Where(PluginNameMatch(raceName)).ForEach(clicker(1));

        var deathItemEdid = SpecialCases.DeathItemPrefix.Replace(deathItem?.EditorID ?? "", "");
        var raceEdid = SpecialCases.RacePostfix.Replace(race?.EditorID ?? "", "");

        var tokens = new HashSet<string?>() { npc.Name?.ToString(), npc.EditorID, race?.Name?.ToString(), raceEdid, deathItemEdid };

        if (debuggingMode) Write.Action(3, $"Initial tokens for {description}: {tokens.Pretty()}");

        // Try this tokenizing matcher to break ties.
        var npcTokens = Tokenizer.Tokenize(tokens);
        if (debuggingMode) Write.Action(3, $"Tokenized tokens for {description}: {npcTokens.Pretty()}");

        foreach (var token in npcTokens.ToArray())
        {
            if (token is not null) SpecialCases.Synonyms.Where(syns => syns.Contains(token)).ForEach(syns => npcTokens.Add(syns));
        }

        if (debuggingMode) Write.Action(3, $"Expanded tokens for {description}: {npcTokens.Pretty()}");

        foreach (var plugin in plugins)
        {
            double intersection = 2.0 * (double) plugin.Tokens.Intersect(npcTokens).Count();
            double surplus = 1.0 + (double) plugin.Tokens.Except(npcTokens).Count();
            double tokenScore = Math.Pow(intersection / surplus, 1.5);
            if (intersection > 0) clicker(intersection / surplus)(plugin);
        }

        // @TODO Add matching for distinctive keywords?
        // @TODO Add exclusion terms?

        if (debuggingMode)
        {
            Write.Action(2, $"Tokens for {description}: {npcTokens.Pretty()}");
            Write.Success(2, $"Candidates for {description}: {candidates.Pretty()}");
        }

        return candidates;
    }


    static public bool IsValidCreature(INpcGetter npc, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache, bool debuggingMode = false)
    {
        PassBlockLists passBlock = new(linkCache, debuggingMode);

        var deathItem = npc.DeathItem;
        var edid = npc.EditorID;       

        if (edid is not null && PassBlockLists.HasForbiddenEditorId(edid))
        {
            if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- forbidden editorId {edid}");
            return false;
        } 
        else if (deathItem is null)
        {
            //if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- no DeathItem");
            return false;
        }
        else if (passBlock.HasForbiddenDeathItem(npc))
        {
            if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- forbidden DeathItem {Naming.DeathItemFB(deathItem.Resolve(linkCache))}");
            return false;
        }
        else if (passBlock.HasForbiddenKeyword(npc))
        {
            if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- forbidden Keyword {npc.Keywords.Pretty()}");
            return false;
        }
        else if (passBlock.HasForbiddenFaction(npc))
        {
            if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- forbidden Faction {npc.Factions.Select(f => f.Faction.Resolve(linkCache)).Pretty()}");
            return false;
        }
        else if (!passBlock.HasAllowedVoice(npc))
        {
            if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- voice not allowed ({npc.Voice})");
            return false;
        }
        else if (PassBlockLists.HasForbiddenFlag(npc))
        {
            if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- forbidden flag {npc.Configuration.Flags.Pretty()}");
            return false;
        }
        else if (npc.ActorEffect?.Contains(Skyrim.Spell.GhostAbility) ?? false)
        {
            if (debuggingMode) Write.Fail(2, $"Skipping {npc.EditorID} -- forbidden NO GHOSTS");
            return false;
        }
        else return true;
    }

    record PassBlockLists(ILinkCache<ISkyrimMod, ISkyrimModGetter> LinkCache, bool DebuggingMode)
    {
        internal static bool HasForbiddenEditorId(string editorId) => SpecialCases.Lists.ForbiddenNpcEditorIds.Any(edid => edid.EqualsIgnoreCase(editorId));

        internal bool HasForbiddenFaction(INpcGetter npc)
        {
            if (npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Factions))
            {
                npc.Template.TryResolve(LinkCache, out var spawner);
                if (spawner is not null && spawner is INpcGetter template) return HasForbiddenFaction(template);
            }
            return npc.Factions.Any(placement => SpecialCases.Lists.ForbiddenFactions.Contains(placement.Faction));
        }

        internal bool HasForbiddenKeyword(INpcGetter npc)
        {
            if (npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Keywords))
            {
                npc.Template.TryResolve(LinkCache, out var spawner);
                if (spawner is not null && spawner is INpcGetter template) return HasForbiddenKeyword(template);
            }
            return npc.Keywords?.Any(keyword => SpecialCases.Lists.ForbiddenKeywords.Contains(keyword)) ?? false;
        }

        internal bool HasAllowedVoice(INpcGetter npc)
        {
            var voice = GetVoice(npc, LinkCache);
            return voice.IsNull || SpecialCases.Lists.AllowedVoices.Contains(voice);
        }

        internal bool HasForbiddenDeathItem(INpcGetter npc)
        {
            if (npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
            {
                npc.Template.TryResolve(LinkCache, out var spawner);
                if (spawner is not null && spawner is INpcGetter template) return HasForbiddenDeathItem(template);
            }
            return npc.DeathItem is DeathItemGetter deathItem && SpecialCases.Lists.ForbiddenDeathItems.Contains(deathItem);
        }

        internal static bool HasForbiddenFlag(INpcGetter npc) => (SpecialCases.Lists.ForbiddenFlags & npc.Configuration.Flags) != 0;

    }

    static private IFormLinkGetter<IRaceGetter> GetRace(INpcGetter npc, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        if (npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
        {
            npc.Template.TryResolve(linkCache, out var templateLink);
            if (templateLink is not null && templateLink is INpcGetter templateNpc) return GetRace(templateNpc, linkCache);
        }

        return npc.Race;
    }

    static private IFormLinkNullableGetter<DeathItemGetter> GetDeathItem(INpcGetter npc, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        if (npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
        {
            npc.Template.TryResolve(linkCache, out var templateLink);
            if (templateLink is not null && templateLink is INpcGetter templateNpc) return GetDeathItem(templateNpc, linkCache);
        }

        return npc.DeathItem;
    }

    static private IFormLinkNullableGetter<IVoiceTypeGetter> GetVoice(INpcGetter npc, ILinkCache<ISkyrimMod, ISkyrimModGetter> linkCache)
    {
        var name = Naming.NpcFB(npc) + (npc.EditorID ?? "");
        if (name.ContainsInsensitive("boar"))
        {
            var x = (npc.Sound is INpcInheritSoundGetter y && !y.InheritsSoundsFrom.IsNull)
                ? y.InheritsSoundsFrom.Resolve(linkCache) : null;
            var z = (npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
                ? npc.Template.Resolve(linkCache) : null;
            Write.Action(2, $"Getting voice for {name}: {npc.Voice.Pretty()}, Template={z}, Sound={x}");
        }

        if (npc.Sound is INpcInheritSoundGetter inherited && !inherited.InheritsSoundsFrom.IsNull)
        {
            inherited.InheritsSoundsFrom.TryResolve(linkCache, out var soundTemplate);
            if (soundTemplate is not null && soundTemplate is INpcGetter templateNPC) return GetVoice(templateNPC, linkCache);
        }
        else if (npc.Configuration.TemplateFlags.HasFlag(NpcConfiguration.TemplateFlag.Traits))
        {
            npc.Template.TryResolve(linkCache, out var template);
            if (template is not null && template is INpcGetter templateNPC) return GetVoice(templateNPC, linkCache);
        }
        return npc.Voice;
    }

    /// <summary>
    /// This thing is ridiculous but convenient. Can you say "Currying"?
    /// 
    /// All this does is create delegate for a dictionary, that creates a delegate for a number, which 
    /// increases the value associated with a key by the amount of the number.
    /// </summary>
    /// 
    static private Func<double, Action<T>> DictionaryIncrementer<T>(Dictionary<T, double> dict) where T : notnull
        => val => plugin => { if (val > 0.0) dict[plugin] = dict.GetValueOrDefault(plugin, 0.0) + val; };

    /// <summary>
    /// Matcher for plugin names. 
    /// A match occurs if the plugin internalName is contained in the target string.
    /// Case-insensitive.
    /// 
    /// </summary>
    /// <param internalName="str">The string against which to match the plugin names.</param>
    /// <returns>The matcher.</returns>
    /// 
    static private Func<PluginEntry, bool> PluginNameMatch(string str) => plugin => str.ContainsInsensitive(plugin.Name);

}
