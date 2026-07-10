using System.Windows;
using System.Windows.Input;
using HunterbornExtenderUI.App_Core.Death_Item_Selection;
using Noggog;
using Noggog.WPF;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using HunterbornExtender.Settings;
using Noggog.UI;

namespace HunterbornExtenderUI;

public class MainWindowVM : ViewModel
{
    private PluginLoader _pluginLoader;
    private readonly VMLoader_Plugins _vmPluginLoader;
    private readonly VM_PluginList _pluginList;
    private readonly VMLoader_DeathItems _vmDeathItemLoader;
    private readonly VM_DeathItemSelectionList _deathItemSelectionList;
    private SettingsProvider _settingsProvider;
    private readonly PatcherSettingsIO _patcherSettingsIO;

    [Reactive]
    public object DisplayedSubView { get; set; }
    public VM_WelcomePage WelcomePage { get; }
    public VM_DeathItemAssignmentPage DeathItemMenu { get; }
    public VM_PluginEditorPage PluginEditorPage { get; }

    public ICommand ClickDeathItemAssignment { get; }
    public ICommand ClickPluginsMenu { get; }
    public ICommand ClickWelcomePage { get; }
    public ICommand SaveSettings { get; }

    public MainWindowVM(
        PatcherSettingsIO patcherSettingsIO,
        SettingsProvider settingsProvider,
        PluginLoader pluginLoader,
        VM_WelcomePage welcomePage,
        VM_PluginEditorPage pluginEditorPage,
        VMLoader_Plugins vmPluginLoader,
        VMLoader_DeathItems deathItemLoader,
        VM_PluginList pluginList,
        VM_DeathItemSelectionList deathItemSelectionList,
        VM_DeathItemAssignmentPage deathItemMenu)
    {
        _patcherSettingsIO = patcherSettingsIO;
        _settingsProvider = settingsProvider;
        _pluginLoader = pluginLoader;

        _pluginList = pluginList;
        _deathItemSelectionList = deathItemSelectionList;

        _vmPluginLoader = vmPluginLoader;
        _vmDeathItemLoader = deathItemLoader;

        WelcomePage = welcomePage;
        PluginEditorPage = pluginEditorPage;
        DeathItemMenu = deathItemMenu;

        DisplayedSubView = WelcomePage;

        ClickDeathItemAssignment = ReactiveCommand.Create(
            () => DisplayedSubView = DeathItemMenu);

        ClickPluginsMenu = ReactiveCommand.Create(
            () => DisplayedSubView = PluginEditorPage);
        ClickWelcomePage = ReactiveCommand.Create(
            () => DisplayedSubView = WelcomePage);

        SaveSettings = ReactiveCommand.Create(() =>
        {
            SaveSettingsMethod(true);
        });

        Application.Current.Exit += OnApplicationExit;
    }
    
    public void Init()
    {
        WelcomePage.Init(_settingsProvider);

        _pluginList.Plugins.SetTo(
            _vmPluginLoader.GetPluginVMs(
                _pluginLoader.LoadPlugins()));
        if (_pluginList.Plugins.Where(x => x.IsVisible).Any())
        {
            PluginEditorPage.DisplayedPlugin = _pluginList.Plugins.Where(x => x.IsVisible).First();
        }

        /*
        _deathItemSelectionList.DeathItems.SetTo(
            _vmDeathItemLoader.GetDeathItemVMs(
                _deathItemLoader.LoadDeathItemSettings()));
        */
        DeathItemMenu.Initialize();
    }

    private void OnApplicationExit(object sender, ExitEventArgs e)
    {
        // Perform any necessary cleanup or save data here
        SaveSettingsMethod(false);
    }

    private void SaveSettingsMethod(bool withClick)
    {
        if (WelcomePage.SettingsDir != string.Empty)
        {
            WelcomePage.SaveSettings(_settingsProvider);
            _patcherSettingsIO.DumpToSettings(_deathItemSelectionList, _pluginList);
            _patcherSettingsIO.SaveToDisk(WelcomePage.SettingsDir);

            foreach (var plugin in _pluginList.Plugins)
            {
                plugin.SaveToDisk(false);   
            }
            if (withClick)
            {
                MessageBox.Show("Saved to " + System.IO.Path.Combine(WelcomePage.SettingsDir, "settings.json"));
            }
        }
        else
        {
            MessageBox.Show("Cannot save settings until a valid output directory is set.");
        }
    }
}