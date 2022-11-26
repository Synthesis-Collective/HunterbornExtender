﻿using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins.Cache;
using static HunterbornExtender.FormKeys;
using System.Linq;
using Noggog.WorkEngine;
using System.Text.RegularExpressions;
using Mutagen.Bethesda.Plugins.Records;
using Newtonsoft.Json.Linq;
using Microsoft.CodeAnalysis;
using HunterbornExtender.Settings;
using static System.Formats.Asn1.AsnWriter;
using System.Reflection;
using Mutagen.Bethesda.Plugins.Assets;
using Mutagen.Bethesda.FormKeys.Fallout4;
using System.IO;
using System.Text.Json.Nodes;
using System.Text.Json;
using DynamicData.Kernel;

namespace HunterbornExtender
{
    sealed internal class Program
    {
        record CreatureType(String Type, String Display, int Index, bool IsMonster);
        record CreatureRecord(String Type, String Display, int Index, bool IsMonster) : CreatureType(Type, Display, Index, IsMonster);
        //record DeathItemNPC(INpcGetter known, String Name, String Edid, String Filename, bool ShowNPCS);
        record DeathItemRecord(String edid, String descriptive, HashSet<INpcGetter> Npcs);
        
        record PersistentData(Dictionary<ILeveledItemGetter, DeathItemRecord> DeathItems);

        record PeltRecord(IItemGetter p0, IItemGetter p1, IItemGetter p2, IItemGetter p3);

        record HBJson();

        private static Lazy<Settings.Settings> _settings = null!;

        static List<IConstructibleObjectGetter> CobjRecords = new();
        static List<IFormListGetter> FlstRecords = new();
        static List<ILeveledItemGetter> LvliRecords = new();
        static Dictionary<CreatureType, IMiscItemGetter> MiscRecords = new();
        static Dictionary<CreatureType, IIngestibleGetter> AlchRecords = new();
        static Dictionary<CreatureType, IQuestGetter> QustRecords = new();
        static Dictionary<CreatureType, PeltRecord> Pelts = new();
        static Dictionary<CreatureType, IItemGetter> DefaultPelt = new();

        static bool CheckPatchesRunOnce = false;
        static int progressNumber = 0;
        static bool debugging = true;
        static List<IFormLinkGetter<ILeveledItemGetter>> KnownDeathItemsAnimals = new();
        static List<IFormLinkGetter<ILeveledItemGetter>> KnownDeathItemsMonsters = new();






        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch, new PatcherPreferences() {
                    ExclusionMods = new List<ModKey>() {
                        new ModKey("HunterbornExtenderPatch.esp", ModType.Plugin),
                    }
                })
                .SetTypicalOpen(GameRelease.SkyrimSE, "HunterbornExtenderPatch.esp")
                .SetAutogeneratedSettings("settings", "settings.json", out _settings)
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Console.WriteLine("Loading persistent data.");
            PersistentData scope = LoadData(state);

            Console.WriteLine("Checking patches.");
            CheckPatches(state);

            LoadCreatures(state, scope);
            // DO SETTINGS AND CONFIGURATION
            // GUI STUFF

            SaveData(state, scope);

