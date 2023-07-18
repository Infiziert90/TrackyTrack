using Newtonsoft.Json;

namespace TrackyTrack.Data;

public class VentureCoffer
{
    public int Opened = 0;
    public Dictionary<uint, uint> Obtained = new();

    [JsonIgnore]
    public static readonly List<uint> Content = new()
    {
        13114, // Pure White
        13115, // Jet Black
        13708, // Pastel Pink

        13709, // Dark Red
        13710, // Dark Brown
        13712, // Dark Green
        13714, // Dark Blue
        13716, // Dark Purple

        13711, // Pastel Green
        13713, // Pastel Blue
        13715, // Pastel Purple

        13717, // Metallic Red
        13718, // Metallic Orange
        13719, // Metallic Yellow
        13720, // Metallic Green
        13721, // Metallic Sky Blue
        13722, // Metallic Blue
        13723, // Metallic Purple
        13116, // Metallic Silver
        13117, // Metallic Gold

        8841, // Retainer Fantasia
    };
}
