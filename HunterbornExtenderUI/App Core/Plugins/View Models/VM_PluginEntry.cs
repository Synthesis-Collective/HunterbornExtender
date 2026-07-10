using Mutagen.Bethesda.Plugins;
using Mutagen.Bethesda.Plugins.Cache;
using Mutagen.Bethesda.Skyrim;
using Noggog;
using ReactiveUI.Fody.Helpers;
using System.Collections.ObjectModel;
using Noggog.WPF;
using System.Windows.Input;
using ReactiveUI;
using HunterbornExtender.Settings;
using static System.Windows.Forms.AxHost;
using System;
using System.Windows.Media.Media3D;
using Noggog.UI;

namespace HunterbornExtenderUI;

public class VM_PluginEntry : ViewModel
{
    private IStateProvider _state;
    public VM_PluginEntry(IStateProvider state)
    {
        _state = state;
        LinkCache = state.LinkCache;

        AddMaterial = ReactiveCommand.Create(
            () => Materials.Add(new(_state.LinkCache, this)));
    }

    [Reactive]
    public EntryType Type { get; set; } = EntryType.Monster;
    [Reactive]
    public string Name { get; set; } = "";
    [Reactive]
    public string ProperName { get; set; } = "";
    [Reactive]
    public string SortName { get; set; } = "";
    [Reactive]
    public FormKey Toggle { get; set; }
    [Reactive]
    public FormKey CarcassMessageBox { get; set; }
    [Reactive]
    public int CarcassSize { get; set; } = 1;
    [Reactive]
    public int CarcassWeight { get; set; } = 1;
    [Reactive]
    public int CarcassValue { get; set; } = 1;
    [Reactive]
    public string[] PeltCount { get; set; } = new string[4] { "1", "1", "1", "1" };
    [Reactive]
    public string[] FurPlateCount { get; set; } = new string[4] { "1", "1", "1", "1" };
    [Reactive]
    public FormKey Meat { get; set; }
    [Reactive]
    public ObservableCollection<VM_MaterialEntry> Materials { get; set; } = new();
    [Reactive]
    public ObservableCollection<FormKey> Discard { get; set; } = new();
    [Reactive]
    public FormKey SharedDeathItems { get; set; }
    [Reactive]
    public FormKey BloodType { get; set; }
    [Reactive]
    public FormKey Venom { get; set; }
    [Reactive]
    public FormKey Voice { get; set; }
    [Reactive]
    public string FilePath { get; set; } = "";
    [Reactive]
    public bool CreateDefaultMeat { get; set; } = false;
    [Reactive]
    public bool CreateDefaultPelt { get; set; } = true;
    public ILinkCache LinkCache { get; set; }
    public IEnumerable<Type> AnimalSwitchType { get; } = typeof(IGlobalGetter).AsEnumerable();
    public IEnumerable<Type> CarcassMessageBoxType { get; } = typeof(IMessageGetter).AsEnumerable();
    public IEnumerable<Type> MeatType { get; } = typeof(IIngestibleGetter).AsEnumerable();
    public IEnumerable<Type> NegativeTreasureType { get; } = typeof(IIngestibleGetter).AsEnumerable();
    public IEnumerable<Type> SharedDeathItemsType { get; } = typeof(ILeveledItem).AsEnumerable();
    public IEnumerable<Type> BloodTypeType { get; } = typeof(IIngestibleGetter).AsEnumerable();
    public IEnumerable<Type> VenomType { get; } = typeof(IIngestibleGetter).AsEnumerable();
    public IEnumerable<Type> VoiceType { get; } = typeof(IVoiceTypeGetter).AsEnumerable();

    public ICommand AddMaterial { get; }
    public bool IsVisible { get; set; } = true;

    public void LoadFromModel(PluginEntry model)
    {
        Type = model.Type;
        Name = model.Name;
        ProperName = model.ProperName;
        SortName = model.SortName;
        Toggle = model.Toggle.FormKey;
        CarcassMessageBox = model.CarcassMessageBox.FormKey;
        CarcassSize = model.CarcassSize;
        CarcassValue = model.CarcassValue;
        PeltCount = model.PeltCount.Select(x => x.ToString()).ToArray();
        FurPlateCount = model.FurPlateCount.Select(x => x.ToString()).ToArray();
        Meat = model.Meat.FormKey;
        foreach (var level in model.Materials)
        {
            var matEntry = new VM_MaterialEntry(LinkCache, this);
            matEntry.GetFromModel(level.ToDict());
            Materials.Add(matEntry);
        }
        Discard = new ObservableCollection<FormKey>(model.Discard.Select(x => x.FormKey));
        SharedDeathItems = model.SharedDeathItems.FormKey;
        BloodType = model.BloodType.FormKey;
        Venom = model.Venom.FormKey;
        Voice = model.Voice.FormKey;
        CreateDefaultMeat = model.CreateDefaultMeat;
        CreateDefaultPelt = model.CreateDefaultPelt;

        if (model.HasRequiredMods(_state.LoadOrder))
        {
            IsVisible = true;
        }
        else
        {
            IsVisible = false;
        }
    }

