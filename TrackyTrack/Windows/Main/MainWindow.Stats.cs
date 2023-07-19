namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private void StatsTab()
    {
        if (ImGui.BeginTabItem("Stats##GeneralStats"))
        {
            Stats();
            ImGui.EndTabItem();
        }
    }

    private void Stats()
    {
        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (!characters.Any())
        {
            Helper.NoCharacters();
            return;
        }

        ImGuiHelpers.ScaledDummy(5.0f);

        var teleports = characters.Sum(c => c.Teleports);
        var tickets = characters.Sum(c => c.TeleportsWithTicket);
        var teleportsWithTickets = teleports - tickets;
        if (teleportsWithTickets == 0)
            teleportsWithTickets = 1;

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Teleport:");
        ImGui.Indent(10.0f);
        if (teleports > 0)
        {
            if (ImGui.BeginTable($"##TeleportStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
            {
                ImGui.TableSetupColumn("##TeleportStat", 0, 0.5f);
                ImGui.TableSetupColumn("##TeleportNum");

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Used");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{teleports} times");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Tickets");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{tickets} utilized");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Costs");

                var teleportCosts = characters.Sum(c => c.TeleportCost);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{teleportCosts:N0} gil");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Average");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{teleportCosts / teleportsWithTickets:N0} gil");
                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "You haven't used teleport yet");
        }
        ImGui.Unindent(10.0f);

        ImGuiHelpers.ScaledDummy(5.0f);

        var repairs = characters.Sum(c => c.Repairs);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Repair:");
        ImGui.Indent(10.0f);
        if (repairs > 0)
        {
            if (ImGui.BeginTable($"##RepairStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
            {
                ImGui.TableSetupColumn("##RepairStat", 0, 0.5f);
                ImGui.TableSetupColumn("##RepairNum");

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Repaired");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{repairs} items");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Costs");

                var repairCosts = characters.Sum(c => c.RepairCost);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{repairCosts:N0} gil");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Average");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{repairCosts / repairs:N0} gil");
                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "You haven't used the NPC to repair yet");
        }
        ImGui.Unindent(10.0f);
    }
}
