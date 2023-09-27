using System.Collections.Concurrent;
using System.Reflection;
using CriticalCommonLib;
using CriticalCommonLib.Services;
using CriticalCommonLib.Services.Ui;
using CriticalCommonLib.Time;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using FFXIVClientStructs.FFXIV.Client.Game;
using TrackyTrack.Attributes;
using TrackyTrack.Data;
using TrackyTrack.Windows.Main;
using TrackyTrack.Windows.Config;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using TrackyTrack.Lib;
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

        public static OdrScanner OdrScanner { get; private set; } = null!;
        public static IGameUiManager GameUi { get; private set; } = null!;
        public static IGameInterface GameInterface { get; private set; } = null!;
        public static IInventoryScanner InventoryScanner { get; private set; } = null!;
        public static ICharacterMonitor CharacterMonitor { get; private set; } = null!;
        public static AddonController AddonController { get; private set; } = null!;

        public string Name => "Tracky Track";

        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("Tracky Track");

        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        public static readonly string Authors = "Infi";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        private readonly PluginCommandManager<Plugin> CommandManager;

        public ConfigurationBase ConfigurationBase;
        public ConcurrentDictionary<ulong, CharacterConfiguration> CharacterStorage = new();

        public TimerManager TimerManager;
        public FrameworkManager FrameworkManager;
        private HookManager HookManager;
        private InventoryChanged InventoryChanged;

        public Plugin()
        {
            ConfigurationBase = new ConfigurationBase(this);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            TexturesCache.Initialize();

            PluginInterface.Create<Service>();

            Service.SeTime = new SeTime();
            Service.ExcelCache = new ExcelCache(Data, false,false,false);
            Service.ExcelCache.PreCacheItemData();

            GameInterface = new GameInterface();
            CharacterMonitor = new CharacterMonitor();

            GameUi = new GameUiManager();
            OdrScanner = new OdrScanner(CharacterMonitor);
            InventoryScanner = new InventoryScanner(CharacterMonitor, GameUi, GameInterface, OdrScanner);
            InventoryScanner.Enable();

            InventoryChanged = new InventoryChanged();

            TimerManager = new TimerManager(this);
            HookManager = new HookManager(this);
            FrameworkManager = new FrameworkManager(this);

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this, Configuration);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            CommandManager = new PluginCommandManager<Plugin>(this, Commands);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            ConfigurationBase.Load();

            AddonController = new AddonController();
            AddonController.AddonFinalize += FrameworkManager.RetainerChecker;
            AddonController.AddonPostSetup += FrameworkManager.RetainerPreChecker;

            InventoryChanged.OnItemAdded += TimerManager.StoreCofferResult;
            InventoryChanged.OnItemAdded += TimerManager.DesynthItemAdded;
            InventoryChanged.OnItemAdded += TimerManager.EurekaItemAdded;
            InventoryChanged.OnItemRemoved += TimerManager.DesynthItemRemoved;
        }

        public void Dispose()
        {
            AddonController.AddonFinalize -= FrameworkManager.RetainerChecker;
            AddonController.AddonPostSetup -= FrameworkManager.RetainerPreChecker;

            InventoryChanged.OnItemAdded -= TimerManager.StoreCofferResult;
            InventoryChanged.OnItemAdded -= TimerManager.DesynthItemAdded;
            InventoryChanged.OnItemAdded -= TimerManager.EurekaItemAdded;
            InventoryChanged.OnItemRemoved -= TimerManager.DesynthItemRemoved;

            ConfigurationBase.Dispose();
            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.Dispose();

            TexturesCache.Instance?.Dispose();

            InventoryScanner.Dispose();
            OdrScanner.Dispose();
            GameUi.Dispose();
            CharacterMonitor.Dispose();
            GameInterface.Dispose();
            Service.Dereference();

            InventoryChanged.Dispose();

            HookManager.Dispose();
            TimerManager.Dispose();
            FrameworkManager.Dispose();
        }

        [Command("/ttracker")]
        [Aliases("/tracky")]
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
            if (GameGui.GetAddonByName("SalvageAutoDialog") != nint.Zero)
                TimerManager.StartBulk();
        }

        public unsafe void DesynthHandler()
        {
            // We have to return whenever we see bulk happening
            if (GameGui.GetAddonByName("SalvageAutoDialog") != nint.Zero)
                return;

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
                if (!character.Storage.Total.TryAdd(id, (uint) result.Quantity))
                    character.Storage.Total[id] += (uint) result.Quantity;
            }

            ConfigurationBase.SaveCharacterConfig();
        }

        public unsafe void RetainerHandler(uint venture, VentureItem primary, VentureItem additional)
        {
            if (!Configuration.EnableRetainer)
                return;

            var retainer = RetainerManager.Instance();
            if (retainer == null)
                return;

            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            var isMaxLevel = retainer->GetActiveRetainer()->Level == 90;
            character.VentureStorage.History.Add(DateTime.Now, new VentureResult(venture, new List<VentureItem>{primary, additional}, isMaxLevel));

            ConfigurationBase.SaveCharacterConfig();
        }

        public void CurrencyHandler(Currency currency, int increase)
        {
            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            switch (currency)
            {
                case Currency.StormSeals or Currency.SerpentSeals or Currency.FlameSeals:
                    character.GCSeals += (uint) increase;
                    break;
                case Currency.AlliedSeals:
                    character.AlliedSeals += (uint) increase;
                    break;
                case Currency.MGP:
                    character.MGP += (uint) increase;
                    break;
                case Currency.Bicolor:
                    character.Bicolor += (uint) increase;
                    break;
                case Currency.SackOfNuts:
                    character.SackOfNuts += (uint) increase;
                    break;
                case Currency.CenturioSeals:
                    character.CenturioSeal += (uint) increase;
                    break;
                case Currency.Ventures:
                    character.VentureCoins += (uint) increase;
                    break;
                case Currency.Skybuilders:
                    character.Skybuilder += (uint) increase;
                    break;
            }
            ConfigurationBase.SaveCharacterConfig();
        }

        public void TeleportCostHandler(uint cost)
        {
            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            character.TeleportCost += cost;
            character.Teleports += 1;
            ConfigurationBase.SaveCharacterConfig();
        }

        public void AetheryteTicketHandler()
        {
            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            character.TeleportsAetheryte += 1;
            character.Teleports += 1;
            ConfigurationBase.SaveCharacterConfig();
        }

        public void CastedTicketHandler(uint ticketId)
        {
            TimerManager.StartTicketUsed();

            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            switch (ticketId)
            {
                case 21069 or 21070 or 21071:
                    character.TeleportsGC += 1;
                    break;
                case 30362:
                    character.TeleportsVesperBay += 1;
                    break;
                case 28064:
                    character.TeleportsVesperBay += 1;
                    break;
            }
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
