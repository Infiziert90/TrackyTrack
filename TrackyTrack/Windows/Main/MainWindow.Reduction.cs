using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private void ReductionTab()
    {
        using var tabItem = ImRaii.TabItem("Reduction");
        if (!tabItem.Success)
            return;

        using var tabBar = ImRaii.TabBar("##ReductionTabBar");
        if (!tabBar.Success)
            return;

        // Sort out any character with 0 reduction
        var characters = Plugin.CharacterStorage.Values.Where(c => c.Reduction.History.Count > 0).ToArray();
        if (characters.Length == 0)
        {
            Helper.NoReductionData();
            return;
        }

        ReductionStats(characters);

        ReductionHistory(characters);
    }

    private void ReductionStats(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("Stats");
        if (!tabItem.Success)
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.TextColored(ImGuiColors.DalamudViolet, "General:");
        using var table = ImRaii.Table("##TotalStatsTable", 3);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##stat", ImGuiTableColumnFlags.WidthStretch, 0.8f);
        ImGui.TableSetupColumn("##name");
        ImGui.TableSetupColumn("##amount");

        using var indent = ImRaii.PushIndent(10.0f);

        var count = characters.Sum(c => c.Reduction.History.Count);
        ImGui.TableNextColumn();
        ImGui.TextColored(ImGuiColors.HealerGreen, "Reductions");
        ImGui.TableNextColumn();
        ImGui.TextUnformatted($"{count:N0} time{(count > 1 ? "s" : "")}");
    }

    private void ReductionHistory(CharacterConfiguration[] characters)
    {
        using var tabItem = ImRaii.TabItem("History");
        if (!tabItem.Success)
            return;

        var selectedCharacter = SelectedCharacter;
        Helper.ClippedCombo("##existingCharacters", ref selectedCharacter, characters, character => $"{character.CharacterName}@{character.World}");
        if (selectedCharacter != SelectedCharacter)
        {
            SelectedCharacter = selectedCharacter;
            SelectedHistory = 0;
        }

        var selectedChar = characters[SelectedCharacter];
        var selectedHistory = selectedChar.Reduction.History.Reverse().ToArray();

        Helper.ClippedCombo("##reductionSelection", ref SelectedHistory, selectedHistory, pair => $"{pair.Key}");
        Helper.DrawArrows(ref SelectedHistory, selectedHistory.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var resultPair = selectedHistory[SelectedHistory];
        var source = Sheets.GetItem(resultPair.Value.Source);
        Helper.DrawIcon(source.Icon);
        ImGui.SameLine();
        ImGui.TextColored(ImGuiColors.HealerGreen, source.Name.ToString());

        new SimpleTable<ItemResult>("##HistoryTable", Helper.NoSort, withIndent: 10.0f)
            .HideHeaderRow()
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.ToItemRow().Icon))
            .AddColumn("##item", entry => { ImGui.TextUnformatted(entry.ToItemRow().Name.ToString()); })
            .AddColumn("##amount", entry => ImGui.TextUnformatted($"x{entry.Count}"), ImGuiTableColumnFlags.WidthStretch, initWidth: 0.2f)
            .Draw(resultPair.Value.Received.Where(i => i.Item > 0));
    }
}
