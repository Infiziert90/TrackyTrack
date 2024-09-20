namespace TrackyTrack.Data;

public enum Currency : uint
{
    Gil = 1,
    StormSeals = 20,
    SerpentSeals = 21,
    FlameSeals = 22,

    AlliedSeals = 27,
    MGP = 29,
    Ventures = 21072,
    CenturioSeals = 10307,
    SackOfNuts = 26533,
    Bicolor = 26807,
    Skybuilders = 28063
}

public static class CurrencyExtension
{
    public static string ToName(this Currency currency)
    {
        return currency switch
        {
            Currency.SerpentSeals or Currency.FlameSeals or Currency.StormSeals => "Grand Company",
            Currency.MGP => "MGP",
            Currency.AlliedSeals => "Allied Seals",
            Currency.CenturioSeals => "Centurio Seals",
            Currency.Bicolor => "Bicolor Gemstone",
            Currency.Skybuilders => "Skybuilders' Scrip",
            Currency.SackOfNuts => "Sack of Nuts",
            Currency.Ventures => "Venture Coins",
            _ => "Unknown"
        };
    }
}
