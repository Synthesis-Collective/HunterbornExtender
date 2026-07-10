using Noggog.WPF;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using HunterbornExtender;
using System.Reflection;
using System.Windows.Input;
using DynamicData.Binding;
using System.Collections.ObjectModel;
using Noggog.UI;

namespace HunterbornExtenderUI;

public class VM_WelcomePage : ViewModel
{
    public VM_PluginList PluginList { get; }
    [Reactive]
    public string SettingsDir { get; set; } = String.Empty;
    public ICommand SetSettingsDir { get; }
    public bool DebuggingMode { get; set; } = true;

    public bool ReuseSelections { get; set; } = true;

    public bool AdvancedTaxonomy { get; set; } = true;

    public bool QuickLootPatch { get; set; } = true;
    public VM_WelcomePage(VM_PluginList pluginList)
    {
        //Temporary directory setting code until environment creation is done via Synthesis
        string exeLocation = string.Empty;
        var assembly = Assembly.GetEntryAssembly();
        if (assembly != null && assembly.Location != null)
        {
            exeLocation = System.IO.Path.GetDirectoryName(assembly.Location) ?? string.Empty;
        }
        var saveInfoPath = System.IO.Path.Combine(exeLocation, "SavePath.json");
        if (System.IO.File.Exists(saveInfoPath))
        {
            var saveInfo = JSONhandler<string>.LoadJSONFile(saveInfoPath, out _);
            if (saveInfo != null)
            {
                SettingsDir = saveInfo;
            }
        }

        this.WhenAnyValue(x => x.SettingsDir).Subscribe(_ =>
        {
            JSONhandler<string>.SaveJSONFile(SettingsDir, saveInfoPath);
        });

        SetSettingsDir = ReactiveCommand.Create(() => 
            {
                System.Windows.Forms.FolderBrowserDialog dialog = new System.Windows.Forms.FolderBrowserDialog();
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    SettingsDir = dialog.SelectedPath;
                }
            });
        // end directory setting

        PluginList = pluginList;
    }

    public void ForceSettingsDir(string settingsDir)
    {
        SettingsDir = settingsDir;
    }

    public void Init(SettingsProvider settingsProvider)
    {
        DebuggingMode = settingsProvider.PatcherSettings.DebuggingMode;
        ReuseSelections = settingsProvider.PatcherSettings.ReuseSelections;
        AdvancedTaxonomy = settingsProvider.PatcherSettings.AdvancedTaxonomy;
        QuickLootPatch = settingsProvider.PatcherSettings.QuickLootPatch;
    }

    public void SaveSettings(SettingsProvider settingsProvider)
    {
        settingsProvider.PatcherSettings.DebuggingMode = DebuggingMode;
        settingsProvider.PatcherSettings.ReuseSelections = ReuseSelections;
        settingsProvider.PatcherSettings.AdvancedTaxonomy = AdvancedTaxonomy;
        settingsProvider.PatcherSettings.QuickLootPatch = QuickLootPatch;
    }
}