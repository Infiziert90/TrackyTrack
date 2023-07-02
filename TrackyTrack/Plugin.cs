using System.IO;
using System.Reflection;
using System.Timers;
using Dalamud.Game.Command;
using Dalamud.IoC;
using Dalamud.Plugin;
using Dalamud.Data;
using Dalamud.Game;
using Dalamud.Game.ClientState;
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

        public string Name => "Tracky Track";

        public Configuration Configuration { get; init; }
        public WindowSystem WindowSystem = new("Tracky Track");

        private ConfigWindow ConfigWindow { get; init; }
        private MainWindow MainWindow { get; init; }

        public static readonly string Authors = "Infi";
        public static readonly string Version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";

        private readonly PluginCommandManager<Plugin> CommandManager;

        public Dictionary<ulong, CharacterConfiguration> CharacterStorage = new();

        private LastSeen Last = new();
        private uint StackCounter;
        private uint ItemCounter;
        private int LastCategory = -1;
        private readonly Dictionary<uint, long> UiBuildTimer = new();

        private Timer LastDesynth = new(30 * 1000);
        private Timer CastTimer = new(3 * 1000);
        private bool OpeningCoffer;

        public static AllaganToolsConsumer AllaganToolsConsumer = null!;

        public Plugin()
        {
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

            LoadCharacters();

            LastDesynth.AutoReset = false;
            LastDesynth.Elapsed +=
                (_, _) =>
                {
                    Last = new LastSeen();
                    StackCounter = 0;
                    ItemCounter = 0;
                    LastCategory = -1;
                    UiBuildTimer.Clear();
                };

            CastTimer.AutoReset = false;
            CastTimer.Elapsed += (_, _) => OpeningCoffer = false;

            Framework.Update += DesynthesisTracker;
            Framework.Update += CofferTracker;
        }

        public void Dispose() => Dispose(true);

        public void Dispose(bool full)
        {
            this.WindowSystem.RemoveAllWindows();

            ConfigWindow.Dispose();
            MainWindow.Dispose();

            CommandManager.Dispose();

            TexturesCache.Instance?.Dispose();

            if (full)
            {
                AllaganToolsConsumer.Unsubscribe(NewItemAdded);
                Framework.Update -= DesynthesisTracker;
                Framework.Update -= CofferTracker;
            }
        }

        [Command("/dtracker")]
        [HelpMessage("Opens the tracker")]
        private void OnCommand(string command, string args)
        {
            MainWindow.IsOpen ^= true;
        }

        [Command("/dconf")]
        [HelpMessage("Opens the config")]
        private void OnConfigCommand(string command, string args)
        {
            ConfigWindow.IsOpen ^= true;
        }

        public void CofferTracker(Framework _)
        {
            if (!Configuration.EnableVentureCoffers)
                return;

            var local = ClientState.LocalPlayer;
            if (local == null)
                return;

            if (!AllaganToolsConsumer.SubscribeToEvent(NewItemAdded))
                return;

            if (!local.IsCasting)
                return;

            if (local is { CastActionId: 32161, CastActionType: 2 })
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

            if (Coffer.Items.Contains(item.ItemId))
            {
                CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
                var character = CharacterStorage[ClientState.LocalContentId];

                character.Coffer.Opened += 1;
                if (!character.Coffer.Obtained.TryAdd(item.ItemId, item.Quantity))
                    character.Coffer.Obtained[item.ItemId] += item.Quantity;

                OpeningCoffer = false;
                SaveCharacter();
            }
        }

        public unsafe void DesynthesisTracker(Framework _)
        {
            if (!Configuration.EnableDesynthesis)
                return;

            var local = ClientState.LocalPlayer;
            if (local == null)
                return;

            var instance = AgentSalvage.Instance();
            if (instance == null)
                return;

            if (instance->ItemList != null)
            {
                if (LastCategory != (int) instance->SelectedCategory || ItemCounter < instance->ItemCount)
                {
                    ItemCounter = instance->ItemCount;
                    LastCategory = (int) instance->SelectedCategory;
                    UiBuildTimer.Clear();
                }

                var current = new LastSeen(instance);
                if (ItemCounter == current.Count)
                    return;

                if (Last == current)
                    return;

                if(UiBuildTimer.TryGetValue(current.Count, out var time))
                {
                    if(Environment.TickCount64 < time)
                        return;
                }
                else
                {
                    UiBuildTimer[current.Count] = Environment.TickCount64 + 500;
                    return;
                }

                CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
                var innerCharacter = CharacterStorage[ClientState.LocalContentId];

                innerCharacter.Storage.History.Add(DateTime.Now, new DesynthResult(instance));

                foreach (var result in instance->DesynthResultSpan)
                {
                    if (!innerCharacter.Storage.Total.TryAdd(result.ItemId, (uint)result.Quantity))
                        innerCharacter.Storage.Total[result.ItemId] += (uint)result.Quantity;
                }

                Last = current;
                Last.Done();

                SaveCharacter();
                return;
            }

            if (instance->DesynthItemSlot != null)
            {
                var current = new LastSeen(instance->DesynthItemSlot);

                // handle stacks
                if (StackCounter == 0 && current.Count == 1)
                {
                    if (Last == current)
                        return;
                }
                else
                {
                    // Set stack counter to the first count, so that it is known when the first result is in
                    if (StackCounter == 0)
                        StackCounter = current.Count;

                    if (StackCounter == current.Count)
                        return;

                    if (Last == current)
                        return;

                    CharacterStorage.TryAdd(ClientState.LocalContentId, CharacterConfiguration.CreateNew());
                    var innerCharacter = CharacterStorage[ClientState.LocalContentId];

                    innerCharacter.Storage.History.Add(DateTime.Now, new DesynthResult(instance));

                    foreach (var result in instance->DesynthResultSpan)
                    {
                        if (!innerCharacter.Storage.Total.TryAdd(result.ItemId, (uint)result.Quantity))
                            innerCharacter.Storage.Total[result.ItemId] += (uint)result.Quantity;
                    }

                    current.Done();
                    SaveCharacter();
                }

                Last = current;
                return;
            }

            // outside of SalvageResult so we reset StackCounter and ItemCounter
            StackCounter = 0;
            ItemCounter = 0;
            LastCategory = -1;
            UiBuildTimer.Clear();

            if (Last.Processed)
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

            LastDesynth.Stop();
            LastDesynth.Start();

            Last.Done();
            SaveCharacter();
        }

        public struct LastSeen
        {
            public int Container;
            public short Slot;
            public uint Count;
            public uint Item;

            public bool Processed;


            public LastSeen()
            {
                Container = -1;
                Slot = -1;
                Count = 0;
                Item = 0;

                Processed = true;
            }

            public unsafe LastSeen(InventoryItem* inventory)
            {
                Container = (int) inventory->Container;
                Slot = inventory->Slot;
                Count = inventory->Quantity;
                Item = inventory->ItemID;

                Processed = false;
            }

            public unsafe LastSeen(AgentSalvage* instance)
            {
                Container = (int) instance->ItemList->InventoryType;
                Slot = (short) instance->ItemList->InventorySlot;
                Count = instance->ItemCount;
                Item = instance->DesynthItemId;

                Processed = false;
            }

            public void Done() => Processed = true;

            public bool Equals(LastSeen other)
            {
                return Container == other.Container && Slot == other.Slot && Count == other.Count && Item == other.Item;
            }

            public static bool operator ==(LastSeen left, LastSeen right)
            {
                return left.Equals(right);
            }

            public static bool operator !=(LastSeen left, LastSeen right)
            {
                return !left.Equals(right);
            }
        }

        #region Character Handler
        public void LoadCharacters()
        {
            foreach (var file in PluginInterface.ConfigDirectory.EnumerateFiles())
            {
                ulong id;
                try
                {
                    id = Convert.ToUInt64(Path.GetFileNameWithoutExtension(file.Name));
                }
                catch (Exception e)
                {
                    PluginLog.Error($"Found file that isn't convertable. Filename: {file.Name}");
                    PluginLog.Error(e.Message);
                    continue;
                }

                var config = CharacterConfiguration.Load(id);

                if (!CharacterStorage.TryAdd(id, config))
                    CharacterStorage[id] = config;
            }
        }

        public void SaveCharacter()
        {
            if (!CharacterStorage.TryGetValue(ClientState.LocalContentId, out var savedConfig))
                return;
            savedConfig.Save();
        }

        public void DeleteCharacter(ulong id)
        {
            if (!CharacterStorage.ContainsKey(id))
                return;

            CharacterStorage.Remove(id);
            var file = PluginInterface.ConfigDirectory.EnumerateFiles().FirstOrDefault(f => f.Name == $"{id}.json");
            if (file == null)
                return;

            try
            {
                file.Delete();
            }
            catch (Exception e)
            {
                PluginLog.Error("Error while deleting character save file.");
                PluginLog.Error(e.Message);
            }
        }
        #endregion

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
