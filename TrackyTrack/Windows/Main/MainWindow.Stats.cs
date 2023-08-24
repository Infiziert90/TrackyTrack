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

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Currency:");
        ImGui.Indent(10.0f);
        if (ImGui.BeginTable($"##CurrencyStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("##CurrencyStat", 0, 0.6f);
            ImGui.TableSetupColumn("##CurrencyNum");

            ImGui.TableNextColumn();

            var seals = characters.Sum(c => c.GCSeals);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Grand Company");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{seals:N0}");

            ImGui.TableNextRow();
            ImGui.TableNextColumn();

            var mgp = characters.Sum(c => c.MGP);
            ImGui.TextColored(ImGuiColors.HealerGreen, "Gold Saucer");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{mgp:N0}");

            var ventures = characters.Sum(c => c.Ventures);
            if (ventures > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Venture Coins");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{ventures:N0}");
            }

            var allied = characters.Sum(c => c.AlliedSeals);
            if (allied > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Allied Seals");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{allied:N0}");
            }

            var centurio = characters.Sum(c => c.CenturioSeal);
            if (centurio > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Centurio Seals");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{centurio:N0}");
            }

            var nuts = characters.Sum(c => c.SackOfNuts);
            if (nuts > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Sack of Nuts");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{nuts:N0}");
            }

            var bicolor = characters.Sum(c => c.Bicolor);
            if (bicolor > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Bicolor Gemstones");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{bicolor:N0}");
            }

            var skybuilders = characters.Sum(c => c.Skybuilder);
            if (skybuilders > 0)
            {
                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Skybuilders' Scrip");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{skybuilders:N0}");
            }

            ImGui.EndTable();
        }
        ImGui.Unindent(10.0f);

        ImGuiHelpers.ScaledDummy(5.0f);

        var teleports = characters.Sum(c => c.Teleports);
        var aetheryteTickets = characters.Sum(c => c.TeleportsAetheryte);
        var gcTickets = characters.Sum(c => c.TeleportsGC);
        var vesperTickets = characters.Sum(c => c.TeleportsVesperBay);
        var firmamentTickets = characters.Sum(c => c.TeleportsFirmament);

        var teleportsWithout = teleports - aetheryteTickets - gcTickets - vesperTickets;
        if (teleportsWithout == 0)
            teleportsWithout = 1;

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Teleport:");
        ImGui.Indent(10.0f);
        if (teleports > 0)
        {
            if (ImGui.BeginTable($"##TeleportStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
            {
                ImGui.TableSetupColumn("##TeleportStat", 0, 0.6f);
                ImGui.TableSetupColumn("##TeleportNum");

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Used");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{teleports} times");

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
                ImGui.TextUnformatted($"{teleportCosts / teleportsWithout:N0} gil");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.TextColored(ImGuiColors.HealerGreen, "Tickets");

                ImGui.TableNextRow();
                ImGui.TableNextColumn();

                ImGui.Indent(10.0f);
                ImGui.TextColored(ImGuiColors.HealerGreen, "Aetheryte");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{aetheryteTickets} used");

                if (aetheryteTickets > 0)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.TextColored(ImGuiColors.HealerGreen, "Grand Company");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{gcTickets} used");
                }

                if (vesperTickets > 0)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.TextColored(ImGuiColors.HealerGreen, "Vesper Bay");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{vesperTickets} used");
                }

                if (firmamentTickets > 0)
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();

                    ImGui.TextColored(ImGuiColors.HealerGreen, "Firmament");
                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted($"{firmamentTickets} used");
                }
                ImGui.Unindent(10.0f);

                ImGui.EndTable();
            }
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "You haven't used teleport yet1");
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
                ImGui.TableSetupColumn("##RepairStat", 0, 0.6f);
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
