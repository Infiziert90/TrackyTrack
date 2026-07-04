using Dalamud.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using Lumina.Excel.Sheets;
using Newtonsoft.Json;

namespace TrackyTrack.Data;

public class Desynth
{
    public Dictionary<uint, uint> Total = new();
    public Dictionary<DateTime, DesynthResultV2> History = new();

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

public class DesynthResultV2
{
    public uint Source;
    public List<ItemResult> Received = [];
    public double Increase;
    public double ClassLevel;
    public ushort[] Bonus = [];

    [JsonIgnore]
    public bool AwaitingResults;

    [JsonIgnore]
    private static readonly HashSet<ushort> BonusParams =
    [
        270, 10270, // Bacon Broth
        271, 10271, // Tinker's Calm
    ];

    [JsonConstructor]
    public DesynthResultV2() {}

    public unsafe DesynthResultV2(uint source)
    {
        Source = ItemUtil.GetBaseId(source).ItemId;
        ClassLevel = PlayerState.Instance()->GetDesynthesisLevel(Sheets.GetItem(Source).ClassJobRepair.RowId);
    }

    public void SetLevel(double increase)
    {
        if (Plugin.ObjectTable.LocalPlayer == null)
            return;

        Increase = increase;
        ClassLevel = Math.Round(ClassLevel - Increase, 2);
        Bonus = Plugin.ObjectTable.LocalPlayer.StatusList.Where(s => BonusParams.Contains(s.Param)).Select(s => s.Param).ToArray();
    }

    public void AddItem(uint item, uint count)
        => Received.Add(new ItemResult(ItemUtil.GetBaseId(item).ItemId, count));

    public bool IsValid
        => Source > 0 && Received.Count is > 0 and <= 3;

    public DesynthResultV2 Clone()
        => new() {Source = Source, Received = [..Received], Increase = Increase, ClassLevel = ClassLevel, Bonus = [..Bonus]};
}

public record ItemResult(uint Item, uint Count, bool HQ = false)
{
    public uint[] Combined() => [ItemUtil.GetBaseId(Item).ItemId, Count];

    public Item ToItemRow() => Sheets.GetItem(Item);
}
