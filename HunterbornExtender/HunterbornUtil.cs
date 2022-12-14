using DynamicData;
using System;
using System.Threading.Tasks;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using Mutagen.Bethesda;
using Mutagen.Bethesda.Synthesis;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.FormKeys.SkyrimSE;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Plugins.Records;
using Microsoft.CodeAnalysis;
using Mutagen.Bethesda.Plugins.Exceptions;
using Mutagen.Bethesda.Plugins.Aspects;
using Noggog;
using HunterbornExtender.Settings;
using static HunterbornExtender.FormKeys;
using DeathItemGetter = Mutagen.Bethesda.Skyrim.ILeveledItemGetter;
using System.Reflection;

namespace HunterbornExtender
{
    /// <summary>
    /// Functions for naming things.
    /// An "FB" suffix means it has fallbacks to ensure that it doesn't return a null or blank string.
    /// </summary>
    public static class Naming
    {
        /// <summary>
        /// Gets the sort name or proper name of a plugin (in that order) depending on which is set.
        /// </summary>
        /// 
        static public string PluginNameFB(PluginEntry plugin)
        {
            if (!plugin.SortName.IsNullOrWhitespace()) return plugin.SortName;
            else if (!plugin.ProperName.IsNullOrWhitespace()) return plugin.ProperName;
            else return plugin.Name;
        }

        /// <summary>
        /// Returns the editorID, StandardizedIdentifier, or FormKey (in that order).
        /// </summary>
        /// 
        static public string DeathItemFB(DeathItemGetter deathItem)
            => deathItem.EditorID ?? deathItem.ToStandardizedIdentifier().ToString() ?? deathItem.FormKey.ToString();

        /// <summary>
        /// Returns the editorID, StandardizedIdentifier, or FormKey (in that order).
        /// </summary>
        /// 
        static public string NpcFB(INpcGetter npc)
        {
            if (npc.Name is not null && npc.EditorID is not null)
                return $"{npc.Name} ({npc.EditorID})";
            else
                return npc.Name?.ToString() ?? npc.EditorID ?? npc.ToStandardizedIdentifier().ToString() ?? npc.FormKey.ToString();
        }

    }

    public static class ScriptUtil
    {
        /// <summary>
        /// Retrieves a script property by quest->script name->property name.
        /// </summary>
        /// <typeparam name="ScriptTProperty">The type of script parameter.</typeparam>
        /// <param name="quest"></param>
        /// <param name="scriptName"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        /// <exception cref="ScriptMissing"></exception>
        /// <exception cref="PropertyMissing"></exception>
        static public ScriptTProperty GetProperty<ScriptTProperty>(IQuestGetter scripted, String scriptName, String propertyName) where ScriptTProperty : ScriptProperty
        {
            var scriptedName = scripted is INamedGetter named ? named.Name?.ToString() : null;
            scriptedName ??= scripted is MajorRecord rec ? (rec.EditorID ?? rec.FormKey.ToString()) : "vmad";

            if (scripted.VirtualMachineAdapter is null)
                throw new ScriptMissing(scriptedName, scriptName);

            return GetProperty<ScriptTProperty>(scripted.VirtualMachineAdapter, scriptedName, scriptName, propertyName);
        }

        /// <summary>
        /// Retrieves a script property by quest->script name->property name.
        /// </summary>
        /// <typeparam name="ScriptTProperty">The type of script parameter.</typeparam>
        /// <param name="quest"></param>
        /// <param name="scriptName"></param>
        /// <param name="propertyName"></param>
        /// <returns></returns>
        /// <exception cref="ScriptMissing"></exception>
        /// <exception cref="PropertyMissing"></exception>
        static public ScriptTProperty GetProperty<ScriptTProperty>(IScriptedGetter scripted, string scriptName, string propertyName) where ScriptTProperty : ScriptProperty
        {
            var scriptedName = scripted is INamedGetter named ? named.Name?.ToString() : null;
            scriptedName ??= scripted is MajorRecord rec ? (rec.EditorID ?? rec.FormKey.ToString()) : "vmad";
            Quest? t = null;
            if (t is Quest q) q.VirtualMachineAdapter?.ToString();

            if (scripted.VirtualMachineAdapter is null)
                throw new ScriptMissing(scriptedName, scriptName);

            return GetProperty<ScriptTProperty>(scripted.VirtualMachineAdapter, scriptedName, scriptName, propertyName);
        }

