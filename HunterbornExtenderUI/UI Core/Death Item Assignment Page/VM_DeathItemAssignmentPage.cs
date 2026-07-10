using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using Noggog.WPF;
using Mutagen.Bethesda.Plugins.Order;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda;
using DynamicData;
using Noggog;
using HunterbornExtenderUI.App_Core.Death_Item_Selection;
using HunterbornExtender.Settings;
using HunterbornExtender;
using System.Windows;
using HunterbornExtender.IO;
using Noggog.UI;

namespace HunterbornExtenderUI;

sealed public class VM_DeathItemAssignmentPage : ViewModel
{
    private IStateProvider _stateProvider;
    private VM_PluginList _pluginVMList;
    private ObservableCollection<PluginEntry> _pluginEntries = new();
    private readonly SettingsProvider _settingsProvider;
    private readonly VMLoader_DeathItems _vmDeathItemLoader;

    [Reactive]
    public ObservableCollection<string> CreatureTypes { get; set; } = new();

    [Reactive]
    public VM_DeathItemSelectionList DeathItemSelectionList { get; set; }
    public VM_DeathItemAssignmentPage(IStateProvider stateProvider, SettingsProvider settingsProvider, VM_PluginList pluginList, VM_DeathItemSelectionList deathItemList, VMLoader_DeathItems deathItemVMLoader)
    {
        _stateProvider = stateProvider;
        _settingsProvider = settingsProvider;
        _pluginVMList = pluginList;
        DeathItemSelectionList = deathItemList;
        _vmDeathItemLoader = deathItemVMLoader;
    }

    public void Initialize()
    {
        try
        {
            ScanForPluginEntries();
            RaceLinkNamer.State = _stateProvider;
            SelectionLinker.LinkDeathItemSelections(_settingsProvider.PatcherSettings.DeathItemSelections, _pluginEntries);
            var deathItems = Heuristics.MakeHeuristicSelections(_stateProvider.LoadOrder.PriorityOrder.OnlyEnabledAndExisting().WinningOverrides<INpcGetter>(), _pluginEntries.ToList(), _settingsProvider.PatcherSettings.DeathItemSelections, _stateProvider.LinkCache);
            DeathItemSelectionList.DeathItems.SetTo(_vmDeathItemLoader.GetDeathItemVMs(deathItems).Where(x => x.DeathItem != null));
        }
        catch (Exception ex) when (ex is RecreationError || ex is HeuristicsError)
        {
            MessageBox.Show($"{ex.Message}\n{ex.InnerException?.Message}", "Alert", MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine(ex.ToString());
        }
        catch (HeuristicsError ex)
        {
            MessageBox.Show($"{ex.Message}", "Alert", MessageBoxButton.OK, MessageBoxImage.Error);
            Console.WriteLine(ex.ToString());
        }
    }
        
    private void ScanForPluginEntries()
    {
        _pluginEntries = new(RecreateInternal.RecreateInternalPluginsUI(_stateProvider.LinkCache, true));

        foreach (var pluginEntry in _pluginVMList.Plugins.SelectMany(x => x.Entries).Select(x => x.DumpToModel()))
        {
            _pluginEntries.Add(pluginEntry);
        }

        CreatureTypes = new(_pluginEntries.Select(x => x.SortName));
        CreatureTypes.Sort(x => x, false);
    }
}