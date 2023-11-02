using Dalamud.Interface.Utility;
using Microsoft.VisualBasic;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private static readonly Dictionary<Currency, uint> IconList = new();

    private static void InitializeStats()
    {
        foreach (var currency in (Currency[])Enum.GetValues(typeof(Currency)))
            IconList[currency] = ItemSheet.GetRow((uint)currency)!.Icon;
    }

    private void StatsTab()
    {
        if (ImGui.BeginTabItem("Common##GeneralStats"))
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

        if (Configuration is { EnableCurrency: false, EnableTeleport: false, EnableRepair: false })
        {
            ImGuiHelpers.ScaledDummy(10.0f);
            Helper.WrappedError("No stats module enabled\n  - Currency\n  - Teleport\n  - Repair");
            return;
        }

        if (Configuration.EnableCurrency)
            CurrencyStats(characters);

        if (Configuration.EnableTeleport)
            TeleportStats(characters);

        if (Configuration.EnableRepair)
            RepairStats(characters);
    }

    private void CurrencyStats(CharacterConfiguration[] characters)
    {
        ImGuiHelpers.ScaledDummy(5.0f);

        ImGui.PushStyleVar(ImGuiStyleVar.CellPadding, new Vector2(0.5f, 0.5f));
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Currency:");
        ImGui.Indent(10.0f);
        if (ImGui.BeginTable($"##CurrencyStatsTable", 3, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
        {
            ImGui.TableSetupColumn("##CurrencyStat", 0, 0.7f);
            ImGui.TableSetupColumn("##CurrencyIcon", 0, 0.17f);
            ImGui.TableSetupColumn("##CurrencyNum");

            var textHeight = ImGui.CalcTextSize("Grand Company").Y * 1.5f;
            var iconSize = new Vector2(textHeight, textHeight);

            foreach (var currency in (Currency[]) Enum.GetValues(typeof(Currency)))
            {
                // We skip these as they are duplicates and aren't saved
                if (currency is Currency.Gil or Currency.StormSeals or Currency.SerpentSeals)
                    continue;

                var count = characters.Sum(c => c.GetCurrencyCount(currency));
                if (count == 0)
                    continue;

                ImGui.TableNextColumn();
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(ImGuiColors.HealerGreen, currency.ToName());
                ImGui.TableNextColumn();
                DrawIcon(IconList[currency], iconSize);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{count:N0}");
            }

            ImGui.EndTable();
        }
        ImGui.Unindent(10.0f);
        ImGui.PopStyleVar();

        ImGuiHelpers.ScaledDummy(5.0f);
    }

    private void TeleportStats(CharacterConfiguration[] characters)
    {
        var teleports = characters.Sum(c => c.Teleports);
        var aetheryteTickets = characters.Sum(c => c.TeleportsAetheryte);
        var gcTickets = characters.Sum(c => c.TeleportsGC);
        var vesperTickets = characters.Sum(c => c.TeleportsVesperBay);
        var firmamentTickets = characters.Sum(c => c.TeleportsFirmament);

        var teleportsWithout = teleports - aetheryteTickets - gcTickets - vesperTickets;
        if (teleportsWithout == 0)
            teleportsWithout = 1;
        
        var buffed = new Dictionary<TeleportBuff, (long, long)>();
        foreach (var buff in (TeleportBuff[]) Enum.GetValues(typeof(TeleportBuff)))
        {
            var count = characters.Sum(c => c.TeleportsWithBuffs.TryGetValue(buff, out var value) ? value : 0);
            var savings = characters.Sum(c => c.TeleportSavingsWithBuffs.TryGetValue(buff, out var value) ? value : 0);
            if (count == 0)
                continue;

            buffed[buff] = (count, savings);
        }

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Teleport:");
        ImGui.Indent(10.0f);
        if (teleports > 0)
        {
            if (ImGui.BeginTable($"##TeleportStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0)))
            {
                ImGui.TableSetupColumn("##TeleportStat", 0, 1.1f);
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

                #region Saving buffs
                if (buffed.Count > 0)
                {
                    ImGuiHelpers.ScaledDummy(5.0f);
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGui.TextColored(ImGuiColors.HealerGreen, "Savings Buffs");
                    ImGui.Indent(10.0f);

                    foreach (var (buff, (count, savings)) in buffed)
                    {
                        ImGui.TableNextRow();
                        ImGui.TableNextColumn();

                        ImGui.TextColored(ImGuiColors.HealerGreen, buff.ToName().Replace("%", "%%"));
                        ImGui.TableNextColumn();

                        var stat = $"{count} times";
                        if (savings > 0)
                        {
                            stat += $" (saved {savings:N0} gil)";
                        }
                        ImGui.TextUnformatted(stat);
                    }

                    ImGui.Unindent(10.0f);
                }
                #endregion

                ImGuiHelpers.ScaledDummy(5.0f);
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
            ImGui.TextColored(ImGuiColors.DalamudOrange, "You haven't used teleport yet!");
        }
        ImGui.Unindent(10.0f);

        ImGuiHelpers.ScaledDummy(5.0f);
    }

    private void RepairStats(CharacterConfiguration[] characters)
    {
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