            // PATCH

        }

        /// <summary>
        /// Read all available Json files and turn them into data structures.
        /// </summary>
        static void CheckPatches(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            if (!CheckPatchesRunOnce)
            {
                // TODO do this
                CheckPatchesRunOnce = true;
                
                // This should populate:
                // * allowedVoice
                // * deathItemNameMatch
                // * fixedAnimalTypes
                // * 
            }
        }

        /// <summary>
        /// Store the creature assignment so that they don't need to be set by the user each time.
        /// This also means storing the table of DeathItems, and the CreatureType lists.
        /// </summary>
        static void SaveData(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, PersistentData scope)
        {

        }

        /// <summary>
        /// Load the creature assignment so that they don't need to be set by the user each time.
        /// This also means loading the table of DeathItems, and the CreatureType lists.
        /// </summary>
        static PersistentData LoadData(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            return new PersistentData(new());
        }

        /// <summary>
        /// Combines persistent data with the results of scanning the load order.
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="scope"></param>
        static void LoadCreatures(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, PersistentData scope)
        {
            CheckPatches(state);
            Console.WriteLine("Loading creature records...");
            try
            {
                GetCreatures(scope, state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failure: {ex.GetType()} {ex.Message}");
                Console.WriteLine(ex.StackTrace?.ToString());
            }
        }

        /// <summary>
        /// Clears persistent data and then scans the load order.
        /// 
        /// </summary>
        /// <param name="state"></param>
        /// <param name="scope"></param>
        static void ClearCreatures(IPatcherState<ISkyrimMod, ISkyrimModGetter> state, PersistentData scope)
        {
            CheckPatches(state);
            Console.WriteLine("Clearing and reloading creature records...");
            try
            {
                scope.DeathItems.Clear();
                GetCreatures(scope, state);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failure: {ex.GetType()} {ex.Message}");
                Console.WriteLine(ex.StackTrace?.ToString());
            }
        }

        /// <summary>
        /// Scans the load order for all Npc records
        /// 
        /// </summary>
        /// <param name="deathItems"></param>
        /// <param name="state"></param>
        static void GetCreatures(PersistentData scope, IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            // Preload the default items.
            LoadKnownDeathItems(state.LinkCache);

            //$scope.items = patcherSettings.items = patcherSettings.items || { };
            //$scope.animalTypes = animalTypes;

            int index = 0;
            int count = state.LoadOrder.PriorityOrder.Npc().WinningOverrides().Count();

            state.LoadOrder.PriorityOrder.Npc().WinningOverrides().ForEach(npc => {
                //Console.WriteLine($"Getting creature information ({index}/{count})");
                //BuildDeathItem(scope, npc, state.LinkCache);
                index++;
            });
        }

        /// <summary>
        /// Copies the contents of the pre-existing deathitems formlists into Lists.
        /// </summary>
        /// <param name="cache"></param>
        static private void LoadKnownDeathItems(ILinkCache<ISkyrimMod, ISkyrimModGetter> cache)
        {
            var formlistAnimals = _DS_FL_DeathItems.Resolve<IFormListGetter>(cache);
            KnownDeathItemsAnimals.Clear();
            KnownDeathItemsAnimals.AddRange(formlistAnimals.Items.OfType<IFormLinkGetter<ILeveledItemGetter>>().Where(x => !x.IsNull));

            var formlistMonsters = _DS_FL_DeathItems_Monsters.Resolve<IFormListGetter>(cache);
            KnownDeathItemsMonsters.Clear();
            KnownDeathItemsMonsters.AddRange(formlistMonsters.Items.OfType<IFormLinkGetter<ILeveledItemGetter>>().Where(x => !x.IsNull));
        }

        /// <summary>
        /// Hunterborn lives and dies by the DeathItem (INAM) field of NPC_ records. 
        /// This method groups Npcs by their DeathItem and assigns a name to that group using 
        ///  * DeathItemNameMatches
        ///  * 
        /// the LeveledItem that identifies the creature? I think?
        /// 
        /// </summary>
        /// <param name="deathItems">Map from the DeathItem (INAM) field of npcs to the DeathItemRecord that will be used to build the patch.</param>
        /// <param name="npc">The npc being examined.</param>
        /// <param name="cache"></param>
        /// <exception cref="InvalidOperationException"></exception>
        static void BuildDeathItem(PersistentData scope, INpcGetter npc, ILinkCache<ISkyrimMod, ISkyrimModGetter> cache)
        {
            String filename = npc.FormKey.ModKey.FileName.ToString();
            String? name = npc.Name?.ToString();
            String? edid = npc.EditorID?.ToString();
            //String name = rec.Name?.ToString() ?? throw new InvalidOperationException();
            //String edid = rec.EditorID?.ToString() ?? throw new InvalidOperationException();

            ILeveledItemGetter? deathItem = npc.DeathItem?.TryResolve(cache);
            String? deathItemEdid = deathItem?.EditorID?.ToString();

            // If there is no deathitem we obviously cannot match based on it, so skip this creature.
            if (deathItem == null)
            {
                //Console.WriteLine($"{npc.ToStandardizedIdentifier()} has no DeathItem (INAM) field, SKIPPING.");
                return;
            }

            // The Edid is currently used to derive names for the Forms that will created later.
            // Therefore skip anything that doesn't have an Edid until an alternative naming system is devised.
            if (deathItemEdid == null)
            {
                Console.WriteLine($"{npc.ToStandardizedIdentifier()} has a DeathItem (INAM) with no editorID, SKIPPING.");
                return;
            }

            // Convert the list of DeathItemNameMatches into regular expressions.
            // The regexes are run against the EDIDS.
            // @TODO Mention to json writers that they can use regular expressions to match their creatures using EditorIDs.
            List<Regex> deathItemNameRegexes = new(DeathItemNameMatches.Select(name => new Regex(name, RegexOptions.IgnoreCase)));

            // Don't create a new DeathItemRecord if there is already a mapping.
            if (!scope.DeathItems.ContainsKey(deathItem))
            {
                var deathItemNameMatch = deathItemNameRegexes
                    .Where(regex => deathItemEdid != null && regex.IsMatch(deathItemEdid))
                    .Select(regex => regex.ToString()).DefaultIfEmpty(null).FirstOrDefault();

                // Check the known deathitems table.
                // @TODO: What does this actually accomplish?
                if (KnownDeathItemsAnimals.Contains(deathItem) || KnownDeathItemsMonsters.Contains(deathItem))
                {
                    var descriptive = NameSubstitutions.GetValueOrDefault(deathItemEdid, deathItemEdid);
                    scope.DeathItems[deathItem] = new DeathItemRecord(deathItemEdid, descriptive, new());
                }

                else if (deathItemNameMatch != null)
                {
                    var descriptive = NameSubstitutions.GetValueOrDefault(deathItemNameMatch, deathItemNameMatch);
                    scope.DeathItems[deathItem] = new DeathItemRecord(deathItemEdid, descriptive, new());
                }

                else if (deathItemEdid?.ContainsInsensitive("SpiderAlbino") ?? false)
                {
                    scope.DeathItems[deathItem] = new DeathItemRecord(deathItemEdid, "Spider, Albino", new());
                }

                else if (deathItemEdid?.ContainsInsensitive(" Elk ") ?? false)
                {
                    scope.DeathItems[deathItem] = new DeathItemRecord(deathItemEdid, "Elk, Male", new());
                }

                else if (deathItemEdid?.ContainsInsensitive(" MudCrab ") ?? false)
                {
                    scope.DeathItems[deathItem] = new DeathItemRecord(deathItemEdid, "MudCrab, Large", new());
                }

                else
                    scope.DeathItems[deathItem] = new DeathItemRecord("Skip", "Skip", new());

                if (deathItemEdid?.ContainsInsensitive(" Spider ") ?? false && scope.DeathItems[deathItem].edid != "Skip")
                {
                    scope.DeathItems[deathItem] = new DeathItemRecord(deathItemEdid, "Spider, Frostbite", new());
                }

                Console.WriteLine($"Added deathItem {deathItem.ToStandardizedIdentifier()} : {scope.DeathItems[deathItem]}");
            }

            if (!scope.DeathItems[deathItem].Npcs.Contains(npc))
            {
                scope.DeathItems[deathItem].Npcs.Add(npc);
                //Console.WriteLine($"Added NPC to deathItem {deathItem.ToStandardizedIdentifier()} -> {npc.ToStandardizedIdentifier()}");
            }
        }

        /*
        static private List<FormLink<IFactionGetter>> ForbiddenFactions = new(new FormLink<IFactionGetter>[] {
            Dawnguard.Faction.DLC1VampireFaction,
            Dragonborn.Faction.DLC2AshSpawnFaction, 
            Skyrim.Faction.DragonPriestFaction, 
            Skyrim.Faction.DraugrFaction,
            Skyrim.Faction.DwarvenAutomatonFaction,
            Skyrim.Faction.IceWraithFaction,
            Dawnguard.Faction.SoulCairnFaction,
            Skyrim.Faction.VampireFaction,
            Skyrim.Faction.WispFaction 
        });

        static private List<FormLink<IVoiceTypeGetter>> AllowedVoice = new(new FormLink<IVoiceTypeGetter>[]{
            Skyrim.VoiceType.CrBearVoice,
            Skyrim.VoiceType.CrChickenVoice,
            Skyrim.VoiceType.CrCowVoice,
            Skyrim.VoiceType.CrDeerVoice,
            Skyrim.VoiceType.CrDogVoice,
            Dawnguard.VoiceType.CrDogHusky,
            Skyrim.VoiceType.CrFoxVoice,
            Skyrim.VoiceType.CrGoatVoice,
            Skyrim.VoiceType.CrHareVoice,
            Skyrim.VoiceType.CrHorkerVoice,
            Skyrim.VoiceType.CrHorseVoice,
            Skyrim.VoiceType.CrMammothVoice,
            Skyrim.VoiceType.CrMudcrabVoice,
            Skyrim.VoiceType.CrSabreCatVoice,
            Skyrim.VoiceType.CrSkeeverVoice,
            Skyrim.VoiceType.CrSlaughterfishVoice,
            Skyrim.VoiceType.CrWolfVoice,
            Dragonborn.VoiceType.DLC2CrBristlebackVoice,
            Skyrim.VoiceType.CrChaurusVoice,
            Skyrim.VoiceType.CrFrostbiteSpiderVoice,
            Skyrim.VoiceType.CrFrostbiteSpiderGiantVoice,
            Skyrim.VoiceType.CrSprigganVoice,
            Skyrim.VoiceType.CrTrollVoice,
            Skyrim.VoiceType.CrWerewolfVoice,
            Skyrim.VoiceType.CrDragonVoice,
            Dawnguard.VoiceType.CrChaurusInsectVoice
        });

        static readonly CreatureType Skip = new CreatureType("Skip", "Skip", -1, false);

        /// <summary>
        /// The list of monster types. 
        /// The indices of the CreatureTypes must initially match the FormLists 
        /// as they exist in the Hunterborn plugin, and must match the order of the formlists in the patch.
        /// So the ordering this list will be used to enforce the ordering in the patch.
        /// </summary>
        static List<CreatureType> MonsterTypes = new(new CreatureType[] {
            new CreatureType("Chaurus" , "Chaurus", 0, true),
            new CreatureType("FrostbiteSpider" , "Spider, Frostbite", 1, true),
            new CreatureType("FrostbiteSpiderGiant" , "Spider, Giant Frostbite", 2, true),
            new CreatureType("Spriggan" , "Spriggan", 3, true),
            new CreatureType("Troll" , "Troll", 4, true),
            new CreatureType("TrollFrost" , "Troll, Frost", 5, true),
            new CreatureType("Werewolf" , "Werewolf", 6, true),
            new CreatureType("Dragon" , "Dragon", 7, true),
            new CreatureType("CharusHunter" , "Chaurus, Hunter", 8, true),
            new CreatureType("Werebear" , "Werebear", 9, true),
            new CreatureType("SprigganBurnt", "Spriggan, Burnt", 10, true)
        });

        /// <summary>
        /// The list of animal types. 
        /// The indices of the CreatureTypes must initially match the FormLists 
        /// as they exist in the Hunterborn plugin, and must match the order of the formlists in the patch.
        /// So the ordering this list will be used to enforce the ordering in the patch.
        /// </summary>
        static List<CreatureType> AnimalTypes = new (new CreatureType[] {
            new CreatureType("Skip", "Skip", -1, false),
            new CreatureType("Bear" , "Bear", 0, false),
            new CreatureType("BearCave" , "Bear, Cave", 1, false),
            new CreatureType("BearSnow" , "Bear, Snow", 2, false),
            new CreatureType("Chicken" , "Chicken", 3, false),
            new CreatureType("Cow" , "Cow", 4, false),
            new CreatureType("Deer" , "Deer", 5, false),
            new CreatureType("Dog" , "Dog", 6, false),
            new CreatureType("ElkFemale" , "Elk, Female", 7, false),
            new CreatureType("ElkMale", "Elk, Male", 8, false),
            new CreatureType("Fox", "Fox" , 9, false),
            new CreatureType("FoxIce", "Fox, Snow" , 10, false),
            new CreatureType("Goat", "Goat", 11, false),
            new CreatureType("Hare" , "Hare", 12, false),
            new CreatureType("Horker" , "Horker", 13, false),
            new CreatureType("Horse" , "Horse", 14, false),
            new CreatureType("Mammoth" , "Mammoth", 15, false),
            new CreatureType("MudCrab01" , "Mudcrab, Small", 16, false),
            new CreatureType("MudCrab02" , "Mudcrab, Large", 17, false),
            new CreatureType("MudCrab03" , "Mudcrab, Giant", 18, false),
            new CreatureType("Sabrecat" , "Sabrecat", 19, false),
            new CreatureType("SabrecatSnow" , "Sabrecat, Snow", 20, false),
            new CreatureType("Skeever" , "Skeever", 21, false),
            new CreatureType("Slaughterfish" , "Slaughterfish", 22, false),
            new CreatureType("Wolf" , "Wolf", 23, false),
            new CreatureType("WolfIce" , "Wolf, Ice", 24, false),
            new CreatureType("DeerVale" , "Deer, Vale", 25, false),
            new CreatureType("SabrecatVale" , "Sabrecat, Vale", 26, false),
            new CreatureType("Bristleback" , "Bristleback", 27, false)
        });


        static private List<EDIDLink<IRaceGetter>> BlacklistedRecords = new(new EDIDLink<IRaceGetter>[] { 
            new EDIDLink<IRaceGetter>("HISLCBlackWolf"), 
            new EDIDLink<IRaceGetter>("BSKEncRat"),
        });

        static private Dictionary<string, string> FixedAnimalTypes = new Dictionary<string, string>() {
            { "Bear, Cave", "BearCave" },
            { "Bear, Snow", "BearSnow" },
            { "Chaurus, Hunter", "CharusHunter" },
            { "Elk, Female", "ElkFemale" },
            { "Elk, Male", "ElkMale" },
            { "Fox, Snow", "FoxIce" },
            { "MudCrab, Small", "MudCrab01" },
            { "MudCrab, Large", "MudCrab02" },
            { "MudCrab, Giant", "MudCrab03" },
            { "Sabrecat, Snow", "SabrecatSnow" },
            { "Spider, Frostbite", "FrostbiteSpider" },
            { "Spider, Giant Frostbite", "FrostbiteSpiderGiant" },
            { "Spriggan, Burnt", "SprigganBurnt" },
            { "Deer, Vale", "DeerVale" },
            { "Sabrecat, Vale", "SabrecatVale" },
            { "Wolf, Ice", "WolfIce" },
            { "Troll, Frost", "TrollFrost" }
        };


        static private List<FormLink<ILeveledItemGetter>> BlacklistedDeathItems = new(new FormLink<ILeveledItemGetter>[] {
            Skyrim.LeveledItem.DeathItemDragonBonesOnly,
            Skyrim.LeveledItem.DeathItemVampire,
            Skyrim.LeveledItem.DeathItemForsworn,
            Dawnguard.LeveledItem.DLC1DeathItemDragon06,
            Dawnguard.LeveledItem.DLC1DeathItemDragon07,
            ModKey.FromFileName("Skyrim Immersive Creatures Special Edition").MakeFormKey(0x11B217).ToLink<ILeveledItemGetter>()
        });


        static private Dictionary<string, string> VanillaToCaco = new Dictionary<string, string>() {
            { "_DS_Food_Raw_Bear", "CACO_FoodMeatBear" },
			{ "_DS_Food_Raw_Chaurus", "CACO_FoodMeatChaurusMeat" },
			{ "_DS_Food_Raw_Dragon", "_DS_Food_Raw_Dragon" },
			{ "_DS_Food_Raw_Elk", "FoodMeatVenison" },
			{ "_DS_Food_Raw_Fox", "CACO_FoodMeatFox" },
			{ "_DS_Food_Raw_Goat", "CACO_FoodMeatGoatPortionRaw" },
			{ "_DS_Food_Raw_Hare", "_DS_Food_Raw_Hare" },
			{ "_DS_Food_Raw_Mammoth", "CACO_FoodMeatMammoth" },
			{ "_DS_Food_Raw_Mudcrab", "_DS_Food_Raw_Mudcrab" },
			{ "_DS_Food_Raw_Sabrecat", "CACO_FoodMeatSabre" },
			{ "_DS_Food_Raw_Skeever", "CACO_FoodMeatSkeeverRaw" },
			{ "_DS_Food_Raw_Slaughterfish", "CACO_FoodSeaSlaughterfishRaw" },
			{ "_DS_Food_Raw_Spider", "_DS_Food_Raw_Spider" },
			{ "_DS_Food_Raw_Troll", "CACO_FoodMeatTroll" },
			{ "_DS_Food_Raw_Wolf", "FoodDogMeat" },
			{ "FoodBeef", "FoodBeef" },
			{ "FoodChicken", "FoodChicken" },
			{ "FoodClamMeat", "FoodClamMeat" },
			{ "FoodDogMeat", "FoodDogMeat" },
			{ "FoodGoatMeat", "FoodGoatMeat" },
			{ "FoodHorkerMeat", "FoodHorkerMeat" },
			{ "FoodHorseMeat", "FoodHorseMeat" },
			{ "FoodMammothMeat", "FoodMammothMeat" },
			{ "FoodPheasant", "FoodPheasant" },
			{ "FoodRabbit", "FoodRabbit" },
			{ "FoodSalmon", "FoodSalmon" },
			{ "FoodVenison", "FoodVenison" },
			{ "DLC2FoodAshHopperMeat", "DLC2FoodAshHopperMeat" },
			{ "DLC2FoodAshHopperLeg", "DLC2FoodAshHopperLeg" },
			{ "DLC2FoodBoarMeat", "DLC2FoodBoarMeat" },
			{ "HumanFlesh", "CACO_FoodMeatHumanoidFlesh" }
        };


        private bool HasFaction(INpcGetter creature, List<FormLink<IFactionGetter>> factions, ILinkCache<ISkyrimMod, ISkyrimModGetter> cache) {
            return !creature.Factions
                .Select(rank => rank.Faction.Resolve<IFactionGetter>(cache))
                .Where(faction => faction != null ? factions.Contains(faction.ToLink()) : false)
                .Any();
        }

        private bool HasVoice(INpcGetter creature, List<FormLink<IVoiceTypeGetter>> voices)
        {
            var creatureVoice = creature.Voice;
            return voices.Any(v => v.Equals(creatureVoice));
        }

        private bool IsCreature(INpcGetter actor, ILinkCache<ISkyrimMod, ISkyrimModGetter> cache) {
            var deathItem = actor.DeathItem;
            var edid = actor.EditorID;
            var edidLink = edid != null ? new EDIDLink<IRaceGetter>(edid) : null;

            return !HasFaction(actor, ForbiddenFactions, cache)
                && HasVoice(actor, AllowedVoice)
                && deathItem != null
                && !(KnownDeathItemsAnimals.Contains(deathItem) || KnownDeathItemsMonsters.Contains(deathItem))
                && (edidLink == null || !BlacklistedRecords.Contains(edidLink))
                && !BlacklistedDeathItems.Any(item => item.Equals(deathItem));
        }

        private String GetElementFileName(FormKey form) {
            return form.ModKey.FileName;
        }

        private void BuildPeltRecords(ILinkCache<ISkyrimMod, ISkyrimModGetter> cache)
        {
            List<IFormListGetter> peltFormLists = _DS_FL_PeltLists.Resolve(cache).Items.OfType<IFormListGetter>().ToList();
            for (int i = 0; i < peltFormLists.Count; i++)
            {
                var peltFormList = peltFormLists[i];
                var peltList = peltFormList?.Items.OfType<IItemGetter>().ToList();

                if (peltList == null) continue;
                if (peltList.Count != 4) throw new InvalidOperationException($"WRONG PELT COUNT IN {peltFormList?.ToStandardizedIdentifier()}");
                if (i >= AnimalTypes.Count) throw new InvalidOperationException("MISMATCHED DATA WHILE BUILDING PELT RECORDS");

                CreatureType ct = AnimalTypes[i];
                Pelts[ct] = new(peltList[0], peltList[1], peltList[2], peltList[3]);
                DefaultPelt[ct] = peltList[1];
            }

            List<IFormListGetter> peltFormLists_Monsters = _DS_FL_PeltLists_Monsters.Resolve(cache).Items.OfType<IFormListGetter>().ToList();
            for (int i = 0; i < peltFormLists_Monsters.Count; i++)
            {
                var peltFormList = peltFormLists[i];
                var peltList = peltFormList?.Items.OfType<IItemGetter>().ToList();

                if (peltList == null) continue;
                if (peltList.Count != 4) throw new InvalidOperationException($"WRONG PELT COUNT IN {peltFormList?.ToStandardizedIdentifier()}");
                if (i >= MonsterTypes.Count) throw new InvalidOperationException("MISMATCHED DATA WHILE BUILDING PELT RECORDS");

                CreatureType ct = MonsterTypes[i];
                Pelts[ct] = new(peltList[0], peltList[1], peltList[2], peltList[3]);
                DefaultPelt[ct] = peltList[1];
            }
        }

    /// <summary>
    /// The quest and formlists needed by the patching methods, already resolved.
    /// </summary>
    readonly record struct StandardRecords(
            Quest _DS_Hunterborn,
            FormList _DS_FL_CarcassObjects,

            FormList _DS_FL_Mats__Lists,
            FormList _DS_FL_Mats__Perfect,
            FormList _DS_FL_PeltLists,
            FormList _DS_FL_DeathItems,
            FormList _DS_FL_DeathItemTokens,

            FormList _DS_FL_Mats__Lists_Monsters,
            FormList _DS_FL_Mats__Perfect_Monsters,
            FormList _DS_FL_PeltLists_Monsters,
            FormList _DS_FL_DeathItems_Monsters,
            FormList _DS_FL_DeathItemTokens_Monsters
            );


        /// <summary>
        /// Some of the methods in this class require the resolved Hunterborn Quest and resolved FormLists.
        /// This resolves them, adds them to the patch, and packs them into a Record.
        /// </summary>
        static StandardRecords CreateStandardRecords(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            StandardRecords recs = new StandardRecords(
                (_DS_Hunterborn.Resolve<Quest>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_CarcassObjects.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),

                (_DS_FL_Mats__Lists.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_Mats__Perfect.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_PeltLists.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_DeathItems.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_DeathItemTokens.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),

                (_DS_FL_Mats__Lists_Monsters.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_Mats__Perfect_Monsters.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_PeltLists_Monsters.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_DeathItems_Monsters.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException(),
                (_DS_FL_DeathItemTokens_Monsters.Resolve<FormList>(state.LinkCache)) ?? throw new InvalidOperationException()
                );

            state.PatchMod.Quests.Add(recs._DS_Hunterborn);
            state.PatchMod.FormLists.Add(recs._DS_FL_CarcassObjects);
            state.PatchMod.FormLists.Add(recs._DS_FL_Mats__Lists);
            state.PatchMod.FormLists.Add(recs._DS_FL_Mats__Perfect);
            state.PatchMod.FormLists.Add(recs._DS_FL_PeltLists);
            state.PatchMod.FormLists.Add(recs._DS_FL_DeathItems);
            state.PatchMod.FormLists.Add(recs._DS_FL_DeathItemTokens);
            state.PatchMod.FormLists.Add(recs._DS_FL_Mats__Lists_Monsters);
            state.PatchMod.FormLists.Add(recs._DS_FL_Mats__Perfect_Monsters);
            state.PatchMod.FormLists.Add(recs._DS_FL_PeltLists_Monsters);
            state.PatchMod.FormLists.Add(recs._DS_FL_DeathItems_Monsters);
            state.PatchMod.FormLists.Add(recs._DS_FL_DeathItemTokens_Monsters);

            return recs;
        }

        /// <summary>
        /// Creates the Misc deathtoken for a creature.
        /// 
        /// The new deathtoken is appended to the deathtoken formlist for animals or monsters.
        /// 
        /// The deathtoken will be derived from the prototype's token (if it exists) or derived from the COW's deathtoken.
        /// 
        /// Naming is done heuristically. 
        /// </summary>
        /// <param name="prototype">The CreatureType prototype that is used to define the creature's hunting properties.</param>
        /// <param name="animalRecord">The CreatureType that the deathtoken will uniquely specify.</param>
        /// 
        void CreateToken(CreatureType prototype, CreatureRecord animalRecord, StandardRecords recs, IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            if (MiscRecords.ContainsKey(prototype))
            {
                var existingToken = MiscRecords[prototype];
                var token = state.PatchMod.MiscItems.AddNew();
                if (existingToken == null || token == null) throw new InvalidOperationException();

                token.DeepCopyIn(existingToken);

                String newEdid = $"_DS_DI{animalRecord.Type}".Replace("Crab", "crab");
                String newName = (token.Name?.ToString() ?? $"{animalRecord.Type} Token").Replace("Elk ", "Elk");
                newName = Regex.Replace(newName, " Crab(0.) ", "crab $1");

                token.EditorID = newEdid;
                token.Name= newName;

                MiscRecords[animalRecord] = token;
                if (!MonsterTypes.Contains(prototype)) recs._DS_FL_DeathItemTokens.Items.Add(token);
                else recs._DS_FL_DeathItemTokens_Monsters.Items.Add(token);
            }       
            else
            {
                var existingToken = recs._DS_FL_DeathItemTokens.Items[5].Resolve(state.LinkCache);
                var token = state.PatchMod.MiscItems.AddNew();
                if (existingToken == null || token == null) throw new InvalidOperationException();

                token.DeepCopyIn(existingToken);

                // WTF is happening here.
                String newEdid = token.EditorID?.Replace("Cow", animalRecord.Type) ?? $"_DS_DI{animalRecord.Type}";
                String newName = token.Name?.ToString().Replace("Cow", animalRecord.Name) ?? $"{animalRecord.Type} Token";

                token.EditorID = newEdid;
                token.Name = newName;

                MiscRecords[animalRecord] = token;
                if (!MonsterTypes.Contains(prototype)) recs._DS_FL_DeathItemTokens.Items.Add(token);
                else
                {
                    recs._DS_FL_DeathItemTokens_Monsters.Items.Add(token);
                    token.Keywords?.Remove(_DS_KW_AnimalToken);
                    // TODO: How do I CREATE the keywords field?
                    token.Keywords.Add(_DS_KW_MonsterToken);
                }
            }
        }


        void CreateCarcass(CreatureType animalType, CreatureRecord animalRecord, INpcGetter npc, HBJSon json, IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            Carcass = null;
            if (miscRecords.hasOwnProperty(`_DS_CarcassFresh_{ animalType}`) && !monsterTypes.includes(animalType)) {
                Carcass = xelib.CopyElement(miscRecords[`_DS_CarcassFresh_{ animalType}`], patch, true);
                xelib.SetValue(Carcass, 'EDID', xelib.EditorID(Carcass).replace(animalType, animalRecord));
                xelib.SetValue(Carcass, 'FULL', xelib.FullName(Carcass).replace(animalType.replace('Sabrecat', 'Sabre Cat').replace(/ Crab(0.) /, 'crab').replace('Elk ', 'Elk'), xelib.FullName(npc)));
                let edid = xelib.EditorID(Carcass);
                miscRecords[edid] = Carcass;
                xelib.AddFormID(flstRecords["_DS_FL_CarcassObjects"], xelib.GetValue(Carcass));
            } else if (!monsterTypes.includes(animalType))
            {
                Carcass = xelib.CopyElement(miscRecords['_DS_CarcassFresh_Cow'], patch, true);
                xelib.SetValue(Carcass, 'EDID', xelib.EditorID(Carcass).replace("Cow", animalRecord));
                let fullName = "";
                if (jsonRecord.properName != "")
                {
                    fullName = jsonRecord.properName;
                }
                else
                {
                    fullName = xelib.FullName(npc);
                }
                xelib.SetValue(Carcass, 'FULL', xelib.FullName(Carcass).replace("Cow", fullName));
                xelib.SetValue(Carcass, 'Model\\MODL', "Clutter\\Containers\\MiscSackLarge.nif");
                xelib.SetValue(Carcass, 'DATA\\Value', jsonRecord.carcassValue);
                xelib.SetValue(Carcass, 'DATA\\Weight', jsonRecord.carcassWeight);
                let edid = xelib.EditorID(Carcass);
                miscRecords[edid] = Carcass;
                xelib.AddFormID(flstRecords["_DS_FL_CarcassObjects"], xelib.GetValue(Carcass));
            }
            return Carcass;
        };

        let CreateMats(animalType, animalRecord, jsonRecord, patch)
        {
            if (flstRecords.hasOwnProperty(`_DS_FL_Mats_{ animalType}`)) {
                let oldEdid = xelib.EditorID(flstRecords[`_DS_FL_Mats_{ animalType}`]);
                let formList = xelib.CopyElement(flstRecords[`_DS_FL_Mats_{ animalType}`], patch, true);
                xelib.SetValue(formList, 'EDID', oldEdid.replace(animalType, animalRecord));
                if (!monsterTypes.includes(animalType))
                {
                    xelib.AddFormID(flstRecords["_DS_FL_Mats__Lists"], xelib.GetValue(formList));
                }
                else
                {
                    xelib.AddFormID(flstRecords["_DS_FL_Mats__Lists_Monsters"], xelib.GetValue(formList));
                }
            } else
            {
                let oldEdid = xelib.EditorID(flstRecords['_DS_FL_Mats_Cow']);
                formList = xelib.CopyElement(flstRecords['_DS_FL_Mats_Cow'], patch, true);
                xelib.SetValue(formList, 'EDID', oldEdid.replace("Cow", animalRecord));
                xelib.RemoveElement(formList, 'FormIDs');
                if (!monsterTypes.includes(animalType))
                {
                    xelib.AddFormID(flstRecords["_DS_FL_Mats__Lists"], xelib.GetValue(formList));
                }
                else
                {
                    xelib.AddFormID(flstRecords["_DS_FL_Mats__Lists_Monsters"], xelib.GetValue(formList));
                }
                Object.keys(jsonRecord.mats).forEach(key => {
                let MatsLvl = xelib.CopyElement(lvliRecords['_DS_LI_Mats_Cow_00'], patch, true);
                xelib.SetValue(MatsLvl, 'EDID', `_DS_LI_Mats_{animalRecord}
                _0${ key}`);
                let edid = xelib.EditorID(MatsLvl);
                lvliRecords[edid] = MatsLvl;
                xelib.AddFormID(formList, xelib.LongName(MatsLvl));
                xelib.RemoveElement(MatsLvl, 'Leveled List Entries');
                let value = jsonRecord.mats[key];
                Object.keys(value).forEach(key2 => {
                    let value2 = value[key2];
                    if (xelib.FileByName('Hunterborn_CACO-SE_Patch.esp') > 0 || cobjRecords.hasOwnProperty(`HB_CACO_RecipeJerkyHare`))
                    {
                        key2 = vanillaToCaco[key2] || key2;
                    }
                    xelib.AddLeveledEntry(MatsLvl, key2, "1", value2.toString());
                });
            });
        }
    };












    public static void PrintFormKeysDefinitions<T>(Class c, IPatcherState<ISkyrimMod, ISkyrimModGetter> state, String filename) where T : IMajorRecordGetter
        {
            state.LoadOrder.TryGetIfEnabledAndExists(new ModKey(filename, ModType.Plugin), out var mod);
            var group = mod?.GetTopLevelGroup<T>();
            if (mod == null || group == null || group.Count() == 0) return;

            String modname = "MOD_" + Regex.Replace(filename.ToUpper(), "[^a-zA-Z0-9_]", "");
            String typeName = typeof(T).FullName ?? "TYPENAME";

            System.Console.WriteLine($"static readonly public ModKey {filename.ToUpper()} = new ModKey(\"{filename}\"), ModType.Plugin);");

            group.Where(rec => rec.EditorID != null).ForEach(rec => {
                var edid = rec.EditorID;
                var formID = rec.FormKey.ID.ToString("x");
                Console.WriteLine($"static readonly public FormLink<I{typeName}> {edid} = {modname}.MakeFormKey(0x{formID}).ToLink<{typeName}>();");
            });

        }




    */


        static private Dictionary<string, string> NameSubstitutions = new Dictionary<string, string>() {
            { "BearCave", "Bear, Cave" },
            { "BearSnow", "Bear, Snow" },
            { "CharusHunter", "Chaurus, Hunter"  },
            { "ElkFemale", "Elk, Female"  },
            { "ElkMale", "Elk, Male"  },
            { "FoxIce", "Fox, Snow"  },
            { "MudCrab01", "MudCrab, Small" },
            { "MudCrab02", "MudCrab, Large" },
            { "MudCrab03", "MudCrab, Giant" },
            { "SabrecatSnow", "Sabrecat, Snow" },
            { "FrostbiteSpider", "Spider, Frostbite" },
            { "FrostbiteSpiderGiant", "Spider, Giant Frostbite" },
            { "SprigganBurnt", "Spriggan, Burnt" },
            { "DeerVale", "Deer, Vale" },
            { "SabrecatVale", "Sabrecat, Vale" },
            { "WolfIce", "Wolf, Ice" },
            { "TrollFrost", "Troll, Frost" }
        };

        static private List<String> DeathItemNameMatches = new(new String[] {
            "Werebear",
            "Bear",
            "BearCave",
            "BearSnow",
            "Bristleback",
            "Chaurus",
            "CharusHunter",
            "Chicken",
            "Cow",
            "DeerVale",
            "Deer",
            "Dog",
            "Dragon",
            "ElkFemale",
            "ElkMale",
            "FoxIce",
            "Fox",
            "FrostbiteSpiderGiant",
            "FrostbiteSpider",
            "Goat",
            "Hare",
            "Horker",
            "Horse",
            "Mammoth",
            "MudCrab01",
            "MudCrab02",
            "MudCrab03",
            "SabrecatSnow",
            "SabrecatVale",
            "Sabrecat",
            "Skeever",
            "Slaughterfish",
            "Spriggan",
            "SprigganBurnt",
            "TrollFrost",
            "Troll",
            "Werewolf",
            "WolfIce",
            "Wolf"
        });


    }


}
