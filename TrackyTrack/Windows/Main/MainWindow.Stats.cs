using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private static readonly Dictionary<Currency, uint> IconList = new();

    private static void InitializeStats()
    {
        foreach (var currency in Enum.GetValues<Currency>())
            IconList[currency] = Sheets.GetItem((uint)currency).Icon;
    }

    private void StatsTab()
    {
        using var tabItem = ImRaii.TabItem("Common##GeneralStats");
        if (!tabItem.Success)
            return;

        Stats();
    }

    private void Stats()
    {
        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (characters.Length == 0)
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

        using var child = ImRaii.Child("ContentChild", Vector2.Zero, true);
        if (!child.Success)
            return;

        if (Configuration.EnableCurrency)
            CurrencyStats(characters);

        if (Configuration.EnableTeleport)
            TeleportStats(characters);

        if (Configuration.EnableRepair)
            RepairStats(characters);
    }

    private void CurrencyStats(CharacterConfiguration[] characters)
    {
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Currency:");

        using var style = ImRaii.PushStyle(ImGuiStyleVar.CellPadding, new Vector2(0.5f, 0.5f));
        using var table = ImRaii.Table("##CurrencyStatsTable", 4, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##Icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X);
        ImGui.TableSetupColumn("##Num");
        ImGui.TableSetupColumn("##Icon2", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X);
        ImGui.TableSetupColumn("##Num2");

        foreach (var currency in Enum.GetValues<Currency>())
        {
            // We skip these as they are duplicates and aren't saved
            if (currency is Currency.Gil or Currency.StormSeals or Currency.SerpentSeals)
                continue;

            var count = characters.Sum(c => c.GetCurrencyCount(currency));
            if (count == 0)
                continue;

            ImGui.TableNextColumn();
            Helper.DrawIcon(IconList[currency]);

            ImGui.TableNextColumn();
            ImGui.AlignTextToFramePadding();
            Helper.HoverableText($"x{count:N0}", currency.ToName());
        }
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

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Teleport:");

        using var indent = ImRaii.PushIndent(10.0f);
        if (teleports > 0)
        {
            using var table = ImRaii.Table("##TeleportStatsTable", 2, 0, new Vector2(450 * ImGuiHelpers.GlobalScale, 0));
            if (!table.Success)
                return;

            ImGui.TableSetupColumn("##TeleportStat", ImGuiTableColumnFlags.WidthStretch, 0.6f);
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

            #region Savings
            var buffed = new Dictionary<TeleportBuff, (long, long)>();
            foreach (var buff in (TeleportBuff[]) Enum.GetValues(typeof(TeleportBuff)))
            {
                var count = characters.Sum(c => c.TeleportsWithBuffs.TryGetValue(buff, out var value) ? value : 0);
                var savings = characters.Sum(c => c.TeleportSavingsWithBuffs.TryGetValue(buff, out var value) ? value : 0);
                if (count == 0)
                    continue;

                buffed[buff] = (count, savings);
            }

            if (buffed.Count > 0)
            {
                ImGui.TableNextColumn();
                ImGuiHelpers.ScaledDummy(5.0f);

                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Savings");

                using var innerIndent = ImRaii.PushIndent(10.0f);
                var currentBuff = Plugin.GetCurrentTeleportBuff();
                foreach (var (buff, (count, savings)) in buffed)
                {
                    ImGui.TableNextRow();

                    var color = ImGuiColors.HealerGreen;
                    if (buff == currentBuff)
                        color = ImGuiColors.DalamudYellow;

                    ImGui.TableNextColumn();
                    ImGui.TextColored(color, buff.ToName());

                    var stat = $"{count} times";
                    if (savings > 0)
                        stat += $" (saved {savings:N0} gil)";

                    ImGui.TableNextColumn();
                    ImGui.TextUnformatted(stat);
                }
            }
            #endregion

            ImGui.TableNextColumn();
            ImGuiHelpers.ScaledDummy(5.0f);

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Tickets");

            ImGui.TableNextRow();

            ImGui.TableNextColumn();
            using var bottomIndent = ImRaii.PushIndent(10.0f);
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
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "You haven't used teleport yet!");
        }

        ImGuiHelpers.ScaledDummy(5.0f);
    }

    private void RepairStats(CharacterConfiguration[] characters)
    {
        var repairs = characters.Sum(c => c.Repairs);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Repair:");

        using var indent = ImRaii.PushIndent(10.0f);
        if (repairs > 0)
        {
            using var table = ImRaii.Table("##RepairStatsTable", 2, 0, new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
            if (!table.Success)
                return;

            ImGui.TableSetupColumn("##RepairStat", ImGuiTableColumnFlags.WidthStretch, 0.6f);
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
        }
        else
        {
            ImGui.TextColored(ImGuiColors.DalamudOrange, "You haven't used the NPC to repair yet");
        }
    }
}
