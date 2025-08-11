// ReSharper disable FieldCanBeMadeReadOnly.Global
// MessagePack can't deserialize into readonly

using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using MessagePack;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

[MessagePackObject]
public class CrowdSourced
{
    [Key(0)] public Dictionary<uint, History> Sources = new();
    [Key(1)] public Dictionary<uint, History> Rewards = new();

    [Key(2)] public uint TotalRecords;
    [Key(3)] public DateTime LastUpdate = DateTime.MinValue;
}

[MessagePackObject]
public struct History
{
    [Key(0)] public uint Records;
    [Key(1)] public Result[] Results;
}

[MessagePackObject]
public struct Result
{
    [Key(0)] public uint Item;
    [Key(1)] public byte Min;
    [Key(2)] public byte Max;
    [Key(3)] public uint Received;

    [SerializationConstructor]
    public Result() { }

    public Result(uint item, uint min, uint max, uint received)
    {
        Item = item;
        Min = (byte) min;
        Max = (byte) max;
        Received = received;
    }
}

public class Importer
{
    public record struct Stats(uint Min, uint Max, uint Records = 1);

    private const string Filename = "CrowdSourcedData.msgpack";
    private readonly string FullPath = Plugin.PluginInterface.AssemblyLocation.DirectoryName!;

    public CrowdSourced SourcedData = new();

    public void Load()
    {
        try
        {
            using var fileStream = File.OpenRead(Path.Combine(FullPath, Filename));
            SourcedData = MessagePackSerializer.Deserialize<CrowdSourced>(fileStream);
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Failed loading crowd source data.");
            SourcedData = new CrowdSourced();
        }
    }

    #region MessagePackCreation
    #if DEBUG
    // Used to create the MessagePack file
    public class CsvImport
    {
        [Name("id")] public uint Id { get; set; }
        [Name("source")] public uint Source { get; set; }
        [Name("rewards")] public string Rewards { get; set; }
    }

    public void Import(string inputFile)
    {
        try
        {
            var totalRecords = 0u;
            var records = new Dictionary<uint, uint>();
            var final = new Dictionary<uint, Dictionary<uint, Stats>>();

            using var reader = new FileInfo(inputFile).OpenText();
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
            foreach (var import in csv.GetRecords<CsvImport>())
            {
                if (import.Source > 100_000)
                    Plugin.Log.Warning($"Invalid source data found, ID: {import.Id}");

                totalRecords++;
                if (!records.TryAdd(import.Source, 1))
                    records[import.Source]++;

                var spl = import.Rewards.Trim('{', '}').Split(",");
                var length = spl.Length / 2;
                if (length > 3)
                    Plugin.Log.Warning($"Invalid length found, ID: {import.Id}");

                for (var i = 0; i < spl.Length / 2; i++)
                {
                    var item = uint.Parse(spl[2 * i]);
                    var amount = uint.Parse(spl[(2 * i) + 1]);

                    switch (item)
                    {
                        case 0:
                            continue;
                        case > 100_000:
                            Plugin.Log.Warning($"Invalid reward data found, ID: {import.Id}");
                            break;
                    }

                    if (!final.ContainsKey(import.Source))
                        final[import.Source] = [];

                    var t = final[import.Source];
                    if (!t.TryGetValue(item, out var minMax))
                    {
                        t[item] = new Stats(amount, amount);
                        continue;
                    }

                    minMax.Records++;
                    minMax.Min = Math.Min(amount, minMax.Min);
                    minMax.Max = Math.Max(amount, minMax.Max);
                    t[item] = minMax;
                }
            }

            SourcedData = new CrowdSourced { TotalRecords = totalRecords, LastUpdate = DateTime.Now };
            foreach (var (source, rewards) in final)
            {
                var r = new List<Result>();
                foreach (var (reward, minMax) in rewards)
                {
                    r.Add(new Result(reward, minMax.Min, minMax.Max, minMax.Records));

                    if (!SourcedData.Rewards.TryGetValue(reward, out var h))
                    {
                        SourcedData.Rewards[reward] = new History
                        {
                            Records = minMax.Records,
                            Results = [new Result(source, minMax.Min, minMax.Max, minMax.Records)]
                        };
                        continue;
                    }

                    h.Records += minMax.Records;
                    h.Results = h.Results.Append(new Result(source, minMax.Min, minMax.Max, minMax.Records)).ToArray();
                    SourcedData.Rewards[reward] = h;
                }

                SourcedData.Sources.Add(source, new History {
                    Records = records[source],
                    Results = r.ToArray()
                });
            }

            var path = Path.Combine(FullPath, Filename);
            if (File.Exists(path))
                File.Delete(path);

            File.WriteAllBytes(path, MessagePackSerializer.Serialize(SourcedData));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Can't build desynthesis history.");
        }
    }

    public class DutyLootImport
    {
        [Name("id")] public uint Id { get; set; }
        [Name("map")] public uint Map { get; set; }
        [Name("territory")] public uint Territory { get; set; }
        [Name("chest_id")] public uint ChestId { get; set; }
        [Name("content")] public string Content { get; set; }
        [Name("hashed")] public string Hashed { get; set; }
        [Name("created_at")] public DateTime CreatedAt { get; set; }

