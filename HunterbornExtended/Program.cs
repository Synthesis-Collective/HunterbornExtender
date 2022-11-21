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


namespace HunterbornExtender
{
    internal class Program
    {
        static readonly string HBEPatchName = "HunterbornExtenderPatch.esp";
        static readonly string HunterbornPluginName = "Hunterborn.esp";
        static readonly public ModKey HUNTERBORN = new ModKey(HunterbornPluginName, ModType.Plugin);

        static readonly public IFormLink<IMessage> MSG_FIELDDRESS = HUNTERBORN.MakeFormKey(0x38D1).ToLink<IMessage>();
        static readonly public IFormLink<IMessage> MSG_CLEAN = HUNTERBORN.MakeFormKey(0x178A3).ToLink<IMessage>();
        static readonly public IFormLink<IMessage> MSG_PLUCK = HUNTERBORN.MakeFormKey(0x178A2).ToLink<IMessage>();
        static readonly public IFormLink<IFormList> _DS_FL_AnimalRaces = HUNTERBORN.MakeFormKey(0x2B04CB).ToLink<IFormList>();
        static readonly public IFormLink<IFormList> _DS_FL_MonsterRaces = HUNTERBORN.MakeFormKey(0x2B04CB).ToLink<IFormList>();
        
        static readonly public IFormLink<IFormList> _DS_FL_CarcassesFresh = HUNTERBORN.MakeFormKey(0x01789F).ToLink<IFormList>();
        static readonly public IFormLink<IFormList> _DS_FL_CarcassesFresh_Monsters = HUNTERBORN.MakeFormKey(0x027770).ToLink<IFormList>();
        
        static readonly public IFormLink<IFormList> _DS_FL_CarcassObjects = HUNTERBORN.MakeFormKey(0x006EB9 ).ToLink<IFormList>();
        static readonly public IFormLink<IFormList> _DS_FL_CarcassObjects_Monsters = HUNTERBORN.MakeFormKey(0x027771).ToLink<IFormList>();

        static readonly public IFormLink<IFormList> _DS_FL_DeathItems = HUNTERBORN.MakeFormKey(0x00FB3F).ToLink<IFormList>();
        static readonly public IFormLink<IFormList> _DS_FL_DeathItems_Monsters = HUNTERBORN.MakeFormKey(0x027764).ToLink<IFormList>();

        static readonly public IFormLink<IFormList> _DS_FL_DeathItemTokens = HUNTERBORN.MakeFormKey(0x00FB5A).ToLink<IFormList>();
        static readonly public IFormLink<IFormList> _DS_FL_DeathItemTokens_Monsters = HUNTERBORN.MakeFormKey(0x027766).ToLink<IFormList>();

        private static Lazy<Settings.Settings> _settings = null!;

        public static Task<int> Main(string[] args)
        {
            return SynthesisPipeline.Instance
                .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch, new PatcherPreferences() {
                    ExclusionMods = new List<ModKey>() {
                        new ModKey(HBEPatchName, ModType.Plugin),
                    }
                })
                .SetTypicalOpen(GameRelease.SkyrimSE, HBEPatchName)
                .SetAutogeneratedSettings("settings", "settings.json", out _settings)
                .Run(args);
        }

        public static void RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
        {
            
        }


