using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private void GachaTab()
    {
        if (ImGui.BeginTabItem("Gacha"))
        {
            if (ImGui.BeginTabBar("##GachaTabBar"))
            {
                GachaThreeZero();

                GachaFourZero();

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void GachaThreeZero()
    {
        if (!ImGui.BeginTabItem("Gacha 3.0"))
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

        var characterGacha = characters.Where(c => c.GachaThreeZero.Opened > 0).ToList();
        if (!characterGacha.Any())
        {
            Helper.NoGachaData();
            ImGui.EndTabItem();
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in GachaContent.ThreeZero)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaThreeZero.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaThreeZero.Opened).Sum();
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (ImGui.BeginTable($"##GachaThreeZeroTable", 4))
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
                ImGui.TextUnformatted($"{((double) count / opened * 100.0):F2}%");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void GachaFourZero()
    {
        if (!ImGui.BeginTabItem("Gacha 4.0"))
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

        var characterGacha = characters.Where(c => c.GachaFourZero.Opened > 0).ToList();
        if (!characterGacha.Any())
        {
            Helper.NoGachaData();
            ImGui.EndTabItem();
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in GachaContent.FourZero)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaFourZero.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaFourZero.Opened).Sum();
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        if (ImGui.BeginTable($"##GachaFourZeroTable", 4))
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
                ImGui.TextUnformatted($"{((double) count / opened * 100.0):F2}%");
                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }
}
