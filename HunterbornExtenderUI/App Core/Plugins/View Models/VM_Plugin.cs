using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using Noggog;
using Noggog.WPF;
using ReactiveUI;
using ReactiveUI.Fody.Helpers;
using HunterbornExtender;
using Noggog.UI;

namespace HunterbornExtenderUI;

public class VM_Plugin : ViewModel
{
    private IStateProvider _state;
    private readonly Func<VM_PluginEntry> _pluginFactory;
    
    public VM_Plugin(
        IStateProvider state,
        Func<VM_PluginEntry> pluginFactory)
    {
        _state = state;
        _pluginFactory = pluginFactory;

        AddEntry = ReactiveCommand.Create(
            () => Entries.Add(_pluginFactory()));

        DeleteEntry = ReactiveCommand.Create<VM_PluginEntry>(
            x => Entries.Remove(x));

        SavePlugin = ReactiveCommand.Create(
            () => SaveToDisk(true));
    }

    public ObservableCollection<VM_PluginEntry> Entries { get; set; } = new();
    [Reactive]
    public VM_PluginEntry? DisplayedEntry { get; set; }
    public Plugin SourceDTO { get; set; } = new();
    public string FilePath { get; set; } = "";
    [Reactive]
    public string FileName { get; set; } = "";
    public ICommand AddEntry { get; }
    public ICommand DeleteEntry { get; }
    public ICommand SavePlugin { get; }
    public bool IsVisible { get; set; } = true;

    public void LoadFromModel(Plugin model)
    {
        SourceDTO = model;
        foreach (var entry in model.Entries)
        {
            var viewModel = new VM_PluginEntry(_state);
            viewModel.LoadFromModel(entry);
            Entries.Add(viewModel);
        }
        FilePath = model.FilePath;
        FileName = Path.GetFileName(FilePath);

        IsVisible = true;
        if (Entries.Any())
        {
            int visibleEntries = 0;
            foreach (var entry in Entries)
            {
                if (entry.IsVisible)
                {
                    visibleEntries++;
                }
            }
            if (visibleEntries == 0) // hide plugins that have no visible entries
            {
                IsVisible = false;
            }
        }

        if (IsVisible && Entries.Where(x => x.IsVisible).Any())
        {
            DisplayedEntry = Entries.Where(x => x.IsVisible).First();
        }
    }

    public Plugin DumpToModel()
    {
        SourceDTO.Entries.Clear();
        foreach (var entry in Entries)
        {
            SourceDTO.Entries.Add(entry.DumpToModel());
        }
        return SourceDTO;
    }

    public void SaveToDisk(bool verbose)
    {
        var model = DumpToModel();
        string savePath = string.Empty;
        if (FilePath != String.Empty)
        {
            savePath = FilePath;
        }
        else
        {
            System.Windows.Forms.SaveFileDialog dialog = new ();

            var pluginsPath = Path.Combine(_state.ExtraSettingsDataPath, "Plugins");

            if (pluginsPath != "")
            {
                Directory.CreateDirectory(pluginsPath); // does nothing if the directory already exists
                dialog.InitialDirectory = pluginsPath;
            }

            dialog.Filter = "JSON|*.json";

            dialog.Title = "Select Save Location";

            var defaultFileName = Path.GetFileNameWithoutExtension(FileName);
            if (!defaultFileName.IsNullOrWhitespace())
            {
                dialog.FileName = defaultFileName;
            }

            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                savePath = dialog.FileName;
                FilePath = savePath;
                FileName = Path.GetFileName(FilePath);
            }
        }

        if (savePath != string.Empty)
        {
            JSONhandler<Plugin>.SaveJSONFile(model, savePath);
            if (verbose)
            {
                MessageBox.Show("Saved to " + FilePath);
            }
        }
    }
}