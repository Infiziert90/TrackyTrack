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
            }
            ImGui.EndTabBar();

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
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (ImGui.BeginTable($"##HistoryTable", 4))
        {
            ImGui.TableSetupColumn("##icon", 0, 0.17f);
            ImGui.TableSetupColumn("##item");
            ImGui.TableSetupColumn("##amount", 0, 0.2f);
            ImGui.TableSetupColumn("##percentage", 0, 0.25f);

            ImGui.Indent(10.0f);
            foreach (var (itemId, count) in dict.Where(pair => pair.Value > 0))
            {
                var item = ItemSheet.GetRow(itemId)!;

                ImGui.TableNextColumn();
                DrawIcon(item.Icon);
                ImGui.TableNextColumn();

                var name = Utils.ToStr(item.Name);
                ImGui.TextUnformatted(name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(Utils.ToStr(item.Name));

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{count}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{((itemId != 8841 ? count / 2.0 : count) / opened * 100.0):F2}%");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
        }

        ImGui.EndTable();
        ImGui.EndTabItem();
    }
}
