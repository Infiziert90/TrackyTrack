using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public class Desynth
{
    public Dictionary<uint, uint> Total = new();
    public Dictionary<DateTime, DesynthResult> History = new();

    [JsonIgnore]
    public static readonly Dictionary<uint, uint> GilItems = new()
    {
        // Clear Demimateria
        {8142, 200},
        {8143, 1000},
        {8144, 5000},

        // Battlecraft Demimateria
        {8145, 60},
        {8146, 300},
        {8147, 500},

        // Fieldcraft Demimateria
        {8148, 44},
        {8149, 220},
        {8150, 400},

        // Allagan Piece
        {5823, 25},
        {5824, 100},
        {5825, 500},
        {5826, 2500},
        {5827, 10000},
    };
}

public record DesynthResult(uint Source, ItemResult[] Received)
{
    [JsonConstructor]
    public DesynthResult() : this(0, []) {}

    public unsafe DesynthResult(AgentSalvage* result) : this(0, [])
    {
        var sourceId = Utils.NormalizeItemId(result->DesynthItemId);

        Source = sourceId;
        Received = result->DesynthResults.ToArray()
                                         .Where(r => r.ItemId > 0)
                                         .Select(r =>
                                         {
                                             // HQ items are Item + 1,000,000
                                             var isHQ = r.ItemId > 1_000_000;
                                             return new ItemResult(Utils.NormalizeItemId(r.ItemId), (uint)r.Quantity, isHQ);
                                         })
                                         .ToArray();
    }

    public DesynthResult(BulkResult result) : this(0, [])
    {
        Source = result.Source;
        Received = result.Received.ToArray();
    }
}

public record ItemResult(uint Item, uint Count, bool HQ)
{
    public uint[] ItemCountArray() => [Utils.NormalizeItemId(Item), Count];

    public Item ToItemRow() => Sheets.GetItem(Item);
}

public struct BulkResult
{
    public uint Source;
    public List<ItemResult> Received;

    public BulkResult()
    {
        Source = 0;
        Received = [new ItemResult(0, 0, false)];
    }

    public void AddSource(uint source) => Source = source;
    public void AddItem(uint item, uint count, bool isHQ) => Received[0] = new ItemResult(Utils.NormalizeItemId(item), count, isHQ);
    public void AddCrystal(uint item, uint count) => Received.Add(new ItemResult(item, count, false));

    public bool IsValid => Source is > 100 and < 100_000 && Received[0].Item is > 100 and < 100_000 && Received.Count <= 3;
}
