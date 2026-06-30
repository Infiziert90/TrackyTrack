using Dalamud.Utility;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public class Reduction
{
    public Dictionary<uint, uint> Total = new();
    public Dictionary<DateTime, ReductionResult> History = [];
}

public struct ReductionResult
{
    public uint Source;
    public uint Collectability;
    public List<ItemResult> Received = [];
    public bool HasBonus;

    [JsonIgnore]
    public bool AwaitingResults;

    [JsonConstructor]
    public ReductionResult() {}

    public ReductionResult(uint source, uint collectability)
    {
        Source = ItemUtil.GetBaseId(source).ItemId;
        Collectability = collectability;
    }

    public void SetBonus()
    {
        HasBonus =  true;
    }

    public void AddItem(uint item, uint count)
    {
        Received.Add(new ItemResult(ItemUtil.GetBaseId(item).ItemId, count));
    }

    public bool IsValid => Source > 0 && Collectability > 0 && Received.Count is > 0 and <= 3;

    public ReductionResult Clone()
        => new() {Source = Source, Collectability = Collectability, Received = [..Received], HasBonus = HasBonus};
}
