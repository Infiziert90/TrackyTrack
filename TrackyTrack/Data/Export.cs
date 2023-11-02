// ReSharper disable ExplicitCallerInfoArgument

using System.Globalization;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using CsvHelper;
using CsvHelper.Configuration;
using Lumina.Excel;
using Lumina.Excel.GeneratedSheets;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public static class Export
{
    private const string GachaUrl = "https://xzwnvwjxgmaqtrxewngh.supabase.co/rest/v1/Gacha";
    private const string BunnyUrl = "https://xzwnvwjxgmaqtrxewngh.supabase.co/rest/v1/Bnuuy";
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

    public class GachaLoot
    {
        [JsonProperty("coffer")]
        public uint Coffer { get; set; }

        [JsonProperty("item_id")]
        public uint ItemId { get; set; }

        [JsonIgnore]
        public string Name { get; set; }

        [JsonProperty("amount")]
        public uint Amount { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; } = Plugin.Version;

        public GachaLoot(uint id, uint amount)
        {
            Coffer = 0;
            ItemId = id;
            Name = Utils.ToStr(ItemSheet.GetRow(id)!.Name);
            Amount = amount;
        }

        public GachaLoot(uint coffer, uint id, uint amount)
        {
            Coffer = coffer;
            ItemId = id;
            Name = Utils.ToStr(ItemSheet.GetRow(id)!.Name);
            Amount = amount;
        }
    }

    public class BunnyLoot
    {
        [JsonProperty("coffer")]
        public uint Rarity { get; set; }

        [JsonProperty("territory")]
        public uint Territory { get; set; }

        [JsonProperty("items")]
        public uint[] Items { get; set; }

        [JsonProperty("version")]
        public string Version { get; set; } = Plugin.Version;

        public BunnyLoot(uint rarity, uint territory, List<EurekaItem> items)
        {
            Rarity = rarity;
            Territory = territory;
            Items = items.Select(i => i.Item).ToArray();
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

    public static async void UploadAllBunny(uint rarity, uint territory, Dictionary<DateTime, EurekaResult> results)
    {
        foreach (var result in results.Values.Select(v => v.Items))
        {
            try
            {
                var content = new StringContent(JsonConvert.SerializeObject(new BunnyLoot(rarity, territory, result)), Encoding.UTF8, "application/json");
                await Client.PostAsync(BunnyUrl, content);

                // Delay to prevent too many uploads in a short time
                await Task.Delay(30);
            }
            catch (Exception e)
            {
                Plugin.Log.Error(e.Message);
                Plugin.Log.Error(e.StackTrace ?? "No Stacktrace");
            }
        }
    }

    public static async void UploadGachaEntry(uint coffer, uint id, uint amount)
    {
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(new GachaLoot(coffer, id, amount)), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync(GachaUrl, content);

            Plugin.Log.Debug($"Item {id} | Response: {response.StatusCode}");
            Plugin.Log.Debug($"Item {id} | Content: {response.Content.ReadAsStringAsync().Result}");
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e.Message);
            Plugin.Log.Error(e.StackTrace ?? "No Stacktrace");
        }
    }

    public static async void UploadBunnyEntry(uint rarity, uint territory, List<EurekaItem> items)
    {
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(new BunnyLoot(rarity, territory, items)), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync(BunnyUrl, content);

            Plugin.Log.Debug($"Coffer {rarity} | Response: {response.StatusCode}");
            Plugin.Log.Debug($"Coffer {rarity} | Content: {response.Content.ReadAsStringAsync().Result}");
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e.Message);
            Plugin.Log.Error(e.StackTrace ?? "No Stacktrace");
        }
    }
}
