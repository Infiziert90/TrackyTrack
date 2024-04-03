using System.Collections.Concurrent;
using System.Reflection;
using System.Threading.Tasks;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Game;
using Dalamud.Game.Addon.Lifecycle;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Windowing;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using TrackyTrack.Attributes;
using TrackyTrack.Data;
using TrackyTrack.Windows.Main;
using TrackyTrack.Windows.Config;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
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
        [PluginService] public static IGameInventory GameInventory { get; private set; } = null!;
        [PluginService] public static INotificationManager NotificationManager { get; private set; } = null!;

        public static FileDialogManager FileDialogManager { get; private set; } = null!;

        public Configuration Configuration { get; init; }

        public readonly WindowSystem WindowSystem = new("Tracky");
        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        public const string Authors = "Infi";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        private readonly PluginCommandManager<Plugin> CommandManager;

        public ConfigurationBase ConfigurationBase;
        public ConcurrentDictionary<ulong, CharacterConfiguration> CharacterStorage = new();

        public readonly TimerManager TimerManager;
        public readonly FrameworkManager FrameworkManager;
        private readonly HookManager HookManager;
        private readonly InventoryChanged InventoryChanged;

        public readonly Importer Importer;

        public Plugin()
        {
            ConfigurationBase = new ConfigurationBase(this);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            FileDialogManager = new FileDialogManager();

            Importer = new Importer();
            Importer.Load();

            TimerManager = new TimerManager(this);
            HookManager = new HookManager(this);
            FrameworkManager = new FrameworkManager(this);
            InventoryChanged = new InventoryChanged();

            InventoryChanged.OnItemsChanged += TimerManager.StoreCofferResult;
            InventoryChanged.OnItemsChanged += TimerManager.StoreEurekaResult;

            InventoryChanged.OnItemAdded += TimerManager.DesynthItemAdded;

            InventoryChanged.OnItemRemoved += TimerManager.DesynthItemRemoved;
            InventoryChanged.OnItemRemoved += FragmentRemoved;

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

            ClientState.Login += Login;
            ClientState.TerritoryChanged += TerritoryChanged;

            Login();

            // Delay load and save tasks, ensuring that everything has loaded
            ConfigurationBase.StartTasks();
        }

        public void Dispose()
        {
            AddonLifecycle.UnregisterListener(AddonEvent.PreFinalize, FrameworkManager.RetainerChecker);
            AddonLifecycle.UnregisterListener(AddonEvent.PostSetup, FrameworkManager.RetainerPreChecker);

            ClientState.Login -= Login;
            ClientState.TerritoryChanged -= TerritoryChanged;

            InventoryChanged.OnItemsChanged -= TimerManager.StoreCofferResult;
            InventoryChanged.OnItemsChanged -= TimerManager.StoreEurekaResult;

            InventoryChanged.OnItemAdded -= TimerManager.DesynthItemAdded;

            InventoryChanged.OnItemRemoved -= TimerManager.DesynthItemRemoved;
            InventoryChanged.OnItemRemoved -= FragmentRemoved;

            ConfigurationBase.Dispose();
            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.Dispose();

            HookManager.Dispose();
            TimerManager.Dispose();
            FrameworkManager.Dispose();
            InventoryChanged.Dispose();
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

            var desynthResult = new DesynthResult(instance);
            character.Storage.History.Add(DateTime.Now, desynthResult);
            foreach (var result in desynthResult.Received)
            {
                if (!character.Storage.Total.TryAdd(result.Item, result.Count))
                    character.Storage.Total[result.Item] += result.Count;
            }

            ConfigurationBase.SaveCharacterConfig();
            UploadEntry(new Export.DesynthesisResult(desynthResult));
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

        public static TeleportBuff GetCurrentTeleportBuff()
        {
            // LocalPlayer can be null in some cases, like loading screens
            return ClientState.LocalPlayer == null ? TeleportBuff.None : TeleportBuffExtension.FromStatusList(ClientState.LocalPlayer.StatusList);
        }

        public void TeleportCostHandler(uint cost)
        {
            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            // Record the current teleport buff and savings
            var buff = GetCurrentTeleportBuff();
            var originalCost = buff.ToOriginalCost(cost);
            var savings = originalCost - cost;
            character.TeleportsWithBuffs.TryAdd(buff, 0);
            character.TeleportSavingsWithBuffs.TryAdd(buff, 0);
            character.TeleportsWithBuffs[buff] += 1;
            if (savings > 0)
                character.TeleportSavingsWithBuffs[buff] += savings;

            character.TeleportCost += cost;
            character.Teleports += 1;
            ConfigurationBase.SaveCharacterConfig();

            Log.Debug($"Teleported for {cost} gil (saved {savings} gil) with savings buff {buff.ToName()}");
            Log.Debug($"Teleported {character.Teleports} times with a total cost of {character.TeleportCost} gil");
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
            character.Lockbox.History.TryAdd(type, new Dictionary<uint, uint>());

            character.Lockbox.Opened += 1;
            var lockboxHistory = character.Lockbox.History[type];
            if (!lockboxHistory.TryAdd(itemId, amount))
                lockboxHistory[itemId] += amount;

            ConfigurationBase.SaveCharacterConfig();
            UploadEntry(new Export.GachaLoot(lockbox, itemId, amount));
        }

        // Fragments need special treatment to be registered
        public void FragmentRemoved((uint ItemId, uint Quantity) changedItem)
        {
            HookManager.LastSeenItemId = uint.MaxValue;
            if (!Lockboxes.Fragments.Contains(changedItem.ItemId))
                return;

            if (changedItem.Quantity > 1)
                return;

            HookManager.LastSeenItemId = Utils.NormalizeItemId(changedItem.ItemId);
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
                ChatGui.Print(Utils.SuccessMessage("This plugin will collect and upload anonymized data. " +
                                                   "For more information on the exact data collected please see the upload tab in the configuration menu. " +
                                                   "You can opt out of any and all forms of data collection."));
            }
        }

        private void TerritoryChanged(ushort _)
        {
            // trigger the warning also for people that just installed it
            if (Configuration.UploadNotification)
                Login();

            if (!CheckUploadPermissions())
                return;

            try
            {
                var character = CharacterStorage[ClientState.LocalContentId];
                if (character.HadDesynthUpload)
                {
                    ClientState.TerritoryChanged -= TerritoryChanged;
                    return;
                }

                character.HadDesynthUpload = true;
                ConfigurationBase.SaveCharacterConfig();

                // Desynthesis
                Task.Run(async () =>
                {
                    foreach (var (source, rewards) in character.Storage.History.Values)
                    {
                        // Delay to prevent too many uploads in a short time
                        await Task.Delay(30);

                        var r = new List<uint>();
                        foreach (var reward in rewards)
                            r.AddRange(reward.ItemCountArray());

                        Export.UploadEntry(new Export.DesynthesisResult(source, r.ToArray()));
                    }
                });
            }
            catch (Exception e)
            {
                Log.Error(e, "Upload went wrong, just throw it away");
            }
        }

        public void UploadEntry(Export.Upload entry)
        {
            if (!CheckUploadPermissions())
                return;

            Task.Run(() => Export.UploadEntry(entry));
        }

        public bool CheckUploadPermissions()
        {
            // Check that the user had enough time to opt out after notification
            return Configuration.UploadPermission && Configuration.UploadNotificationReceived < DateTime.Now;
        }
        #endregion
    }
}