        /// <summary>
        /// Retrieves a script property by vmad->script name->property name.
        /// </summary>
        /// <typeparam name="ScriptTProperty">The type of script parameter.</typeparam>
        /// <exception cref="ScriptMissing"></exception>
        /// <exception cref="PropertyMissing"></exception>
        /// IVirtualMachineAdapterGetter
        static public ScriptTProperty GetProperty<ScriptTProperty>(IAVirtualMachineAdapterGetter vmad, string scriptedName, string scriptName, string propertyName) where ScriptTProperty : ScriptProperty
        {
            var scriptFilter = ScriptFilter(scriptName);
            var propertyFilter = PropertyFilter(propertyName);

            var script = vmad.Scripts.Where(scriptFilter).FirstOrDefault();
            if (script == null) throw new ScriptMissing(scriptedName, scriptName);

            if (script.Properties.Where(propertyFilter).FirstOrDefault() is not ScriptTProperty property)
                throw new PropertyMissing(scriptedName, scriptName, propertyName);

            return property;
        }

        /// <summary>
        /// A predicate for matching scripts by internalName.
        /// </summary>
        /// <param internalName="name">The internalName to match.</param>
        /// <returns>A predicate that matches the specified internalName.</returns>
        /// 
        static Func<IScriptEntryGetter, bool> ScriptFilter(string name) => (IScriptEntryGetter s) => name.EqualsIgnoreCase(s?.Name);

        /// <summary>
        /// A predicate for matching script properties by internalName.
        /// </summary>
        /// <param internalName="name">The internalName to match.</param>
        /// <returns>A predicate that matches the specified internalName.</returns>
        /// 
        static Func<IScriptPropertyGetter, bool> PropertyFilter(string name) => (IScriptPropertyGetter s) => name.EqualsIgnoreCase(s?.Name);

    }

    /// <summary>
    /// Adds an EqualsIgnoreCase method to String.
    /// </summary>
    public static class StringEqualsIgnoreCase
    {
        /// <summary>
        /// Equivalent to s1.ToLower().Equals(s2.ToLower()) with identity and null checking.
        /// </summary>
        public static bool EqualsIgnoreCase(this String s1, String? s2)
        {
            if (s1 == s2) return true;
            else if (s1 == null || s2 == null) return false;
            else return s1.ToLower().Equals(s2.ToLower());
        }

    }

    /// <summary>
    /// Adds pretty-printing methods to lists, arrays, dictionaries, and lists of dictionaries.
    /// </summary>
    public static class GenericPrettyPrinting
    {
        public static string Pretty<T>(this T[] array) where T : notnull => "[" + string.Join(", ", array.Select(Prettify)) + "]";

        public static string Pretty<T>(this List<T> list) where T : notnull => "[" + string.Join(", ", list.Select(Prettify)) + "]";

        public static string Pretty<T>(this HashSet<T> set) where T : notnull => "{" + string.Join(", ", set.Select(Prettify)) + "}";

        public static string Pretty<S, T>(this Dictionary<S, T> dict) where S : notnull => "{" + string.Join(", ", dict) + "}";

        public static string Pretty<S, T>(this List<Dictionary<S, T>> listOfDicts) where S : notnull => "[" + string.Join(", ", listOfDicts.Select(l => l.Pretty())) + "]";

        public static string Pretty<Tuple>(this Tuple tuple) => "{" + string.Join(", ", tuple.AsEnumerable().Select(Prettify)) + "}";

        public static string Pretty<S, T>(this IOrderedEnumerable<KeyValuePair<S, T>> enumerable) where S : notnull => "{" + string.Join(", ", enumerable) + "}";

        public static string Pretty<S, T>(this IEnumerable<KeyValuePair<S,T>> enumerable) where S : notnull => "{" + string.Join(", ", enumerable) + "}";

        public static string Pretty<T>(this IEnumerable<T> enumerable) => "{" + string.Join(", ", enumerable) + "}";

        private static bool IsPretty(this object o) => o.GetType().GetMethod("Pretty") != null;

        private static string Prettify<T>(T o) => ((o?.IsPretty() ?? false) ? o?.Pretty() : o?.ToString()) ?? "null";
    }

    /// <summary>
    /// Methods for breaking apart names into parts based on capital letters, spaces, underlines, and dashes.
    /// They are always returned as a single set.
    /// 
    /// </summary>
    sealed public class Tokenizer
    {
        static public HashSet<string> Tokenize(IEnumerable<string?> names) => names.Where(n => !n.IsNullOrWhitespace()).SelectMany(Tokenize).ToHashSet();

        static public HashSet<string> Tokenize(params string?[] names) => names.Where(n => !n.IsNullOrWhitespace()).SelectMany(Tokenize).ToHashSet();

