﻿// ReSharper disable ExplicitCallerInfoArgument
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using CsvHelper;
using CsvHelper.Configuration;
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
            ItemId = Utils.NormalizeItemId(id);
            Amount = amount;
            Name = Utils.ToStr(Sheets.GetItem(ItemId).Name);
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
            Items = items.Select(i => Utils.NormalizeItemId(i.Item)).ToArray();
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
            Source = Utils.NormalizeItemId(source);
            Rewards = rewards.Select(Utils.NormalizeItemId).ToArray();
        }

        public DesynthesisResult(DesynthResult result) : base("Desynthesis")
        {
            Source = Utils.NormalizeItemId(result.Source);
            var r = new List<uint>();
            foreach (var reward in result.Received)
                r.AddRange(reward.ItemCountArray());

            Rewards = r.ToArray();
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
            PrimaryId = Utils.NormalizeItemId(primary.Item);
            PrimaryCount = primary.Count;
            PrimaryHq = primary.HQ;

            var additional = venture.Items[1];
            AdditionalId = Utils.NormalizeItemId(additional.Item);
            AdditionalCount = additional.Count;
            AdditionalHq = additional.HQ;

            MaxLevel = venture.MaxLevel;
            QuickVenture = venture.IsQuickVenture;
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
