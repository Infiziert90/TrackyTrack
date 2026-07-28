namespace TrackyTrack.Data;

public class FashionReportResult
{
    public uint WeekNum;
    public uint Score;
    public List<FashionReportCategory> Categories = [];
    public List<uint> ItemIds = [];
    public List<uint> StainIds = [];
}

public record FashionReportCategory(uint HintId, uint StampId)
{
    public uint[] Coupled() => [HintId, StampId];
};