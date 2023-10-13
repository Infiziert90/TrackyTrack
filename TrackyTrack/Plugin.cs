using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using CriticalCommonLib;
using CriticalCommonLib.Services;
using CriticalCommonLib.Services.Ui;
using CriticalCommonLib.Time;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
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
        [PluginService] public static IDataManager Data { get; private set; } = null!;
        [PluginService] public static IFramework Framework { get; private set; } = null!;
        [PluginService] public static ICommandManager Commands { get; private set; } = null!;
        [PluginService] public static DalamudPluginInterface PluginInterface { get; private set; } = null!;
        [PluginService] public static IClientState ClientState { get; private set; } = null!;
        [PluginService] public static IChatGui ChatGui { get; private set; } = null!;
        [PluginService] public static ISigScanner SigScanner { get; private set; } = null!;
        [PluginService] public static IGameInteropProvider Hook { get; private set; } = null!;
        [PluginService] public static IGameGui GameGui { get; private set; } = null!;
        [PluginService] public static IPluginLog Log { get; private set; } = null!;
        [PluginService] public static IAddonLifecycle AddonLifecycle { get; private set; } = null!;
        [PluginService] public static ITextureProvider Texture { get; private set; } = null!;

        public static OdrScanner OdrScanner { get; private set; } = null!;
        public static IGameUiManager GameUi { get; private set; } = null!;
        public static IGameInterface GameInterface { get; private set; } = null!;
        public static IInventoryScanner InventoryScanner { get; private set; } = null!;
        public static ICharacterMonitor CharacterMonitor { get; private set; } = null!;

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
            PluginInterface.Create<Service>();

            Service.SeTime = new SeTime();
            Service.ExcelCache = new ExcelCache(Data, false,false,false);
            Service.ExcelCache.PreCacheItemData();

            GameInterface = new GameInterface(Hook);
            CharacterMonitor = new CharacterMonitor(Framework, ClientState, Service.ExcelCache);

            GameUi = new GameUiManager(Hook);
            OdrScanner = new OdrScanner(CharacterMonitor);
            InventoryScanner = new InventoryScanner(CharacterMonitor, GameUi, GameInterface, OdrScanner, Hook);
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
            Export.Init();

            AddonLifecycle.RegisterListener(AddonEvent.PreFinalize, FrameworkManager.RetainerChecker);
            AddonLifecycle.RegisterListener(AddonEvent.PostSetup, FrameworkManager.RetainerPreChecker);

            InventoryChanged.OnItemAdded += TimerManager.StoreCofferResult;
            InventoryChanged.OnItemAdded += TimerManager.DesynthItemAdded;
            InventoryChanged.OnItemAdded += TimerManager.EurekaItemAdded;
            InventoryChanged.OnItemRemoved += TimerManager.DesynthItemRemoved;

            ClientState.Login += Login;
            ClientState.TerritoryChanged += TerritoryChanged;
        }

        public void Dispose()
        {
            AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, FrameworkManager.RetainerChecker);
            AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, FrameworkManager.RetainerPreChecker);

            ClientState.Login -= Login;
            ClientState.TerritoryChanged -= TerritoryChanged;

            InventoryChanged.OnItemAdded -= TimerManager.StoreCofferResult;
            InventoryChanged.OnItemAdded -= TimerManager.DesynthItemAdded;
            InventoryChanged.OnItemAdded -= TimerManager.EurekaItemAdded;
            InventoryChanged.OnItemRemoved -= TimerManager.DesynthItemRemoved;

            ConfigurationBase.Dispose();
            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.Dispose();

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
                Log.Warning("AgentSalvage was null");
                return;
            }

            // Making sure that we received real items
            if (instance->DesynthItemId == 0)
                return;

            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            character.Storage.History.Add(DateTime.Now, new DesynthResult(instance));
            foreach (var result in instance->DesynthResultsSpan.ToArray().Where(r => r.ItemId != 0))
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

        public void LockboxHandler(uint lockbox, uint itemId, uint amount)
        {
            if (!Configuration.EnableLockboxes)
                return;

            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            // Multiple other items use this handler, like the deep dungeon treasures, so we just add them as we go
            var type = (LockboxTypes) lockbox;
            if (!character.Lockbox.History.ContainsKey(type))
                character.Lockbox.History.Add(type, new Dictionary<uint, uint>());

            character.Lockbox.Opened += 1;
            var lockboxHistory = character.Lockbox.History[type];
            if (!lockboxHistory.TryAdd(itemId, amount))
                lockboxHistory[itemId] += amount;

            ConfigurationBase.SaveCharacterConfig();
            EntryUpload(lockbox, itemId, amount);
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

        #region Uploads
        private void Login()
        {
            // There is a chance that we logged into a different character, so we try to deregister and register it a new
            ClientState.TerritoryChanged -= TerritoryChanged;
            ClientState.TerritoryChanged += TerritoryChanged;

            // Notify the user once about upload opt out
            if (Configuration.UploadNotification)
            {
                // User received the notice, so we schedule the first upload 1h after
                Configuration.UploadNotification = false;
                Configuration.UploadNotificationReceived = DateTime.Now.AddHours(1);
                Configuration.Save();

                ChatGui.Print(Utils.SuccessMessage("Important"));
                ChatGui.Print(Utils.SuccessMessage("This plugin will collect anonymized data. " +
                                                   "For more information on the exact data collected please see the upload tab in the configuration menu. " +
                                                   "You can opt out of any and all forms of data collection."));
            }
        }

        private void TerritoryChanged(ushort _)
        {
            // trigger the warning also for people that just installed it
            if (Configuration.UploadNotification)
                Login();

            if (!Configuration.UploadPermission)
                return;

            // Check that the user had enough time to opt out after notification
            if (Configuration.UploadNotificationReceived > DateTime.Now)
                return;

            try
            {
                var character = CharacterStorage[ClientState.LocalContentId];
                if (character.HadBulkUpload)
                {
                    ClientState.TerritoryChanged -= TerritoryChanged;
                    return;
                }

                character.HadBulkUpload = true;
                ConfigurationBase.SaveCharacterConfig();

                // 32161 Venture Coffers
                Task.Run(() => Export.UploadAll(32161, character.Coffer.Obtained));

                // 36635 Gacha 3.0
                Task.Run(() => Export.UploadAll(36635, character.GachaThreeZero.Obtained));

                // 36636 Gacha 4.0
                Task.Run(() => Export.UploadAll(36636, character.GachaFourZero.Obtained));

                // 41667 Sanctuary
                Task.Run(() => Export.UploadAll(41667, character.GachaSanctuary.Obtained));
            }
            catch (Exception e)
            {
                Log.Error(e, "Upload went wrong, just throw it away");
            }
        }

        public void EntryUpload(uint coffer, uint itemId, uint amount)
        {
            if (Configuration.UploadPermission)
            {
                // Check that the user had enough time to opt out after notification
                if (Configuration.UploadNotificationReceived > DateTime.Now)
                    return;

                try
                {
                    // check if this character had a full upload yet
                    if (!CharacterStorage[ClientState.LocalContentId].HadBulkUpload)
                        return;

                    Task.Run(() => Export.UploadEntry(coffer, itemId, amount));
                }
                catch (Exception e)
                {
                    Log.Error(e, "Upload went wrong, just throw it away");
                }
            }
        }
        #endregion
    }
}
