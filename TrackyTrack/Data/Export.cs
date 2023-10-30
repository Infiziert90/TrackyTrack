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
    private const string SupabaseUrl = "https://xzwnvwjxgmaqtrxewngh.supabase.co/rest/v1/Gacha";
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

    public class ExportLoot
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

        public ExportLoot(uint id, uint amount)
        {
            Coffer = 0;
            ItemId = id;
            Name = Utils.ToStr(ItemSheet.GetRow(id)!.Name);
            Amount = amount;
        }

        public ExportLoot(uint coffer, uint id, uint amount)
        {
            Coffer = coffer;
            ItemId = id;
            Name = Utils.ToStr(ItemSheet.GetRow(id)!.Name);
            Amount = amount;
        }
    }

    public sealed class ExportMap : ClassMap<ExportLoot>
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

            csv.WriteHeader<ExportLoot>();
            csv.NextRecord();

            foreach (var detailedLoot in dict.Select(pair => new ExportLoot(pair.Key, pair.Value)))
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

    public static async void UploadAll(Dictionary<CofferRarity, Dictionary<DateTime, EurekaResult>> dict)
    {
        foreach (var (rarity, results) in dict)
        {
            foreach (var result in results.Values.SelectMany(v => v.Items))
            {
                try
                {
                    var content = new StringContent(JsonConvert.SerializeObject(new ExportLoot((uint) rarity, result.Item, 1)), Encoding.UTF8, "application/json");
                    await Client.PostAsync(SupabaseUrl, content);

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
    }

    public static async void UploadEntry(uint coffer, uint id, uint amount)
    {
        try
        {
            var content = new StringContent(JsonConvert.SerializeObject(new ExportLoot(coffer, id, amount)), Encoding.UTF8, "application/json");
            var response = await Client.PostAsync(SupabaseUrl, content);

            Plugin.Log.Debug($"Item {id} | Response: {response.StatusCode}");
            Plugin.Log.Debug($"Item {id} | Content: {response.Content.ReadAsStringAsync().Result}");
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e.Message);
            Plugin.Log.Error(e.StackTrace ?? "No Stacktrace");
        }
    }
}
