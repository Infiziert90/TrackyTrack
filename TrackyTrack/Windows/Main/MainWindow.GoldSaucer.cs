using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private uint SelectedSaucerType;
    private int SaucerSelectedCharacter;
    private int SaucerSelectedHistory;

    private void SaucerTab()
    {
        using var tabItem = ImRaii.TabItem("Gold Saucer");
        if (!tabItem.Success)
            return;

        var characters = Plugin.CharacterStorage.Values;
        if (characters.Count == 0)
        {
            Helper.NoMiniCactpotData();
            return;
        }

        var characterSaucer = characters.Where(c => c.MiniCactpot.Recorded > 0).ToArray();
        if (characterSaucer.Length == 0)
        {
            Helper.NoMiniCactpotData();
            return;
        }

        var styles = ImGui.GetStyle();
        var nameDict = new SortedDictionary<uint, (string Name, float Width)>();
        nameDict[0] = ("Mini Cactpot", ImGui.CalcTextSize("Mini Cactpot").X + styles.ItemSpacing.X * 2);

        var pos = ImGui.GetCursorPos();

        var childSize = new Vector2(nameDict.Select(pair => pair.Value.Width).Max(), 0);
        using (var tabChild = ImRaii.Child("Tabs", childSize, true))
        {
            if (tabChild.Success)
            {
                foreach (var (id, (name, _)) in nameDict)
                    if (ImGui.Selectable(name, SelectedSaucerType == id))
                        SelectedSaucerType = id;
            }
        }

        ImGui.SetCursorPos(pos with {X = pos.X + childSize.X});
        using (var contentChild = ImRaii.Child("Content", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                if (SelectedSaucerType == 0)
                    MiniCactpotOverview(characterSaucer);
            }
        }
    }

    private void MiniCactpotOverview(CharacterConfiguration[] characters)
    {
        var selectedCharacter = SaucerSelectedCharacter;
        Helper.ClippedCombo("##existingCharacters", ref selectedCharacter, characters, character => $"{character.CharacterName}@{character.World}");
        if (selectedCharacter != SaucerSelectedCharacter)
        {
            SaucerSelectedCharacter = selectedCharacter;
            SaucerSelectedHistory = 0;
        }

        var reversedHistory = characters[SaucerSelectedCharacter].MiniCactpot.History.Reverse().ToArray();

        Helper.ClippedCombo("##historySelection", ref SaucerSelectedHistory, reversedHistory, pair => $"{pair.Key}");
        Helper.DrawArrows(ref SaucerSelectedHistory, reversedHistory.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var result = reversedHistory[SaucerSelectedHistory].Value;
        ImGui.Text($"Sum: {result.Sum} | Payout: {result.Payout}");
        using var table = ImRaii.Table("##HistoryTable", 3, ImGuiTableFlags.None, new Vector2(100, 0));
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##first");
        ImGui.TableSetupColumn("##second");
        ImGui.TableSetupColumn("##third");

        foreach (var (value, index) in result.FullBoard.Select((val, idx) => (val, idx)))
        {
            if (index % 3 == 0)
                ImGui.TableNextRow();

            var color = result.Start[1] == value ? ImGuiColors.HealerGreen : ImGuiColors.DalamudOrange;

            ImGui.TableNextColumn();
            ImGui.TextColored(color, value.ToString());
        }
    }
}
