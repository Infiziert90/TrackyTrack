namespace TrackyTrack.Data;

public class MiniCactpot
{
    public uint Recorded;
    public Dictionary<DateTime, MiniCactpotData> History = [];
}

public class MiniCactpotData
{
    public byte[] Start = new byte[2];
    public byte[] FullBoard = new byte[9];

    public int Sum;
    public int Payout;
}