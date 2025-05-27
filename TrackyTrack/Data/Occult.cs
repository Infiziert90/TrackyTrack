using Newtonsoft.Json;

namespace TrackyTrack.Data;

public record OccultResult
{
    public readonly List<OccultItem> Items = [];

    public void AddItem(uint item, uint count) => Items.Add(new OccultItem(Utils.NormalizeItemId(item), count));
    [JsonIgnore] public bool IsValid => Items.Count != 0;
}

public record OccultItem(uint Item, uint Count);