    public PluginEntry DumpToModel()
    {
        var model = new AddonPluginEntry(Type, Name);
        model.ProperName = ProperName;
        model.SortName = SortName;
        model.Toggle = Toggle.ToLinkGetter<IGlobalGetter>();
        model.CarcassMessageBox = CarcassMessageBox.ToLinkGetter<IMessageGetter>();
        model.CarcassSize = CarcassSize;
        model.CarcassValue = CarcassValue;
        model.PeltCount = PeltCount.Select(x => Convert.ToInt32(x)).ToArray();
        model.FurPlateCount = FurPlateCount.Select(x => Convert.ToInt32(x)).ToArray();
        model.Meat = Meat.ToLinkGetter<IItemGetter>();
        foreach (var entry in Materials)
        {
            model.Materials.Add(MaterialLevel.FromDict(entry.DumpToModel()));
        }
        model.Discard = Discard.Select(x => x.ToLinkGetter<IItemGetter>()).ToList();
        model.SharedDeathItems = SharedDeathItems.ToLinkGetter<IFormListGetter>();
        model.BloodType = BloodType.ToLinkGetter<IItemGetter>();
        model.Venom = Venom.ToLinkGetter<IItemGetter>();
        model.Voice = Voice.ToLinkGetter<IVoiceTypeGetter>();
        model.CreateDefaultMeat = CreateDefaultMeat;
        model.CreateDefaultPelt = CreateDefaultPelt;
        return model;
    }
}

public class VM_MaterialEntry : ViewModel
{
    [Reactive]
    public ObservableCollection<VM_Material> Items { get; set; } = new();
    public ILinkCache LinkCache { get; set; }
    public ICommand AddItem { get; }
    public ICommand DeleteMe { get; }
    public VM_PluginEntry Parent { get; set; }

    public VM_MaterialEntry(ILinkCache linkCache, VM_PluginEntry parent)
    {
        LinkCache = linkCache;
        Parent = parent;
        AddItem = ReactiveCommand.Create(
            () => Items.Add(new(linkCache, this)));
        DeleteMe = ReactiveCommand.Create(
            () => Parent.Materials.Remove(this));
    }

    public void GetFromModel(Dictionary<IFormLinkGetter<IItemGetter>, int> model)
    {
        foreach (var key in model.Keys)
        {
            VM_Material mat = new(LinkCache, this);
            mat.Key = key.FormKey;
            mat.Value = model[key];
            Items.Add(mat);
        }
    }

    public Dictionary<IFormLinkGetter<IItemGetter>, int> DumpToModel()
    {
        Dictionary<IFormLinkGetter<IItemGetter>, int> model = new();
        foreach (var item in Items)
        {
            var keyLink = item.Key.ToLinkGetter<IItemGetter>();
            if (!model.ContainsKey(keyLink))
            {
                model.Add(keyLink, item.Value);
            }
        }
        return model;
    }
}

public class VM_Material : ViewModel
{
    public ILinkCache LinkCache { get; set; }
    public IEnumerable<Type> MatType { get; } = typeof(IMiscItemGetter).AsEnumerable().And(typeof(IIngredientGetter));
    public VM_MaterialEntry Parent { get; set; }
    public ICommand DeleteMe { get; }

    public VM_Material(ILinkCache linkCache, VM_MaterialEntry parent)
    {
        LinkCache = linkCache;
        Parent = parent;

        DeleteMe = ReactiveCommand.Create(
            () => Parent.Items.Remove(this));
    }

    //[Reactive]
    public FormKey Key { get; set; } = new();
    [Reactive]
    public int Value { get; set; } = 1;

}

public enum EntryTypeClone// duplicated here until I can figure out how to reference the one in HunterbornExtender.Settings in xaml
{
    Animal,
    Monster
}
