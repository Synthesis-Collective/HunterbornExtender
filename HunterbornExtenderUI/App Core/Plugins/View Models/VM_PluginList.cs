using System.Collections.ObjectModel;
using DynamicData.Binding;
using Noggog.UI;
using Noggog.WPF;
using ReactiveUI.Fody.Helpers;

namespace HunterbornExtenderUI;

public class VM_PluginList : ViewModel
{
    public VM_PluginList()
    {
        Plugins.ToObservableChangeSet().Subscribe(_ => {
            VisiblePluginCount = Plugins.Where(x => x.IsVisible).Count();
        });
    }
    public ObservableCollection<VM_Plugin> Plugins { get; } = new();
    public int VisiblePluginCount { get; set; }
}