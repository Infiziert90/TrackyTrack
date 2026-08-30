// ReSharper disable ExplicitCallerInfoArgument
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.Network;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public static class Export
{
    private const string BaseUrl = "https://infi.ovh/api/";
    private const string AnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJyb2xlIjoiYW5vbiJ9.Ur6wgi_rD4dr3uLLvbLoaEvfLCu4QFWdrF-uHRtbl_s";
    private static readonly HttpClient Client = new();

    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture) { HasHeaderRecord = false };

    static Export()
    {
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {AnonKey}");
        Client.DefaultRequestHeaders.Add("Prefer", "return=minimal");
    }

    public class Upload(string table)
    {
        [JsonIgnore]
        public string Table = table;

        [JsonProperty("version")]
        public string Version = Plugin.PluginInterface.Manifest.AssemblyVersion.ToString();
    }

    public class GachaLoot : Upload
    {
        [JsonProperty("coffer")]
        public uint Coffer;

        [JsonProperty("item_id")]
        public uint ItemId;

        [JsonProperty("amount")]
        public uint Amount;

        [JsonIgnore]
        public readonly string Name;

        public GachaLoot(uint id, uint amount) : this(0, id, amount) { }

        public GachaLoot(uint coffer, uint id, uint amount) : base("Gacha")
        {
            Coffer = coffer;
            ItemId = ItemUtil.GetBaseId(id).ItemId;
            Amount = amount;
            Name = Sheets.GetItem(ItemId).Name.ToString();
        }
    }

    public class BunnyLoot : Upload
    {
        [JsonProperty("coffer")]
        public uint Rarity;

        [JsonProperty("territory")]
        public uint Territory;

        [JsonProperty("items")]
        public uint[] Items;


        public BunnyLoot(uint rarity, uint territory, List<ItemResult> items) : base("Bnuuy")
        {
            Rarity = rarity;
            Territory = territory;
            Items = items.Select(i => ItemUtil.GetBaseId(i.Item).ItemId).ToArray();
        }
    }

    public class DesynthesisResultV2 : Upload
    {
        [JsonProperty("source")]
        public uint Source;

        [JsonProperty("rewards")]
        public uint[] Rewards;

        [JsonProperty("increase")]
        public double Increase;

        [JsonProperty("class_level")]
        public double ClassLevel;

        [JsonProperty("bonus")]
        public ushort[] Bonus;

        public DesynthesisResultV2(DesynthResultV2 result) : base("DesynthesisV2")
        {
            Source = ItemUtil.GetBaseId(result.Source).ItemId;
            Rewards = result.Received.SelectMany(r => r.Combined()).ToArray();
            Increase = result.Increase;
            ClassLevel = result.ClassLevel;
            Bonus = result.Bonus;
        }
    }

    public class ReductionUpload : Upload
    {
        [JsonProperty("source")]
        public uint Source;

        [JsonProperty("collectability")]
        public uint Collectability;

        [JsonProperty("rewards")]
        public uint[] Rewards;

        [JsonProperty("bonus")]
        public bool Bonus;

        public ReductionUpload(ReductionResult result) : base("Reduction")
        {
            Source = ItemUtil.GetBaseId(result.Source).ItemId;
            Collectability = result.Collectability;
            Rewards = result.Received.SelectMany(r => r.Combined()).ToArray();
            Bonus = result.HasBonus;
        }
    }

    public class VentureLoot : Upload
    {
        [JsonProperty("venture_type")]
        public uint VentureType;

        [JsonProperty("primary_id")]
        public uint PrimaryId;

        [JsonProperty("primary_count")]
        public short PrimaryCount;

        [JsonProperty("primary_hq")]
        public bool PrimaryHq;

        [JsonProperty("additional_id")]
        public uint AdditionalId;

        [JsonProperty("additional_count")]
        public short AdditionalCount;

        [JsonProperty("additional_hq")]
        public bool AdditionalHq;

        [JsonProperty("max_level")]
        public bool MaxLevel;

        [JsonProperty("quick_venture")]
        public bool QuickVenture;


        public VentureLoot(VentureResult venture) : base("Ventures")
        {
            VentureType = venture.VentureType;

            var primary = venture.Items[0];
            PrimaryId = ItemUtil.GetBaseId(primary.Item).ItemId;
            PrimaryCount = primary.Count;
            PrimaryHq = primary.HQ;

            var additional = venture.Items[1];
            AdditionalId = ItemUtil.GetBaseId(additional.Item).ItemId;
            AdditionalCount = additional.Count;
            AdditionalHq = additional.HQ;

            MaxLevel = venture.MaxLevel;
            QuickVenture = venture.IsQuickVenture;
        }
    }

    public class DutyLoot : Upload
    {
        [JsonProperty("map")]
        public uint MapId;

        [JsonProperty("territory")]
        public uint TerritoryId;

        [JsonProperty("chest_id")]
        public uint ChestBaseId;

        [JsonProperty("chest_x")]
        public float ChestPosX;

        [JsonProperty("chest_y")]
        public float ChestPosY;

        [JsonProperty("chest_z")]
        public float ChestPosZ;

        [JsonProperty("content")]
        public List<uint> ContentPairs = [];

        [JsonProperty("hashed")]
        public string Hashed;

        [JsonIgnore]
        private readonly HashSet<uint> SeenLootIndex = [];


        public DutyLoot(Vector3 chestPos, uint chestBaseId, uint chestObjectId, ulong lowestContentId) : base("DutyLootV2")
        {
            MapId = Plugin.ClientState.MapId;
            TerritoryId = Plugin.ClientState.TerritoryType;
            ChestBaseId = chestBaseId;

            ChestPosX = chestPos.X;
            ChestPosY = chestPos.Y;
            ChestPosZ = chestPos.Z;

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(chestBaseId);
                writer.Write(chestObjectId);
                writer.Write(lowestContentId);
            }

            stream.Position = 0;
            using (var hash = SHA256.Create())
            {
                var result = hash.ComputeHash(stream);
                Hashed = string.Join("", result.Select(b => $"{b:X2}"));
            }
        }

        public void AddContent(uint itemId, ushort amount, uint lootIndex)
        {
            // Loot at this specific index was already added
            if (!SeenLootIndex.Add(lootIndex))
                return;

            ContentPairs.Add(ItemUtil.GetBaseId(itemId).ItemId);
            ContentPairs.Add(amount);
        }
    }

    public class OccultTreasure : Upload
    {
        [JsonProperty("base_id")]
        public uint BaseId;

        [JsonProperty("territory")]
        public uint Territory;

        [JsonProperty("rewards")]
        public uint[] Rewards;

        [JsonProperty("pos_x")]
        public float ChestPosX;

        [JsonProperty("pos_y")]
        public float ChestPosY;

        [JsonProperty("pos_z")]
        public float ChestPosZ;

        public OccultTreasure(OccultCoffer coffer) : base("OccultTreasure")
        {
            BaseId = coffer.Base;
            Territory = (uint)coffer.Territory;

            Rewards = coffer.Received.SelectMany(r => r.Combined()).ToArray();

            ChestPosX = coffer.Position.X;
            ChestPosY = coffer.Position.Y;
            ChestPosZ = coffer.Position.Z;
        }
    }

    public class OccultBunny : Upload
    {
        [JsonProperty("coffer")]
        public uint Rarity;

        [JsonProperty("territory")]
        public uint Territory;

        [JsonProperty("rewards")]
        public uint[] Rewards;

        [JsonProperty("pos_x")]
        public float ChestPosX;

        [JsonProperty("pos_y")]
        public float ChestPosY;

        [JsonProperty("pos_z")]
        public float ChestPosZ;

        [JsonProperty("fate_id")]
        public uint FateId;

        public OccultBunny(OccultPot pot) : base("OccultBunny")
        {
            Rarity = (uint)pot.Rarity;
            Territory = (uint)pot.Territory;

            Rewards = pot.Received.SelectMany(r => r.Combined()).ToArray();

            ChestPosX = pot.Position.X;
            ChestPosY = pot.Position.Y;
            ChestPosZ = pot.Position.Z;

            FateId = pot.FateId;
        }
    }

    public class MiniCactpotSet : Upload
    {
        [JsonProperty("start")]
        public ushort[] Start; // NewtonsoftJson will base64 encode a byte[], so we have to use ushort[] here

        [JsonProperty("board")]
        public ushort[] Board;

        public MiniCactpotSet(MiniCactpotData data) : base("MiniCactpot")
        {
            Start = data.Start.Select(b => (ushort)b).ToArray();
            Board = data.FullBoard.Select(b => (ushort)b).ToArray();
        }
    }

    public class BnpcPair : Upload
    {
        [JsonProperty("base")]
        public uint BaseId;

        [JsonProperty("name")]
        public uint NameId;

        [JsonProperty("model_chara")]
        public uint ModelChara;

        [JsonProperty("territory")]
        public uint TerritoryId;

        [JsonProperty("map")]
        public uint MapId;

        [JsonProperty("no_target")]
        public bool NoTarget;

        [JsonProperty("level_id")]
        public uint LevelId;

        [JsonProperty("x")]
        public float X;

        [JsonProperty("y")]
        public float Y;

        [JsonProperty("z")]
        public float Z;

        [JsonProperty("rotation")]
        public uint Rotation;

        [JsonProperty("enemy_type")]
        public ushort EnemyType;

        [JsonProperty("level")]
        public ushort Level;

        [JsonProperty("foray_level")]
        public byte ForayLevel;

        [JsonProperty("foray_element")]
        public byte ForayElement;

        [JsonProperty("display_flags")]
        public uint DisplayFlags;

        [JsonProperty("spawn_type")]
        public ushort SpawnType;

        [JsonProperty("object_kind")]
        public ushort ObjectKind;

        [JsonProperty("hash")]
        public string Hashed;

        public unsafe BnpcPair(SpawnNpcPacket* packet, ushort spawnType) : base("BnpcPairs")
        {
            BaseId = packet->Common.BaseId;
            NameId = packet->Common.NameId;
            ModelChara = packet->Common.ModelChara;

            MapId = Plugin.ClientState.MapId;
            TerritoryId = Plugin.ClientState.TerritoryType;

            NoTarget = packet->Common.TargetId.ObjectId == 0xE0000000;

            LevelId = packet->Common.LayoutId;
            X = packet->Common.Position.X;
            Y = packet->Common.Position.Y;
            Z = packet->Common.Position.Z;
            Rotation = packet->Common.Rotation;

            EnemyType = packet->Common.Battalion;
            Level = packet->Common.Level;
            ForayLevel = packet->Common.ForayRank;
            ForayElement = packet->Common.ForayElement;
            DisplayFlags = packet->Common.DisplayFlags;
            ObjectKind = (ushort)packet->Common.ObjectKind;

            SpawnType = spawnType;

            using var stream = new MemoryStream();
            using (var writer = new BinaryWriter(stream, Encoding.UTF8, true))
            {
                writer.Write(BaseId);
                writer.Write(NameId);
                writer.Write(MapId);
                writer.Write(TerritoryId);
                writer.Write(LevelId);
                writer.Write(Plugin.PlayerState.ContentId);
            }

            stream.Position = 0;
            using (var hash = SHA256.Create())
            {
                var result = hash.ComputeHash(stream);
                Hashed = string.Join("", result.Select(b => $"{b:X2}"));
            }
        }
    }

    public class FateReward : Upload
    {
        // Extra data

        [JsonProperty("client_language")]
        public byte ClientLanguage;

        [JsonProperty("territory")]
        public uint Territory;

        [JsonProperty("map")]
        public uint Map;

        // Struct data

        [JsonProperty("type")]
        public byte Type;

        [JsonProperty("success")]
        public byte IsSuccess;

        [JsonProperty("name")]
        public string Name;

        [JsonProperty("icon")]
        public uint Icon;

        [JsonProperty("medal")]
        public uint Medal;

        [JsonProperty("fate_id")]
        public uint FateId;

        [JsonProperty("eureka_fate")]
        public byte EurekaFate;

        [JsonProperty("experience")]
        public uint Experience;

        [JsonProperty("experience_flags")]
        public byte ExperienceFlags;

        [JsonProperty("currency_amount")]
        public uint CurrencyAmount;

        [JsonProperty("currency_flags")]
        public byte CurrencyFlags;

        [JsonProperty("rewards")]
        public uint[] Rewards;

        [JsonProperty("fate_token_type_id")]
        public byte FateTokenTypeId;

        [JsonProperty("fate_token_type_item_id")]
        public uint FateTokenTypeItemId;

        [JsonProperty("fate_token_type_amount")]
        public uint FateTokenTypeAmount;

        [JsonProperty("fate_token_type_flags")]
        public byte FateTokenTypeFlags;

        [JsonProperty("grand_company")]
        public byte GrandCompany;

        [JsonProperty("gc_seals_amount")]
        public uint GCSealsAmount;

        [JsonProperty("additional_rewards")]
        public uint[] AdditionalRewards;

        [JsonProperty("item_processed_bits")]
        public byte ItemProcessedBits;

        [JsonProperty("item_processed_count")]
        public byte ItemProcessedCount;

        public unsafe FateReward(AgentFateReward.Reward* reward) : base("FateReward")
        {
            ClientLanguage = (byte)Plugin.ClientState.ClientLanguage;
            Territory = Plugin.ClientState.TerritoryType;
            Map = Plugin.ClientState.MapId;

            Type = (byte)reward->Type;
            IsSuccess = reward->IsSuccess ? (byte)1 : (byte)0;
            Name = reward->Name.AsReadOnlySeString().ToString();
            Icon = reward->Icon;
            Medal = reward->Medal;
            FateId = reward->Id;
            EurekaFate = reward->EurekaFate;
            Experience = reward->Experience;
            ExperienceFlags = reward->ExperienceFlags;
            CurrencyAmount = reward->CurrencyAmount;
            CurrencyFlags = reward->CurrencyFlags;
            FateTokenTypeId = reward->FateTokenTypeId;
            FateTokenTypeItemId = reward->FateTokenTypeItemId;
            FateTokenTypeAmount = reward->FateTokenTypeAmount;
            FateTokenTypeFlags = reward->FateTokenTypeFlags;
            GrandCompany = reward->GrandCompany;
            GCSealsAmount = reward->GCSealsAmount;
            ItemProcessedBits = reward->ItemProcessedBits;
            ItemProcessedCount = reward->ItemProcessedCount;

            var l = new List<uint>();
            foreach (var item in reward->Items)
            {
                l.Add(item.ItemId);
                l.Add(item.Amount);
            }

            Rewards = l.ToArray();

            l.Clear();
            foreach (var item in reward->AdditionalItems)
            {
                l.Add(item.ItemId);
                l.Add(item.Amount);
            }

            AdditionalRewards = l.ToArray();
        }
    }

    public class FashionReport : Upload
    {
        [JsonProperty("plugin")]
        public uint Plugin = 0;

        [JsonProperty("week_num")]
        public uint WeekNum;

        [JsonProperty("score")]
        public uint Score;

        [JsonProperty("hints")]
        public uint[] Hints;

        [JsonProperty("items")]
        public uint[] Items;

        [JsonProperty("dyes")]
        public uint[] Dyes;

        public FashionReport(FashionReportResult data) : base("FashionReport")
        {
            WeekNum = data.WeekNum;
            Score = data.Score;
            Hints = data.Categories.SelectMany(cat => cat.Coupled()).ToArray();
            Items = data.ItemIds.ToArray();
            Dyes = data.StainIds.ToArray();
        }
    }

    public class RouletteReport : Upload
    {
        [JsonProperty("roulette")]
        public uint Roulette;

        [JsonProperty("cfc")]
        public uint CFC;

        [JsonProperty("class")]
        public uint Class;

        [JsonProperty("level")]
        public int Level;

        [JsonProperty("class_exp")]
        public int ClassExp;

        [JsonProperty("exp")]
        public uint Exp;

        [JsonProperty("gil")]
        public uint Gil;

        [JsonProperty("limited_leveling")]
        public bool LimitedLeveling;

        [JsonProperty("in_progress")]
        public bool InProgress;

        public RouletteReport(RouletteData data) : base("Roulettes")
        {
            Roulette = data.Roulette;
            CFC = data.CFC;

            Class = data.Class;
            Level = data.Level;
            ClassExp = data.ClassExp;

            Exp = data.Exp;
            Gil = data.Gil;

            LimitedLeveling = data.LimitedLeveling;
            InProgress = data.InProgress;
        }
    }

    public class GuildleveAssignments : Upload
    {
        [JsonProperty("rowId")]
        public uint RowId;

        [JsonProperty("categoryRowId")]
        public byte CategoryRowId;

        [JsonProperty("categoryIndex")]
        public byte CategoryIndex;

        [JsonProperty("leveIds")]
        public ushort[] LeveIds;

        public GuildleveAssignments(GuildleveAssignmentData data) : base("GuildleveAssignments")
        {
            RowId = data.RowId;
            CategoryRowId = data.CategoryRowId;
            CategoryIndex = data.CategoryIndex;
            LeveIds = data.LeveIds.ToArray();
        }
    }

    public sealed class ExportMap : ClassMap<GachaLoot>
    {
        public ExportMap()
        {
            Map(m => m.Coffer).Ignore();

            Map(m => m.ItemId).Index(0).Name("ItemId");
            Map(m => m.Name).Index(1).Name("Name");
            Map(m => m.Amount).Index(2).Name("Amount");

            Map(m => m.Version).Ignore();
        }
    }

    public static void ExportToClipboard(Dictionary<uint, uint> dict)
    {
        try
        {
            using var writer = new StringWriter();
            using var csv = new CsvWriter(writer, CsvConfig);

            csv.Context.RegisterClassMap(new ExportMap());

            csv.WriteHeader<GachaLoot>();
            csv.NextRecord();

            foreach (var detailedLoot in dict.Select(pair => new GachaLoot(pair.Key, pair.Value)))
            {
                csv.WriteRecord(detailedLoot);
                csv.NextRecord();
            }

            ImGui.SetClipboardText(writer.ToString());
            Plugin.ChatGui.Print(Utils.SuccessMessage("Export to clipboard done."));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Export to clipboard failed.");
        }
    }

    public static async void UploadEntry(Upload entry)
    {
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(entry), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync($"{BaseUrl}{entry.Table}", content);

            if (response.StatusCode != HttpStatusCode.Created)
                Plugin.Log.Warning($"Table {entry.Table} | Content: {response.Content.ReadAsStringAsync().Result}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Warning(ex, "Upload failed.");
        }
    }
}
