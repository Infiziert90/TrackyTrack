using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public class Desynth
{
    public Dictionary<uint, uint> Total = new();
    public Dictionary<DateTime, DesynthResult> History = new();
}

public record DesynthResult(uint Source, ItemResult[] Received)
{
    [JsonConstructor]
    public DesynthResult() : this(0, Array.Empty<ItemResult>()) {}

    public unsafe DesynthResult(AgentSalvage* result) : this(0, Array.Empty<ItemResult>())
    {
        Source = result->DesynthItemId;
        Received = result->DesynthResultSpan
                   .ToArray()
                   .Where(r => r.ItemId > 0)
                   .Select(r =>
                   {
                       // HQ items are Item + 1,000,000
                       var isHQ = r.ItemId > 1_000_000;
                       return new ItemResult(isHQ ? r.ItemId - 1_000_000 : r.ItemId, (uint)r.Quantity, isHQ);
                   })
                   .ToArray();
    }
}
public record ItemResult(uint Item, uint Count, bool HQ);