        static readonly ModKey UPDATE = ModKey.FromNameAndExtension("Update.esm");
        static readonly ModKey DLC1 = ModKey.FromNameAndExtension("Dawnguard.esm");
        static readonly ModKey DLC2 = ModKey.FromNameAndExtension("Dragonborn.esm");

        
        private List<FormLink<IFactionGetter>> forbiddenFactions = new(new FormLink<IFactionGetter>[] {
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
        
        private List<FormLink<IVoiceTypeGetter>> allowedVoice = new(new FormLink<IVoiceTypeGetter>[]{
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

        private List<String> monsterTypes = new(new String[] {
            "Chaurus",
            "CharusHunter",
            "Dragon",
            "FrostbiteSpider",
            "FrostbiteSpiderGiant",
            "Spriggan",
            "SprigganBurnt",
            "Troll",
            "TrollFrost",
            "Werebear",
            "Werewolf" });

        private List<String> animalTypes = new(new String[] {
            "Skip", 
            "Bear", 
            "Bear, Cave", 
            "Bear, Snow", 
            "Bristleback", 
            "Chaurus", 
            "Chaurus, Hunter", 
            "Chicken", 
            "Cow", 
            "Deer", 
            "Deer, Vale", 
            "Dog", 
            "Dragon", 
            "Elk, Female", 
            "Elk, Male", 
            "Fox", 
            "Fox, Snow", 
            "Goat", 
            "Hare", 
            "Horker", 
            "Horse", 
            "Mammoth", 
            "MudCrab, Small", 
            "MudCrab, Large", 
            "MudCrab, Giant", 
            "Sabrecat", 
            "Sabrecat, Vale", 
            "Skeever", 
            "Slaughterfish", 
            "Spider, Frostbite", 
            "Spider, Giant Frostbite", 
            "Spriggan", 
            "Spriggan, Burnt", 
            "Troll", 
            "Troll, Frost", 
            "Werebear", 
            "Werewolf", 
            "Wolf", 
            "Wolf, Ice"});

        private List<EDIDLink<IRaceGetter>> blacklistedRecords = new(new EDIDLink<IRaceGetter>[] { 
            new EDIDLink<IRaceGetter>("HISLCBlackWolf"),
            new EDIDLink<IRaceGetter>("BSKEncRat"),
        });

        private List<String> deathNameItemMatch = new(new String[] {
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

        private List<String> blacklistedDeathItems = new(new String[] {
            "DLC1DeathItemDragon06", 
			"DLC1DeathItemDragon07", 
			"DeathItemDragonBonesOnly", 
			"DeathItemVampire", 
			"_00DeathItemDramanBossSpecial", 
            "DeathItemForsworn" 
        });

        private Dictionary<string, string> fixedAnimalTypes = new Dictionary<string,string> () {
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

        private Dictionary<string, int> animalTypeIndex = new Dictionary<string, int>() {
            { "Bear" , 0 },
            { "BearCave" , 1 },
			{ "BearSnow" , 2 },
            { "Chicken" , 3 },
			{ "Cow" , 4 },
			{ "Deer" , 5 },
			{ "Dog" , 6 },
			{ "ElkFemale" , 7 },
			{ "ElkMale" , 8 },
			{ "Fox" , 9 },
			{ "FoxIce" , 10 },
			{ "Goat" , 11 },
			{ "Hare" , 12 },
			{ "Horker" , 13 },
			{ "Horse" , 14 },
			{ "Mammoth" , 15 },
			{ "MudCrab01" , 16 },
			{ "MudCrab02" , 17 },
			{ "MudCrab03" , 18 },
			{ "Sabrecat" , 19 },
			{ "SabrecatSnow" , 20 },
			{ "Skeever" , 21 },
			{ "Slaughterfish" , 22 },
			{ "Wolf" , 23 },
			{ "WolfIce" , 24 },
			{ "DeerVale" , 25 },
			{ "SabrecatVale" , 26 },
			{ "Bristleback" , 27 },
			{ "Chaurus" , 0 },
			{ "FrostbiteSpider" , 1 },
			{ "FrostbiteSpiderGiant" , 2 },
			{ "Spriggan" , 3 },
			{ "Troll" , 4 },
			{ "TrollFrost" , 5 },
			{ "Werewolf" , 6 },
			{ "Dragon" , 7 },
			{ "CharusHunter" , 8 },
			{ "Werebear" , 9 },
			{ "SprigganBurnt", 10 }
        };

        private Dictionary<string, string> vanillaToCaco = new Dictionary<string, string>() {
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


        List<ConstructibleObject> cobjRecords = new();
        List<FormList> flstRecords = new();
        List<LeveledItem> lvliRecords = new();
        List<MiscItem> miscRecords = new();
        List<Ingestible> alchRecords = new();
        List<Quest> qustRecords = new();
        List<MiscItem> Pelts = new();
        List<MiscItem> DefaultPelt = new();
        bool CheckPatchesRunOnce = false;
        int progressNumber = 0;
        bool debugging = true;

        private void loadKnownDeathItemsAnimals()
        {

        }
    }
}