        [Name("chest_x")] public float ChestX { get; set; }
        [Name("chest_y")] public float ChestY { get; set; }
        [Name("chest_z")] public float ChestZ { get; set; }

        public uint[] GetContent()
        {
            var spl = Content.Trim('{', '}').Split(",");
            if (spl.Length % 2 != 0)
            {
                Plugin.Log.Information($"Invalid length found, ID: {Id}");
                return [];
            }

            return spl.Select(uint.Parse).ToArray();
        }
    }

    public struct DutyLoot(string dutyName)
    {
        public string DutyName = dutyName;
        public Dictionary<uint, ChestLoot> Chests = [];

        public int Records;

        public struct ChestLoot(string chestName, uint chestId, float x, float y, float z, uint territory, uint map)
        {
            public uint ChestId = chestId;
            public string ChestName = chestName;
            public Dictionary<uint, LootStats> Rewards = [];

            public uint MapId = map;
            public uint TerritoryId = territory;
            public Vector3 Position = new(x, y, z);
            public Dictionary<Vector3, uint> Positions = [];

            public int Records;
        }

        public struct LootStats()
        {
            public uint Obtained = 0;
            public uint Total = 0;
            public uint Min = uint.MaxValue;
            public uint Max = uint.MinValue;

            public void AdjustAmount(uint amount)
            {
                Obtained += 1;
                Total += amount;
                Min = Math.Min(amount, Min);
                Max = Math.Max(amount, Max);
            }
        }
    }

    public Dictionary<uint, DutyLoot> DutyLootCache = [];
    public void ImportDutyLoot(string inputFile)
    {
        var errorCounter = 0;
        try
        {
            var hashes = new Dictionary<string, DutyLootImport>();

            using var reader = new FileInfo(inputFile).OpenText();
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture) { HasHeaderRecord = true });
            foreach (var import in csv.GetRecords<DutyLootImport>())
            {
                if (!hashes.TryAdd(import.Hashed, import))
                {
                    var existing = hashes[import.Hashed];
                    if (!existing.GetContent().SequenceEqual(import.GetContent()))
                        Plugin.Log.Warning($"Duplicated hash found, ID: {import.Id} vs {existing.Id} | {import.CreatedAt} vs {existing.CreatedAt} | {import.Content} vs {existing.Content}");
                    continue;
                }

                if (!Sheets.TerritoryTypeSheet.TryGetRow(import.Territory, out var territoryType))
                {
                    Plugin.Log.Warning($"Invalid territory found, ID: {import.Id}");
                    continue;
                }

                if (!Sheets.MapSheet.HasRow(import.Map))
                {
                    Plugin.Log.Error($"Invalid map found, ID: {import.Id}");
                    continue;
                }

                if (!Sheets.TreasureSheet.TryGetRow(import.ChestId, out var treasure))
                {
                    Plugin.Log.Warning($"Invalid treasure found, ID: {import.Id}");
                    continue;
                }

                if (territoryType.ContentFinderCondition.RowId == 0)
                {
                    Plugin.Log.Warning($"Invalid duty found, ID: {import.Id}");
                    continue;
                }

                if (!DutyLootCache.TryGetValue(territoryType.ContentFinderCondition.RowId, out var dutyLoot))
                    dutyLoot = new DutyLoot(territoryType.ContentFinderCondition.Value.Name.ExtractText());

                if (!dutyLoot.Chests.TryGetValue(import.ChestId, out var chest))
                    chest = new DutyLoot.ChestLoot(treasure.Unknown0.ExtractText(), import.ChestId, import.ChestX, import.ChestY, import.ChestZ, import.Territory, import.Map);

                dutyLoot.Records++;
                chest.Records++;

                var content = import.GetContent();
                for (var i = 0; i < content.Length / 2; i++)
                {
                    var item = content[2 * i];
                    var amount = content[(2 * i) + 1];

                    chest.Rewards.TryAdd(item, new DutyLoot.LootStats());
                    var loot = chest.Rewards[item];
                    loot.AdjustAmount(amount);
                    chest.Rewards[item] = loot;
                }

                var pos = new Vector3(import.ChestX, import.ChestY, import.ChestZ);
                if (!chest.Positions.TryAdd(pos, 1))
                    chest.Positions[pos]++;

                dutyLoot.Chests[import.ChestId] = chest;
                DutyLootCache[territoryType.ContentFinderCondition.RowId] = dutyLoot;
            }

            var path = Path.Combine(FullPath, "test.json");
            if (File.Exists(path))
                File.Delete(path);

            File.WriteAllText(path, JsonConvert.SerializeObject(DutyLootCache, Formatting.Indented));
        }
        catch (Exception ex)
        {
            Plugin.Log.Error(ex, "Can't build duty loot history.");
        }

        Plugin.Log.Information($"Wrongly formatted positions: {errorCounter}");
    }
    #endif
    #endregion
}