        static public HashSet<string> Tokenize(string? name)
        {
            if (name.IsNullOrWhitespace()) return new();

            var filtered = TOKENIZER_FILTER.Replace(name, "");
            var tokens = TOKENIZER_BREAK_SPLITTER.Split(filtered);
            if (tokens == null || tokens.Length == 0) return new();

            var flatTokens = tokens.SelectMany(t => TOKENIZER_CAMEL_SPLITTER.Split(t)).Where(t => !t.IsNullOrWhitespace()).ToHashSet();

            foreach (var tokenGroup in SpecialCases.TokenSynonyms)
            {
                if (tokenGroup.Overlaps(flatTokens)) flatTokens.Add(tokenGroup);
            }

            return flatTokens;
        }

        readonly static private Regex TOKENIZER_FILTER = new("[^A-Za-z0-9 _-]");
        readonly static private Regex TOKENIZER_BREAK_SPLITTER = new("[ _-]");
        readonly static private Regex TOKENIZER_CAMEL_SPLITTER = new("([A-Z]+[a-z0-9]*)");
    }

    /// <summary>
    /// Log writing methods.
    /// </summary>
    sealed public class Write
    {
        public static void Success(int IndentLevel, string msg)
        {
            Console.Write(new string('\t', IndentLevel));
            Console.Write(" o ");
            Console.WriteLine(msg);
        }

        public static void Fail(int IndentLevel, string msg)
        {
            Console.Write(new string('\t', IndentLevel));
            Console.Write(" x ");
            Console.WriteLine(msg);
        }

        public static void Action(int IndentLevel, string msg)
        {
            Console.Write(new string('\t', IndentLevel));
            Console.WriteLine(msg);
        }

        static public void Title(int IndentLevel, string msg)
        {
            Action(IndentLevel, CreateTitle(msg));
        }

        public static void Divider(int IndentLevel)
        {
            Action(IndentLevel, DividerString);
        }

        /// <summary>
        /// For printing dividers in the console output.
        /// </summary>
        static private readonly string DividerString = "====================================================";

        /// <summary>
        /// For printing titled-dividers in the console output.
        /// </summary>
        public static string CreateTitle(string title)
        {
            int dividerLength = DividerString.Length;
            int titleLength = title.Length;
            int leftLength = Math.Max(0, (dividerLength - titleLength) / 2);
            int rightLength = Math.Max(0, (dividerLength - titleLength + 1) / 2);
            string left = DividerString[..leftLength];
            string right = DividerString[..rightLength];
            return $"{left}{title}{right}";
        }

    }
    /// <summary>
    /// Debugging class that outputs numbered checkin strings.
    /// </summary>
    sealed public record Checkin(int IndentLevel = 0, string CheckinName = "")
    {
        private int Counter = 0;

        public void Check()
        {
            Counter++;
            Write.Success(IndentLevel, $"{CheckinName} {Counter,-3}: every is okay.");
        }
    }

    /// <summary>
    /// Thrown to indicate that a problem occurred during Heuristics.
    /// </summary>
    public sealed class HeuristicsError : Exception
    {
        public HeuristicsError(Exception cause) : base("An error occurred during heuristic selection.", cause) { }
    }

    /// <summary>
    /// Thrown to indicate that a problem occurred during internal plugin recreation.
    /// </summary>
    public sealed class RecreationError : Exception
    {
        public RecreationError(Exception cause) : base("An error occurred while recreating internal plugins.", cause) { }
    }

    /// <summary>
    /// Thrown to indicate that an Npc has no DeathItem and therefore can't be processed by Hunterborn.
    /// @TODO Create a PO3-enhanced Taxonomy spell that can add DeathItems to creatures.
    /// </summary>
    sealed class NoDeathItemException : Exception
    {
        public NoDeathItemException(FormKey form) : base($"No DeathItem: {form}") { }
    }

    /// <summary>
    /// Thrown to indicate that an Npc's DeathItem has already been processed. 
    /// </summary>
    sealed class DeathItemAlreadyAddedException : Exception
    {
        public DeathItemAlreadyAddedException(FormKey form) : base($"DeathItem already processed: {form}") { }
    }

    /// <summary>
    /// Thrown to indicate that one of the forms in Hunterborn.esp couldn't be loaded.
    /// If this happens then something is terribly wrong with Hunterborn.esp.
    /// </summary>
    sealed class CoreRecordMissing : Exception
    {
        public CoreRecordMissing(IFormLinkGetter<ISkyrimMajorRecordGetter> form) : base($"Missing core record: {form} from Hunterborn.esp.")
        {
        }
    }

