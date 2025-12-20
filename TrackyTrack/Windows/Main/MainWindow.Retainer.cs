using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks;
using Dalamud.Interface.Components;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Lumina.Excel.Sheets;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private int RetainerSelectedCharacter;
    private int RetainerSelectedHistory;
    private int RetainerAvgInput = 100;

    private int TotalQuick;

    // cache
    private int CofferVentures;
    private int TotalCoffers;

    private double TotalLvl;
    private double TotalSeals;
    private double TotalFCPoints;

    private bool RetainerTaskRunning;
    private int LastRetainerHistoryCount;
    private readonly ConcurrentDictionary<uint, uint> AllItemsHistory = new();

    private Tabs SelectedRetainerTab;
    private static readonly Tabs[] RetainerTabs = [Tabs.Received, Tabs.History, Tabs.VentureCoffers, Tabs.Advanced];

    private void CofferTab()
    {
        using var tabItem = ImRaii.TabItem("Retainer");
        if (!tabItem.Success)
            return;

        var characters = Plugin.CharacterStorage.Values.ToArray();
        if (characters.Length == 0)
        {
            Helper.NoVentureCofferData();
            return;
        }

        // Fills history cache if total has changed
        FillRetainerItemHistory(characters);

        var pos = ImGui.GetCursorPos();

        var nameDict = TabHelper.TabSize(RetainerTabs);
        var childSize = new Vector2(nameDict.Select(pair => pair.Value.Width).Max(), 0);
        using (var tabChild = ImRaii.Child("Tabs", childSize, true))
        {
            if (tabChild.Success)
            {
                if (ImGui.Selectable("Stats", SelectedRetainerTab == Tabs.Stats))
                    SelectedRetainerTab = Tabs.Stats;

                ImGui.Spacing();
                ImGui.Separator();
                ImGui.Spacing();

                foreach (var (id, (name, _)) in nameDict)
                    if (ImGui.Selectable(name, SelectedRetainerTab == id))
                        SelectedRetainerTab = id;
            }
        }

        ImGui.SetCursorPos(pos with {X = pos.X + childSize.X});
        using (var contentChild = ImRaii.Child("Content", Vector2.Zero, true))
        {
            if (contentChild.Success)
            {
                switch (SelectedRetainerTab)
                {
                    case Tabs.Stats:
                        RetainerStats(characters);
                        break;
                    case Tabs.Received:
                        RetainerAllOverview();
                        break;
                    case Tabs.History:
                        RetainerHistory(characters);
                        break;
                    case Tabs.VentureCoffers:
                        VentureCoffers(characters);
                        break;
                    case Tabs.Advanced:
                        RetainerAdvanced();
                        break;
                }
            }
        }
    }

    private void RetainerStats(CharacterConfiguration[] characters)
    {
        var history = characters.SelectMany(c => c.VentureStorage.History.Values).ToArray();
        var quickHistory = history.Where(v => v.IsQuickVenture).ToArray();

        var totalNormal = history.Length - quickHistory.Length;
        var totalQuick = quickHistory.Length;

        if (TotalQuick != totalQuick)
        {
            TotalQuick = totalQuick;

            // Coffers only drop from max level retainers
            CofferVentures = quickHistory.Count(v => v.MaxLevel);
            TotalCoffers = quickHistory.Count(v => v.Primary.Item == 32161);

            // All valid gear is rarity green or higher
            (Item Item, bool HQ)[] validGear = quickHistory.Select(v => (Sheets.GetItem(v.Primary.Item), v.Primary.HQ)).Where(i => i.Item1.Rarity > 1).ToArray();
            TotalLvl = validGear.Sum(i => i.Item.LevelItem.RowId);
            TotalSeals = validGear.Sum(i => Sheets.GCSupplySheet.GetRow(i.Item.LevelItem.RowId).SealsExpertDelivery);
            TotalFCPoints = validGear.Sum(i =>
            {
                var iLvL = i.Item.LevelItem.RowId;
                if ((iLvL & 1) == 1)
                    iLvL += 1;

                return (i.HQ ? 3.0 : 1.5) * iLvL;
            });
        }

        ImGui.TextColored(ImGuiColors.DalamudViolet, "Venture Types:");
        using var table = ImRaii.Table("##TotalStatsTable", 2);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##stat", ImGuiTableColumnFlags.WidthStretch, 0.6f);
        ImGui.TableSetupColumn("##amount");

        using (ImRaii.PushIndent(10.0f))
        {
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Normal");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{totalNormal:N0} time{(totalNormal > 1 ? "s" : "")}");
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Quick");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{totalQuick:N0} time{(totalQuick > 1 ? "s" : "")}");
        }

        if (TotalQuick > 0)
        {
            ImGui.TableNextColumn();
            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Average:");

            using (ImRaii.PushIndent(10.0f))
            {
                ImGui.TableNextRow();

                var avgLvL = TotalLvl / TotalQuick;
                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "iLvL");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{avgLvL:F2}");

                var avgFCPoints = TotalFCPoints / TotalQuick;
                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "FC Points");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{avgFCPoints:F2}");

                var avgSeals = TotalSeals / TotalQuick;
                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "GC Seals");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{avgSeals:F2}");
            }
        }

        if (CofferVentures > 0)
        {
            ImGui.TableNextColumn();
            ImGuiHelpers.ScaledDummy(5.0f);
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Venture Coffers:");

            using (ImRaii.PushIndent(10.0f))
            {
                ImGui.TableNextRow();

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Obtained");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{TotalCoffers:N0}");

                ImGui.TableNextColumn();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Valid");
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{CofferVentures:N0} venture{(CofferVentures > 1 ? "s" : "")}");

                ImGui.TableNextColumn();
                var width = ImGui.CalcTextSize("10000").X * 1.2f;
                var avg = TotalCoffers / (double)CofferVentures * RetainerAvgInput;
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Chance in");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(width);
                ImGui.InputInt("##AvgInput", ref RetainerAvgInput, 0);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{avg:F2} coffer{(avg > 1 ? "s" : "")}");
            }
        }
    }

    private void RetainerAllOverview()
    {
        if (RetainerTaskRunning)
        {
            ImGui.TextColored(ImGuiColors.DalamudViolet, "Rebuilding Cache...");
            return;
        }

        using var child = ImRaii.Child("ItemTableChild");
        if (!child.Success)
            return;

        using var table = ImRaii.Table("##RetainerAllTable", 3);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthFixed, Helper.IconSize.X);
        ImGui.TableSetupColumn("##item");
        ImGui.TableSetupColumn("##amount", ImGuiTableColumnFlags.WidthStretch, 0.2f);

        var items = AllItemsHistory.OrderByDescending(pair => pair.Value).ToArray();
        using var clipper = new ListClipper(items.Length, itemHeight: Helper.IconSize.Y * ImGuiHelpers.GlobalScale);
        foreach (var i in clipper.Rows)
        {
            var (itemId, count) = items[i];
            var item = Sheets.GetItem(itemId);

            ImGui.TableNextColumn();
            Helper.DrawIcon(Utils.CheckItemAction(item));

            ImGui.TableNextColumn();
            var name = item.Name.ToString();
            if (ImGui.Selectable(name))
                ImGui.SetClipboardText(name);

            if (ImGui.IsItemHovered())
                ImGui.SetTooltip($"{name}\nClick to copy");

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{count:N0}");

            ImGui.TableNextRow();
        }
    }

    private void RetainerHistory(CharacterConfiguration[] characters)
    {
        characters = characters.Where(c => c.VentureStorage.History.Count != 0).ToArray();
        if (characters.Length == 0)
        {
            Helper.NoRetainerData();
            return;
        }

        var selectedCharacter = RetainerSelectedCharacter;
        Helper.ClippedCombo("##existingCharacters", ref selectedCharacter, characters, character => $"{character.CharacterName}@{character.World}");
        if (selectedCharacter != RetainerSelectedCharacter)
        {
            RetainerSelectedCharacter = selectedCharacter;
            RetainerSelectedHistory = 0;
        }

        var reversedHistory = characters[RetainerSelectedCharacter].VentureStorage.History.Reverse().ToArray();

        Helper.ClippedCombo("##historySelection", ref RetainerSelectedHistory, reversedHistory, pair => $"{pair.Key}");
        Helper.DrawArrows(ref RetainerSelectedHistory, reversedHistory.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var ventureResult = reversedHistory[RetainerSelectedHistory].Value;
        using var table = ImRaii.Table("##HistoryTable", 3);
        if (!table.Success)
            return;

        ImGui.TableSetupColumn("##icon", ImGuiTableColumnFlags.WidthStretch, 0.2f);
        ImGui.TableSetupColumn("##item");
        ImGui.TableSetupColumn("##amount", ImGuiTableColumnFlags.WidthStretch, 0.2f);

        foreach (var ventureItem in ventureResult.Items)
        {
            if (!ventureItem.Valid)
                continue;

            var item = Sheets.GetItem(ventureItem.Item);

            ImGui.TableNextColumn();
            Helper.DrawIcon(Utils.CheckItemAction(item));
            ImGui.TableNextColumn();

            var name = item.Name.ToString();
            ImGui.TextUnformatted(name);

            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"x{ventureItem.Count}");
            ImGui.TableNextRow();
        }
    }

    private void VentureCoffers(CharacterConfiguration[] characters)
    {
        if (!Plugin.Configuration.EnableVentureCoffers)
        {
            Helper.TrackingDisabled("Venture Coffer tracking has been disabled in the config.");
            return;
        }

        var characterCoffers = characters.Where(c => c.Coffer.Opened > 0).ToArray();
        if (characterCoffers.Length == 0)
        {
            Helper.NoVentureCofferData();
            return;
        }

        // fill dict in order
        var dict = new Dictionary<uint, uint>();
        foreach (var item in VentureCoffer.Content)
            dict.Add(item, 0);

        // fill dict with real values
        foreach (var pair in characterCoffers.SelectMany(c => c.Coffer.Obtained))
            dict[pair.Key] += pair.Value;

        var opened = characterCoffers.Select(c => c.Coffer.Opened).Sum();
        var unsortedList = dict.Where(pair => pair.Value > 0).Select(pair =>
        {
            return Utils.SortedEntry.FromItem(Sheets.GetItem(pair.Key), pair.Value, 0, 0, Utils.ToChance(pair.Key != 8841 ? pair.Value / 2.0 : pair.Value, opened));
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Received From Coffers: {dict.Count(pair => pair.Value > 0)} out of {VentureCoffer.Content.Count}");
        new SimpleTable<Utils.SortedEntry>("##HistoryTable", Utils.SortEntries, ImGuiTableFlags.Sortable)
            .EnableSortSpec()
            .AddIconColumn("##icon", entry => Helper.DrawIcon(entry.Icon))
            .AddColumn("Item##item", entry => Helper.HoverableText(entry.Name))
            .AddColumn("Num##amount", entry => ImGui.TextUnformatted($"x{entry.Obtained}"), initWidth: 0.2f)
            .AddColumn("Pct##percentage", entry => ImGui.TextUnformatted($"{entry.Percentage:F2}%"), ImGuiTableColumnFlags.DefaultSort, 0.25f)
            .Draw(unsortedList);

        ImGuiHelpers.ScaledDummy(10.0f);
        if (ImGui.Button("Export to clipboard"))
            Export.ExportToClipboard(dict);
    }

    private void RetainerAdvanced()
    {
        Helper.WrappedError("This interface is for resetting your retainer history.\nBe careful and read the tooltips before doing anything.");

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Button("Reset Character") && ImGui.GetIO().KeyCtrl)
        {
            if (Plugin.CharacterStorage.TryGetValue(Plugin.PlayerState.ContentId, out var character))
            {
                character.VentureStorage = new Retainer();

                Plugin.ConfigurationBase.SaveCharacterConfig();
                Utils.AddNotification("History for your character removed", NotificationType.Success);
            }
        }

        if (ImGui.IsItemHovered())
            ImGui.SetTooltip("Deletes the retainer history for your current character." +
                             "\nThis doesn't touch venture coffer obtained results" +
                             "\nHold Control to delete");

        ImGuiHelpers.ScaledDummy(5.0f);

        var multipleProcesses = Process.GetProcessesByName("ffxiv_dx11").Length > 1;
        if (!multipleProcesses)
        {
            if (ImGui.Button("Reset All Characters") && ImGui.GetIO().KeyCtrl)
            {
                foreach (var character in Plugin.CharacterStorage.Values)
                    character.VentureStorage = new Retainer();

                Plugin.ConfigurationBase.SaveAll();
                Utils.AddNotification("History for all characters removed", NotificationType.Success);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Deletes the retainer history for all characters." +
                                 "\nThis doesn't touch venture coffer obtained results" +
                                 "\nHold Control to delete");
        }
        else
        {
            ImGuiComponents.DisabledButton("Reset All Characters");
            Helper.WrappedError("Detected multiple FFXIV instances.\nPlease close all other instances of the game.");
        }
    }

    private void FillRetainerItemHistory(IEnumerable<CharacterConfiguration> characters)
    {
        if (RetainerTaskRunning)
            return;

        var total = characters.Sum(c => c.VentureStorage.History.Count);
        if (LastRetainerHistoryCount != total)
        {
            // We set true outside so that all follow-up functions know this is running
            // In rare cases the follow-up function can run before the new thread starts to execute
            RetainerTaskRunning = true;
            LastRetainerHistoryCount = total;
            AllItemsHistory.Clear();

            Task.Run(() =>
            {
                try
                {
                    foreach (var pair in Plugin.CharacterStorage.Values.Where(c => c.VentureStorage.History.Count > 0).SelectMany(c => c.VentureStorage.History))
                    {
                        foreach (var item in pair.Value.Items)
                        {
                            // Old corruption
                            if (item.Count == 0)
                                continue;

                            if (!AllItemsHistory.TryAdd(item.Item, (uint) item.Count))
                                AllItemsHistory[item.Item] += (uint) item.Count;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Log.Error(ex, "Error while building retainer cache");
                }

                RetainerTaskRunning = false;
            });
        }
    }
}
