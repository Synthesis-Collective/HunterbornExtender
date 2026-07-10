using System.IO;
using System.Reflection;
using System.Windows;
using Autofac;
using HunterbornExtender;
using Mutagen.Bethesda.Skyrim;
using Mutagen.Bethesda.Synthesis;
using Noggog.WPF;
using ReactiveUI.Builder;
using static System.Windows.Forms.AxHost;

namespace HunterbornExtenderUI;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        RxAppBuilder.CreateReactiveUIBuilder()
            .WithCoreServices()
            .WithWpf()
            .BuildApp();
        
        SynthesisPipeline.Instance
            .SetOpenForSettings(OpenForSettings)
            .AddPatch<ISkyrimMod, ISkyrimModGetter>(RunPatch)
            .SetTypicalOpen(StandaloneOpen)
            .SetForWpf()
            .Run(e.Args)
            .Wait();
    }

    public static int OpenForSettings(IOpenForSettingsState state)
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<MainModule>();
        builder.RegisterInstance(new OpenForSettingsWrapper(state)).AsImplementedInterfaces();
        var container = builder.Build();
        
        var window = new MainWindow();
        var mainVM = container.Resolve<MainWindowVM>();
        window.DataContext = mainVM;
        if (state.ExtraSettingsDataPath != null)
        {
            mainVM.WelcomePage.ForceSettingsDir(state.ExtraSettingsDataPath.Value);
        }
        mainVM.Init(); // init after setting SettingsDir

        window.Show();
        window.CenterAround(state.RecommendedOpenLocation);

        return 0;
    }

    public static int StandaloneOpen()
    {
        var builder = new ContainerBuilder();
        builder.RegisterModule<MainModule>();
        builder.RegisterType<StandaloneRunStateProvider>().AsImplementedInterfaces();
        var container = builder.Build();
        
        var window = new MainWindow();
        var mainVM = container.Resolve<MainWindowVM>();
        window.DataContext = mainVM;

        var assembly = Assembly.GetEntryAssembly() ?? throw new ArgumentNullException();
        var rootPath = Path.GetDirectoryName(assembly.Location);
        if (rootPath != null)
        {
            mainVM.WelcomePage.ForceSettingsDir(Path.Combine(rootPath, "Settings"));
        }
        mainVM.Init(); // init after setting SettingsDir

        window.Show();
        
        return 0;
    }

    private async Task RunPatch(IPatcherState<ISkyrimMod, ISkyrimModGetter> state)
    {
        HunterbornExtender.Settings.Settings settings = new();
        if (state.ExtraSettingsDataPath != null)
        {
            Write.Success(0, "Loading settings in RunPatch from " + state.ExtraSettingsDataPath);
            settings = PatcherSettingsIO.LoadFromDisk(System.IO.Path.Combine(state.ExtraSettingsDataPath, "settings.json"));
            if (settings != null)
            {
                Write.Success(0, "Loaded settings in RunPatch from " + state.ExtraSettingsDataPath);
            }
        }

        HunterbornExtender.Program.RunPatch(state, settings ?? new HunterbornExtender.Settings.Settings());
    }
}