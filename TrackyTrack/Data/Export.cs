// ReSharper disable ExplicitCallerInfoArgument

using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public static class Export
{
    private const string BaseUrl = "https://xzwnvwjxgmaqtrxewngh.supabase.co/rest/v1/";
    private const string SupabaseAnonKey = "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.eyJpc3MiOiJzdXBhYmFzZSIsInJlZiI6Inh6d252d2p4Z21hcXRyeGV3bmdoIiwicm9sZSI6ImFub24iLCJpYXQiOjE2ODk3NzcwMDIsImV4cCI6MjAwNTM1MzAwMn0.aNYTnhY_Sagi9DyH5Q9tCz9lwaRCYzMC12SZ7q7jZBc";
    private static readonly HttpClient Client = new();

    private static ExcelSheet<Item> ItemSheet = null!;

    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture) { HasHeaderRecord = false };

    public static void Init()
    {
        ItemSheet = Plugin.Data.GetExcelSheet<Item>()!;

        Client.DefaultRequestHeaders.Add("apikey", SupabaseAnonKey);
        Client.DefaultRequestHeaders.Add("Authorization", $"Bearer {SupabaseAnonKey}");
        Client.DefaultRequestHeaders.Add("Prefer", "return=minimal");
    }

    public class Upload
    {
        [JsonIgnore]
        public string Table;

        [JsonProperty("version")]
        public string Version = Plugin.PluginInterface.Manifest.AssemblyVersion.ToString();

        public Upload(string table)
        {
            Table = table;
        }
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
            ItemId = id;
            Amount = amount;
            Name = Utils.ToStr(ItemSheet.GetRow(ItemId)!.Name);
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


        public BunnyLoot(uint rarity, uint territory, List<EurekaItem> items) : base("Bnuuy")
        {
            Rarity = rarity;
            Territory = territory;
            Items = items.Select(i => i.Item).ToArray();
        }
    }

    public class DesynthesisResult : Upload
    {
        [JsonProperty("source")]
        public uint Source;

        [JsonProperty("rewards")]
        public uint[] Rewards;

        public DesynthesisResult(uint source, uint[] rewards) : base("Desynthesis")
        {
            Source = source;
            Rewards = rewards;
        }

        public DesynthesisResult(DesynthResult result) : base("Desynthesis")
        {
            Source = result.Source;
            var r = new List<uint>();
            foreach (var reward in result.Received)
                r.AddRange(reward.ItemCountArray());
            Rewards = r.ToArray();
        }
    }

    public class RevisitedResult : Upload
    {
        [JsonProperty("node")]
        public int Node;

        [JsonProperty("revisited")]
        public bool Revisited;

        [JsonProperty("gathering")]
        public int Gathering;

        [JsonProperty("perception")]
        public int Perception;

        public unsafe RevisitedResult(int node, bool revisited) : base("Revisits")
        {
            Node = node;
            Revisited = revisited;

            var player = PlayerState.Instance();
            if (player == null)
                return;

            Gathering = player->Attributes[72];
            Perception = player->Attributes[73];
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
        catch (Exception e)
        {
            Plugin.Log.Error(e.StackTrace ?? "No Stacktrace");
        }
    }

    public static async void UploadEntry(Upload entry)
    {
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(entry), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync($"{BaseUrl}{entry.Table}", content);

            if (response.StatusCode != HttpStatusCode.Created)
                Plugin.Log.Debug($"Table {entry.Table} | Content: {response.Content.ReadAsStringAsync().Result}");
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Upload failed");
        }
    }
}
