using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private void CofferTab()
    {
        if (ImGui.BeginTabItem("Coffers"))
        {
            if (ImGui.BeginTabBar("##CofferTabBar"))
            {
                Coffers();

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void Coffers()
    {
        if (!ImGui.BeginTabItem("Venture"))
            return;

        if (!Plugin.AllaganToolsConsumer.IsAvailable)
        {
            ImGui.TextColored(ImGuiColors.ParsedOrange, $"AllaganTools not available");
            ImGui.EndTabItem();
            return;
        }

        var characters = Plugin.CharacterStorage.Values.ToArray();

        if (!characters.Any())
        {
            Helper.NoVentureCofferData();
            ImGui.EndTabItem();
            return;
        }

        var characterCoffers = characters.Where(c => c.Coffer.Opened > 0).ToList();
        if (!characterCoffers.Any())
        {
            Helper.NoVentureCofferData();
            ImGui.EndTabItem();
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in Coffer.Items)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterCoffers.SelectMany(c => c.Coffer.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterCoffers.Select(c => c.Coffer.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value;
            var percentage = (pair.Key != 8841 ? count / 2.0 : count) / opened * 100.0;
            return new Utils.SortedEntry(item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (ImGui.BeginTable($"##HistoryTable", 4, ImGuiTableFlags.Sortable))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.NoSort, 0.17f);
            ImGui.TableSetupColumn("Item##item");
            ImGui.TableSetupColumn("Num##amount", 0, 0.2f);
            ImGui.TableSetupColumn("Pct##percentage", ImGuiTableColumnFlags.DefaultSort, 0.25f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var sortedEntry in Utils.SortEntries(unsortedList, ImGui.TableGetSortSpecs().Specs))
            {
                ImGui.TableNextColumn();
                DrawIcon(sortedEntry.Icon);
                ImGui.TableNextColumn();

                ImGui.TextUnformatted(sortedEntry.Name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(sortedEntry.Name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{sortedEntry.Count}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sortedEntry.Percentage:F2}%");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }
}