    /// <summary>
    /// Thrown to indicate that one of the scripts in the main Hunterborn quest couldn't be found.
    /// If this happens then something is terribly wrong with Hunterborn.esp.
    /// </summary>
    sealed class ScriptMissing : Exception
    {
        public ScriptMissing(String scriptedName, String scriptName) : base($"Missing script: {scriptedName}.{scriptName}") { }
    }

    /// <summary>
    /// Thrown to indicate that one of the properties on one of the scripts in the main Hunterborn quest couldn't be found.
    /// If this happens then something is terribly wrong with Hunterborn.esp.
    /// </summary>
    sealed class PropertyMissing : Exception
    {
        public PropertyMissing(String scriptedName, String scriptName, String propertyName) : base($"Missing property: {scriptedName}.{scriptName}.{propertyName}") { }
    }

    /// <summary>
    /// Thrown to indicate that some data in Hunterborn.esp is invalid but in a way that can probably be ignored.
    /// 
    /// The most common cause is that some other patcher (like the zedit one) already made a patch, imperfectly.
    /// 
    /// </summary>
    /// 
    sealed class DataConsistencyError : Exception
    {
        public DataConsistencyError(EntryType type, String name, int index, string msg) : base($"Inconsistent data for {type} #{index}: {name}. {msg}") { }
    }

    sealed public class Queries
    {
        /// <summary>
        /// Scans a plugin for all records of a given type and outputs named FormLink definitions for them.
        /// 
        /// They take the general form:
        /// static readonly public ModKey MY_MOD = new ModKey(FILENAME, ModType.Plugin);
        /// static readonly public FormLink<IMiscItemGetter> EDITORID = new (new FormKey(MY_MOD, 0x000000));
        /// 
        /// </summary>
        /// <typeparam name="T">The type of record to scan and make FormLink definitions for.</typeparam>
        /// <param name="filename">The name of the mod to scan.</param>
        /// <param name="state">The patcher state.</param>
        /// 
        public static void PrintFormKeysDefinitions<T>(String filename, IPatcherState<ISkyrimMod, ISkyrimModGetter> state) where T : IMajorRecordGetter
        {
            state.LoadOrder.TryGetIfEnabledAndExists(new ModKey(filename, ModType.Plugin), out var mod);
            var group = mod?.GetTopLevelGroup<T>();
            if (mod == null || group == null || group.Count == 0) return;

            string modname = $"{Regex.Replace(filename.ToUpper(), "[^a-zA-Z0-9_]", "")}";
            string typeName = typeof(T).FullName ?? "TYPENAME";

            System.Console.WriteLine($"static readonly public ModKey {modname} = new ModKey(\"{filename}\"), ModType.Plugin);");

            group.Where(rec => rec.EditorID != null).ForEach(rec => {
                var edid = rec.EditorID;
                var formID = rec.FormKey.ID.ToString("x");
                Console.WriteLine($"static readonly public FormLink<I{typeName}Getter> {edid} = new (new FormKey({modname}, 0x{formID}));");
            });

        }

        static void QueryImportantProperties(Quest hunterbornQuest)
        {
            hunterbornQuest.VirtualMachineAdapter?.Scripts.ForEach(script => {
                script.Properties
                    .Where(p => p is ScriptObjectListProperty)
                    .Select(p => (p as ScriptObjectListProperty)!)
                    .Where(p => p.Objects.Count >= 10)
                    .ForEach(p => Console.WriteLine($"\tRelevant object array property: {script.Name}.{p.Name}"));

                script.Properties
                    .Where(p => p is ScriptBoolListProperty)
                    .Select(p => (p as ScriptBoolListProperty)!)
                    .Where(p => p.Data.Count >= 10)
                    .ForEach(p => Console.WriteLine($"\tRelevant bool array property: {script.Name}.{p.Name}"));

                script.Properties
                    .Where(p => p is ScriptIntListProperty)
                    .Select(p => (p as ScriptIntListProperty)!)
                    .Where(p => p.Data.Count >= 10)
                    .ForEach(p => Console.WriteLine($"\tRelevant int array property: {script.Name}.{p.Name}"));

                script.Properties
                    .Where(p => p is ScriptFloatListProperty)
                    .Select(p => (p as ScriptFloatListProperty)!)
                    .Where(p => p.Data.Count >= 10)
                    .ForEach(p => Console.WriteLine($"\tRelevant float array property: {script.Name}.{p.Name}"));

                script.Properties
                    .Where(p => p is ScriptStringListProperty)
                    .Select(p => (p as ScriptStringListProperty)!)
                    .Where(p => p.Data.Count >= 10)
                    .ForEach(p => Console.WriteLine($"\tRelevant string array property: {script.Name}.{p.Name}"));
            });
        }
    }

}
