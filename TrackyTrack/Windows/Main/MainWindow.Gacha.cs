using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using OtterGui.Raii;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private long LastRefresh;
    private readonly Dictionary<uint, bool> Unlocked = [];

    private void GachaTab()
    {
        if (ImGui.BeginTabItem("Gacha"))
        {
            if (ImGui.BeginTabBar("##GachaTabBar"))
            {
                // Refreshes the unlocked dict every 5s
                RefreshUnlocks();

                GachaThreeZero();

                GachaFourZero();

                Sanctuary();

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void GachaThreeZero()
    {
        if (!ImGui.BeginTabItem("Gacha 3.0"))
            return;

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
            Helper.NoGachaData("Grand Company 3.0");
            ImGui.EndTabItem();
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in Data.GachaThreeZero.Content)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaThreeZero.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaThreeZero.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value;
            var percentage = (double) count / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        var showUnlock = Plugin.Configuration.ShowUnlockCheckmark;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Obtained: {dict.Count(pair => pair.Value > 0)} out of {Data.GachaThreeZero.Content.Count}");
        if (ImGui.BeginTable($"##GachaThreeZeroTable", showUnlock ? 5 : 4, ImGuiTableFlags.Sortable))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.NoSort, 0.17f);
            ImGui.TableSetupColumn("Item##item");
            ImGui.TableSetupColumn("Num##amount", 0, 0.2f);
            ImGui.TableSetupColumn("Pct##percentage", ImGuiTableColumnFlags.DefaultSort, 0.25f);
            if (showUnlock)
                ImGui.TableSetupColumn("##unlocked", ImGuiTableColumnFlags.NoSort, 0.1f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var sortedEntry in Utils.SortEntries(unsortedList, ImGui.TableGetSortSpecs().Specs))
            {
                ImGui.TableNextColumn();
                Helper.DrawIcon(sortedEntry.Icon);
                ImGui.TableNextColumn();

                ImGui.TextUnformatted(sortedEntry.Name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(sortedEntry.Name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{sortedEntry.Count}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sortedEntry.Percentage:F2}%");

                if (showUnlock)
                {
                    Unlocked.TryGetValue(sortedEntry.Id, out var unlocked);

                    ImGui.TableNextColumn();
                    using (Plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                    using (ImRaii.PushColor(ImGuiCol.Text, unlocked ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed))
                    {
                        ImGui.TextUnformatted(unlocked ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Times.ToIconString());
                    }
                }

                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Missing:");
        if (ImGui.BeginTable($"##GachaThreeMissingTable", showUnlock ? 3 : 2))
        {
            ImGui.TableSetupColumn("##missingItemIcon", 0, 0.17f);
            ImGui.TableSetupColumn("Item##missingItem");
            if (showUnlock)
                ImGui.TableSetupColumn("##unlocked", ImGuiTableColumnFlags.NoSort, 0.1f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var itemId in Data.GachaThreeZero.Content.Where(i => dict[i] == 0))
            {
                var item = ItemSheet.GetRow(itemId)!;

                ImGui.TableNextColumn();
                Helper.DrawIcon(item.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(item.Name);

                if (showUnlock)
                {
                    Unlocked.TryGetValue(itemId, out var unlocked);

                    ImGui.TableNextColumn();
                    using (Plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                    using (ImRaii.PushColor(ImGuiCol.Text, unlocked ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed))
                    {
                        ImGui.TextUnformatted(unlocked ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Times.ToIconString());
                    }
                }

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
            Helper.NoGachaData("Grand Company 4.0");
            ImGui.EndTabItem();
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in Data.GachaFourZero.Content)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaFourZero.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaFourZero.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value;
            var percentage = (double) count / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        var showUnlock = Plugin.Configuration.ShowUnlockCheckmark;
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Obtained: {dict.Count(pair => pair.Value > 0)} out of {Data.GachaFourZero.Content.Count}");
        if (ImGui.BeginTable($"##GachaFourZeroTable", showUnlock ? 5 : 4, ImGuiTableFlags.Sortable))
        {
            ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.NoSort, 0.17f);
            ImGui.TableSetupColumn("Item##item");
            ImGui.TableSetupColumn("Num##amount", 0, 0.2f);
            ImGui.TableSetupColumn("Pct##percentage", ImGuiTableColumnFlags.DefaultSort, 0.25f);
            if (showUnlock)
                ImGui.TableSetupColumn("##unlocked", ImGuiTableColumnFlags.NoSort, 0.1f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var sortedEntry in Utils.SortEntries(unsortedList, ImGui.TableGetSortSpecs().Specs))
            {
                ImGui.TableNextColumn();
                Helper.DrawIcon(sortedEntry.Icon);
                ImGui.TableNextColumn();

                ImGui.TextUnformatted(sortedEntry.Name);
                if (ImGui.IsItemHovered())
                    ImGui.SetTooltip(sortedEntry.Name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{sortedEntry.Count}");

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{sortedEntry.Percentage:F2}%");

                if (showUnlock)
                {
                    Unlocked.TryGetValue(sortedEntry.Id, out var unlocked);

                    ImGui.TableNextColumn();
                    using (Plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                    using (ImRaii.PushColor(ImGuiCol.Text, unlocked ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed))
                    {
                        ImGui.TextUnformatted(unlocked ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Times.ToIconString());
                    }
                }

                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, "Missing:");
        if (ImGui.BeginTable("##GachaFourMissingTable", showUnlock ? 3 : 2))
        {
            ImGui.TableSetupColumn("##missingItemIcon", 0, 0.17f);
            ImGui.TableSetupColumn("Item##missingItem");
            if (showUnlock)
                ImGui.TableSetupColumn("##unlocked", ImGuiTableColumnFlags.NoSort, 0.1f);

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var itemId in Data.GachaFourZero.Content.Where(i => dict[i] == 0))
            {
                var item = ItemSheet.GetRow(itemId)!;

                ImGui.TableNextColumn();
                Helper.DrawIcon(item.Icon);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted(item.Name);

                if (showUnlock)
                {
                    Unlocked.TryGetValue(itemId, out var unlocked);

                    ImGui.TableNextColumn();
                    using (Plugin.PluginInterface.UiBuilder.IconFontFixedWidthHandle.Push())
                    using (ImRaii.PushColor(ImGuiCol.Text, unlocked ? ImGuiColors.HealerGreen : ImGuiColors.DalamudRed))
                    {
                        ImGui.TextUnformatted(unlocked ? FontAwesomeIcon.Check.ToIconString() : FontAwesomeIcon.Times.ToIconString());
                    }
                }

                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void Sanctuary()
    {
        if (!ImGui.BeginTabItem("Sanctuary"))
            return;

        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (!characters.Any())
        {
            Helper.NoVentureCofferData();
            ImGui.EndTabItem();
            return;
        }

        var characterGacha = characters.Where(c => c.GachaSanctuary.Opened > 0).ToList();

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in Data.Sanctuary.Content)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterGacha.SelectMany(c => c.GachaSanctuary.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterGacha.Select(c => c.GachaSanctuary.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value;
            var percentage = (Data.Sanctuary.MultiRewardItems.Contains(pair.Key) ? count / 5.0 : count ) / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Found: {dict.Count(pair => pair.Value > 0)} out of {Data.Sanctuary.Content.Count} items");
        if (ImGui.BeginTable($"##SanctuaryTable", 4, ImGuiTableFlags.Sortable))
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
                Helper.DrawIcon(sortedEntry.Icon);
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

        ImGuiHelpers.ScaledDummy(10.0f);

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Missing:");
        if (ImGui.BeginTable($"##SanctuaryMissingTable", 2))
        {
            ImGui.TableSetupColumn("##missingItemIcon", 0, 0.17f);
            ImGui.TableSetupColumn("Item##missingItem");

            ImGui.TableHeadersRow();

            ImGui.Indent(10.0f);
            foreach (var itemId in Data.Sanctuary.Content.Where(i => dict[i] == 0))
            {
                var item = ItemSheet.GetRow(itemId)!;

                ImGui.TableNextColumn();
                Helper.DrawIcon(item.Icon);

                ImGui.TableNextColumn();
                if (ImGui.Selectable(Utils.ToStr(item.Name)))
                    ImGui.SetClipboardText(Utils.ToStr(item.Name));

                ImGui.TableNextRow();
            }

            ImGui.Unindent(10.0f);
            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void RefreshUnlocks()
    {
        if (Environment.TickCount64 < LastRefresh)
            return;

        LastRefresh = Environment.TickCount64 + 5000; // 5s

        foreach (var item in Data.GachaThreeZero.Content)
            Unlocked[item] = CheckUnlockStatus(item);

        foreach (var item in Data.GachaFourZero.Content)
            Unlocked[item] = CheckUnlockStatus(item);
    }

    private unsafe bool CheckUnlockStatus(uint id)
    {
        var item = Sheets.ItemSheet.GetRow(id);
        if (item == null || item.ItemAction.Row == 0)
            return false;

        var action = item.ItemAction.Value;
        if (action == null)
            return false;

        var instance = UIState.Instance();
        return action.Type switch
        {
            1322 => instance->PlayerState.IsMountUnlocked(action.Data[0]),
            853 => instance->IsCompanionUnlocked(action.Data[0]),
            _ => false
        };
    }
}
