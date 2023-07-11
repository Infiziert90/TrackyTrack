using System.Reflection;
using System.Timers;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
using Dalamud.Game.Gui;
using Dalamud.Hooking;
using Dalamud.Interface.Windowing;
using Dalamud.Logging;
using TrackyTrack.Attributes;
using TrackyTrack.Data;
using TrackyTrack.IPC;
using TrackyTrack.Windows.Main;
using TrackyTrack.Windows.Config;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

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

        private static readonly string DesynthResultSig = "E8 ?? ?? ?? ?? E9 ?? ?? ?? ?? 44 0F B6 43 ?? 4C 8D 4B 17";
        private delegate void DesynthResultDelegate(uint param1, ushort param2, sbyte param3, Int64 param4, char param5);
        private Hook<DesynthResultDelegate> DesynthResultHook;

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

        private BulkResult LastBulkResult = new();
        private Timer FinishedBulkDesynth = new(1 * 1000);

        private Timer CastTimer = new(3 * 1000);
        private bool OpeningCoffer;

        public static AllaganToolsConsumer AllaganToolsConsumer = null!;

        public Plugin()
        {
            ConfigurationBase = new ConfigurationBase(this);

            Configuration = PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            Configuration.Initialize(PluginInterface);

            TexturesCache.Initialize();

            AllaganToolsConsumer = new AllaganToolsConsumer();

            ConfigWindow = new ConfigWindow(this);
            MainWindow = new MainWindow(this, Configuration);

            WindowSystem.AddWindow(ConfigWindow);
            WindowSystem.AddWindow(MainWindow);

            CommandManager = new PluginCommandManager<Plugin>(this, Commands);

            PluginInterface.UiBuilder.Draw += DrawUI;
            PluginInterface.UiBuilder.OpenConfigUi += DrawConfigUI;

            ConfigurationBase.Load();

            var desynthResultPtr = SigScanner.ScanText(DesynthResultSig);
            DesynthResultHook = Hook<DesynthResultDelegate>.FromAddress(desynthResultPtr, DesynthResultPacket);
            DesynthResultHook.Enable();

            FinishedBulkDesynth.AutoReset = false;
            FinishedBulkDesynth.Elapsed += StoreBulkResult;

            CastTimer.AutoReset = false;
            CastTimer.Elapsed += (_, _) => OpeningCoffer = false;

            Framework.Update += CofferTracker;
        }

        public void Dispose() => Dispose(true);

        public void Dispose(bool full)
        {
            ConfigurationBase.Dispose();
            WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.Dispose();

            TexturesCache.Instance?.Dispose();

            if (full)
            {
                AllaganToolsConsumer.Dispose();
                DesynthResultHook.Dispose();

                Framework.Update -= CofferTracker;
            }
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

        public unsafe void DesynthResultPacket(uint param1, ushort param2, sbyte param3, Int64 param4, char param5)
        {
            DesynthResultHook.Original(param1, param2, param3, param4, param5);

            // DesynthResult is triggered by multiple events
            if (param1 != 3735552)
            {
                PluginLog.Error("Received param1 that isn't DesynthResult");
                PluginLog.Error($"Param1 {param1}");
                return;
            }

            // We have to handle Bulk Desynthesis extra
            if (Configuration.EnableBulkSupport)
            {
                var addonPtr = GameGui.GetAddonByName("SalvageAutoDialog", 1);
                if (addonPtr != nint.Zero)
                {
                    if (!AllaganToolsConsumer.SubscribeAddEvent("DesynthItemAdded", DesynthItemAdded))
                        return;

                    if (!AllaganToolsConsumer.SubscribeRemoveEvent("DesynthItemRemoved", DesynthItemRemoved))
                        return;

                    LastBulkResult = new();
                    FinishedBulkDesynth.Start();

                    return;
                }
            }

            if (Configuration.EnableDesynthesis)
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
        }

        public void DesynthItemAdded((uint ItemId, InventoryItem.ItemFlags Flags, ulong CharacterId, uint Quantity) item)
        {
            if (!FinishedBulkDesynth.Enabled)
                return;

            // 19 and below are crystals
            if (item.ItemId > 19)
                LastBulkResult.AddItem(item.ItemId, item.Quantity, item.Flags == InventoryItem.ItemFlags.HQ);
            else
                LastBulkResult.AddCrystal(item.ItemId, item.Quantity);
        }

        public void DesynthItemRemoved((uint ItemId, InventoryItem.ItemFlags Flags, ulong CharacterId, uint Quantity) item)
        {
            if (!FinishedBulkDesynth.Enabled)
                return;

            LastBulkResult.AddSource(item.ItemId);
        }

        public void StoreBulkResult(object? _, ElapsedEventArgs __)
        {
            if (!LastBulkResult.IsValid)
                return;

            CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
            var character = CharacterStorage[ClientState.LocalContentId];

            character.Storage.History.Add(DateTime.Now, new DesynthResult(LastBulkResult));
            foreach (var result in LastBulkResult.Received.Where(r => r.Item != 0))
            {
                var id = result.Item > 1_000_000 ? result.Item - 1_000_000 : result.Item;
                if (!character.Storage.Total.TryAdd(id, result.Count))
                    character.Storage.Total[id] += result.Count;
            }

            ConfigurationBase.SaveCharacterConfig();
        }

        public void CofferTracker(Framework _)
        {
            // Both coffers disabled, we don't need to track
            if (Configuration is { EnableVentureCoffers: false, EnableGachaCoffers: false })
                return;

            var local = ClientState.LocalPlayer;
            if (local == null)
                return;

            if (!local.IsCasting)
                return;

            if (!AllaganToolsConsumer.SubscribeAddEvent("CofferItemAdded", NewItemAdded))
                return;

            if (local is { CastActionId: 32161, CastActionType: 2 } or { CastActionId: 36635, CastActionType: 2 } or { CastActionId: 36636, CastActionType: 2 })
            {
                CastTimer.Stop();
                CastTimer.Start();

                OpeningCoffer = true;
            }
        }

        public void NewItemAdded((uint ItemId, InventoryItem.ItemFlags Flags, ulong CharacterId, uint Quantity) item)
        {
            if (!OpeningCoffer)
                return;

            if (Coffer.Items.Contains(item.ItemId) && Configuration.EnableVentureCoffers)
            {
                CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
                var character = CharacterStorage[ClientState.LocalContentId];

                character.Coffer.Opened += 1;
                if (!character.Coffer.Obtained.TryAdd(item.ItemId, item.Quantity))
                    character.Coffer.Obtained[item.ItemId] += item.Quantity;

                OpeningCoffer = false;
                ConfigurationBase.SaveCharacterConfig();
            }
            else if (GachaContent.ThreeZero.Contains(item.ItemId) && Configuration.EnableGachaCoffers)
            {
                CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
                var character = CharacterStorage[ClientState.LocalContentId];

                character.GachaThreeZero.Opened += 1;
                if (!character.GachaThreeZero.Obtained.TryAdd(item.ItemId, item.Quantity))
                    character.GachaThreeZero.Obtained[item.ItemId] += item.Quantity;

                OpeningCoffer = false;
                ConfigurationBase.SaveCharacterConfig();
            }
            else if (GachaContent.FourZero.Contains(item.ItemId) && Configuration.EnableGachaCoffers)
            {
                CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
                var character = CharacterStorage[ClientState.LocalContentId];

                character.GachaFourZero.Opened += 1;
                if (!character.GachaFourZero.Obtained.TryAdd(item.ItemId, item.Quantity))
                    character.GachaFourZero.Obtained[item.ItemId] += item.Quantity;

                OpeningCoffer = false;
                ConfigurationBase.SaveCharacterConfig();
            }

            if (OpeningCoffer)
                ChatGui.Print($"Found an item that is possible from chest {item.ItemId} but in no list");
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
