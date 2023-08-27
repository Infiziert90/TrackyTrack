namespace TrackyTrack.Data;

public record VentureResult(uint VentureType, uint Item, uint Count, bool HQ, bool MaxLevel);

public class Retainer
{
    public Dictionary<DateTime, VentureResult> History = new();
}
