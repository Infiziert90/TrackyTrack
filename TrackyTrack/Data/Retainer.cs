using Newtonsoft.Json;

namespace TrackyTrack.Data;

public record VentureItem(uint Item, short Count, bool HQ)
{
    [JsonIgnore] public bool Valid => Item > 0;
}

public record VentureResult(uint VentureType, List<VentureItem> Items, bool MaxLevel)
{
    [JsonIgnore] public bool IsQuickVenture => VentureType == 395;
    [JsonIgnore] public VentureItem Primary => Items[0];
}

public class Retainer
{
    public Dictionary<DateTime, VentureResult> History = new();
}
