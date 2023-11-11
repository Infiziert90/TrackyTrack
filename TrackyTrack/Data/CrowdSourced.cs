// ReSharper disable FieldCanBeMadeReadOnly.Global
// MessagePack can't deserialize into readonly

using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using CsvHelper.Configuration.Attributes;
using MessagePack;

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

    public record struct Stats(uint Min, uint Max, uint Records = 1);

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
                if (import.Source > 1_000_000)
                    Plugin.Log.Warning($"Invalid data found, ID: {import.Id}");

                totalRecords++;
                if (!records.TryAdd(import.Source, 1))
                    records[import.Source]++;

                var spl = import.Rewards.Trim('{', '}').Split(",");
                var length = spl.Length / 2;
                if (length > 3)
                    Plugin.Log.Warning($"Invalid data found, ID: {import.Id}");

                for (var i = 0; i < spl.Length / 2; i++)
                {
                    var item = uint.Parse(spl[2 * i]);
                    var amount = uint.Parse(spl[(2 * i) + 1]);

                    switch (item)
                    {
                        case 0:
                            continue;
                        case > 1_000_000:
                            Plugin.Log.Warning($"Invalid data found, ID: {import.Id}");
                            break;
                    }

                    final.TryAdd(import.Source, new Dictionary<uint, Stats>());

                    var t = final[import.Source];
                    if (!t.TryAdd(item, new Stats(amount, amount)))
                    {
                        var minMax = t[item];
                        minMax.Records++;
                        minMax.Min = Math.Min(amount, minMax.Min);
                        minMax.Max = Math.Max(amount, minMax.Max);
                        t[item] = minMax;
                    }
                }
            }

            SourcedData = new CrowdSourced { TotalRecords = totalRecords, LastUpdate = DateTime.Now };
            foreach (var (source, rewards) in final)
            {
                var r = new List<Result>();
                foreach (var (reward, minMax) in rewards)
                {
                    r.Add(new Result(reward, minMax.Min, minMax.Max, minMax.Records));

                    if (!SourcedData.Rewards.TryAdd(reward, new History { Records = minMax.Records, Results = new [] { new Result(source, minMax.Min, minMax.Max, minMax.Records) } }))
                    {
                        var h = SourcedData.Rewards[reward];
                        h.Records += minMax.Records;
                        h.Results = h.Results.Append(new Result(source, minMax.Min, minMax.Max, minMax.Records)).ToArray();
                        SourcedData.Rewards[reward] = h;
                    }
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
        catch (Exception e)
        {
            Plugin.Log.Error(e, "Can't build desynthesis history.");
        }
    }
    #endif
    #endregion
}
