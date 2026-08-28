using TrackyTrack.Data;

namespace TrackyTrack.Manager;

public class TempManager(Plugin plugin)
{
    public readonly Plugin Plugin = plugin;

    public RouletteData CurrentRoulette = new();

    public void StartRoulette(uint contentId, uint job, bool inProgress, bool limitedLeveling)
    {
        CurrentRoulette = new RouletteData
        {
            AwaitingResults = true,

            Roulette = contentId,
            Class = job,
            Level = (uint)Plugin.PlayerState.GetClassJobLevel(Sheets.ClassJobSheet.GetRow(job)),

            InProgress = inProgress,
            LimitedLeveling = limitedLeveling,
        };
    }
}