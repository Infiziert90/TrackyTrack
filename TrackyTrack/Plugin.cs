using System.Reflection;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using TrackyTrack.Attributes;
using TrackyTrack.Data;
using TrackyTrack.IPC;
using TrackyTrack.Windows.Main;
using TrackyTrack.Windows.Config;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using TrackyTrack.Manager;

namespace TrackyTrack
{
    public class Plugin : IDalamudPlugin
    {
        [PluginService] public static DataManager Data { get; private set; } = null!;
        [PluginService] public static Framework Framework { get; private set; } = null!;
        [PluginService] public static CommandManager Commands { get; private set; } = null!;
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static ClientState ClientState { get; private set; } = null!;
        [PluginService] public static ChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static SigScanner SigScanner { get; private set; } = null!;
        [PluginService] public static GameGui GameGui { get; private set; } = null!;

        public string Name => "Tracky Track";

        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("Tracky Track");

        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        public static readonly string Authors = "Infi";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        private readonly PluginCommandManager<Plugin> CommandManager;

        public ConfigurationBase ConfigurationBase;
        public Dictionary<ulong, CharacterConfiguration> CharacterStorage = new();

        public static AllaganToolsConsumer AllaganToolsConsumer = null!;
        private TimerManager TimerManager;
        private HookManager HookManager;

        public Plugin()
        {
            ConfigurationBase = new ConfigurationBase(this);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            TexturesCache.Initialize();

            AllaganToolsConsumer = new AllaganToolsConsumer();
            TimerManager = new TimerManager(this);
            HookManager = new HookManager(this);

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this, Configuration);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            CommandManager = new PluginCommandManager<Plugin>(this, Commands);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            ConfigurationBase.Load();

            Framework.Update += CofferTracker;
        }

        public void Dispose()
        {
            ConfigurationBase.Dispose();
            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.Dispose();

            TexturesCache.Instance?.Dispose();

            HookManager.Dispose();
            TimerManager.Dispose();
            AllaganToolsConsumer.Dispose();
            Framework.Update -= CofferTracker;
        }

        [Command("/ttracker")]
        [HelpMessage("Opens the tracker")]
        private void OnCommand(string command, string args)
        {
            MainWindow.IsOpen ^= true;
        }

        [Command("/tconf")]
        [HelpMessage("Opens the config")]
        private void OnConfigCommand(string command, string args)
        {
            ConfigWindow.IsOpen ^= true;
        }

        public void BulkHandler()
        {
            var addonPtr = GameGui.GetAddonByName("SalvageAutoDialog", 1);
            if (addonPtr != nint.Zero)
            {
                if (!AllaganToolsConsumer.SubscribeAddEvent("DesynthItemAdded", TimerManager.DesynthItemAdded))
                    return;

                if (!AllaganToolsConsumer.SubscribeRemoveEvent("DesynthItemRemoved", TimerManager.DesynthItemRemoved))
                    return;

                TimerManager.StartBulk();
            }
        }

        public unsafe void DesynthHandler()
        {
            var instance = AgentSalvage.Instance();
            if (instance == null)
            {
                PluginLog.Warning("AgentSalvage was null");
                return;
            }

            // Making sure that we received real items
            if (instance->DesynthItemId == 0)
                return;

            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            character.Storage.History.Add(DateTime.Now, new DesynthResult(instance));
            foreach (var result in instance->DesynthResultSpan.ToArray().Where(r => r.ItemId != 0))
            {
                var id  = result.ItemId > 1_000_000 ? result.ItemId - 1_000_000 : result.ItemId;
                if (!character.Storage.Total.TryAdd(id, (uint)result.Quantity))
                    character.Storage.Total[id] += (uint)result.Quantity;
            }

            ConfigurationBase.SaveCharacterConfig();
        }

        public void CofferTracker(Framework _)
        {
            // Both coffers disabled, we don't need to track
            if (Configuration is { EnableVentureCoffers: false, EnableGachaCoffers: false })
                return;

            var local = ClientState.LocalPlayer;
            if (local == null || !local.IsCasting)
                return;

            if (!AllaganToolsConsumer.SubscribeAddEvent("CofferItemAdded", TimerManager.StoreCofferResult))
                return;

            if (local is { CastActionId: 32161, CastActionType: 2 } or { CastActionId: 36635, CastActionType: 2 } or { CastActionId: 36636, CastActionType: 2 })
                TimerManager.StartCast();
        }

        public void TeleportCostHandler(uint cost)
        {
            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            character.TeleportCost += cost;
            character.Teleports += 1;
            ConfigurationBase.SaveCharacterConfig();
        }

        public void RepairHandler(uint repairs)
        {
            TimerManager.Repaired = repairs;
            TimerManager.StartRepair();
        }

        #region Draws
        private void DrawUI()
        {
            this.WindowSystem.Draw();
        }

        public void DrawConfigUI()
        {
            ConfigWindow.IsOpen = true;
        }
        #endregion
    }
}
