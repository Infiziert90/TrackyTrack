using Dalamud.Game.Text.SeStringHandling;
using Dalamud.Utility;

namespace TrackyTrack;

public static class Utils
{
    public static string ToStr(SeString content) => content.ToString();
    public static string ToStr(Lumina.Text.SeString content) => content.ToDalamudString().ToString();
}
