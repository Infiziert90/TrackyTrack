using System.Diagnostics;
using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using Dalamud.Interface.Components;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Lumina.Excel.GeneratedSheets;
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

    private static readonly CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture) { HasHeaderRecord = false };

    public class ExportLoot
    {
        public uint ItemId { get; set; }
        public string Name { get; set; }
        public uint Amount { get; set; }

        public ExportLoot(uint id, uint amount)
        {
            ItemId = id;
            Name = Utils.ToStr(ItemSheet.GetRow(id)!.Name);
            Amount = amount;
        }
    }

    public sealed class ExportMap : ClassMap<ExportLoot>
    {
        public ExportMap()
        {
            Map(m => m.ItemId).Index(0).Name("ItemId");
            Map(m => m.Name).Index(1).Name("Name");
            Map(m => m.Amount).Index(2).Name("Amount");
        }
    }

    private void CofferTab()
    {
        if (ImGui.BeginTabItem("Retainer"))
        {
            if (ImGui.BeginTabBar("##RetainerTabBar"))
            {
                var characters = Plugin.CharacterStorage.Values.ToArray();
                if (!characters.Any())
                {
                    Helper.NoVentureCofferData();

                    ImGui.EndTabBar();
                    ImGui.EndTabItem();
                    return;
                }

                RetainerStats(characters);

                RetainerHistory(characters);

                VentureCoffers(characters);

                RetainerAdvanced();

                ImGui.EndTabBar();
            }
            ImGui.EndTabItem();
        }
    }

    private void RetainerStats(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Stats"))
            return;

        var history = characters.SelectMany(c => c.VentureStorage.History.Values).ToArray();
        var quickHistory = history.Where(v => v.IsQuickVenture).ToArray();

        var totalNormal = history.Count(c => !c.IsQuickVenture);
        var totalQuick = quickHistory.Length;

        if (TotalQuick != totalQuick)
        {
            TotalQuick = totalQuick;

            // Coffers only drop from max level retainers
            CofferVentures = quickHistory.Count(v => v.MaxLevel);
            TotalCoffers = quickHistory.Count(v => v.Primary.Item == 32161);

            // All valid gear is rarity green or higher
            (Item Item, bool HQ)[] validGear = quickHistory.Select(v => (ItemSheet.GetRow(v.Primary.Item)!, v.Primary.HQ)).Where(i => i.Item1.Rarity > 1).ToArray();
            TotalLvl = validGear.Sum(i => i.Item.LevelItem.Row);
            TotalSeals = validGear.Sum(i => GCSupplySheet.GetRow(i.Item.LevelItem.Row)!.SealsExpertDelivery);
            TotalFCPoints = validGear.Sum(i =>
            {
                var iLvL = i.Item.LevelItem.Row;
                if ((iLvL & 1) == 1)
                    iLvL += 1;

                return (i.HQ ? 3.0 : 1.5) * iLvL;
            });
        }

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.TextColored(ImGuiColors.DalamudViolet, "Venture Types:");
        if (ImGui.BeginTable($"##TotalStatsTable", 2))
        {
            ImGui.TableSetupColumn("##stat", 0, 0.6f);
            ImGui.TableSetupColumn("##amount");

            ImGui.Indent(10.0f);
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Normal");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{totalNormal:N0} time{(totalNormal > 1 ? "s" : "")}");
            ImGui.TableNextColumn();
            ImGui.TextColored(ImGuiColors.HealerGreen, "Quick");
            ImGui.TableNextColumn();
            ImGui.TextUnformatted($"{totalQuick:N0} time{(totalQuick > 1 ? "s" : "")}");
            ImGui.Unindent(10.0f);

            if (TotalQuick > 0)
            {
                ImGui.TableNextColumn();
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.TextColored(ImGuiColors.DalamudViolet, "Average:");
                ImGui.Indent(10.0f);

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
                ImGui.Unindent(10.0f);
            }

            if (CofferVentures > 0)
            {
                ImGui.TableNextColumn();
                ImGuiHelpers.ScaledDummy(5.0f);
                ImGui.TextColored(ImGuiColors.DalamudViolet, "Venture Coffers:");
                ImGui.Indent(10.0f);

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
                var avg = (TotalCoffers/ (double) CofferVentures) * RetainerAvgInput;
                ImGui.AlignTextToFramePadding();
                ImGui.TextColored(ImGuiColors.HealerGreen, "Chance in");
                ImGui.SameLine();
                ImGui.SetNextItemWidth(width);
                ImGui.InputInt("##AvgInput", ref RetainerAvgInput, 0);
                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"{avg:F2} coffer{(avg > 1 ? "s" : "")}");
                ImGui.Unindent(10.0f);
            }

            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void RetainerHistory(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("History"))
            return;

        characters = characters.Where(c => c.VentureStorage.History.Any()).ToArray();
        if (!characters.Any())
        {
            Helper.NoRetainerData();

            ImGui.EndTabItem();
            return;
        }

        var existingCharacters = characters.Select(character => $"{character.CharacterName}@{character.World}").ToArray();
        var selectedCharacter = RetainerSelectedCharacter;
        ImGui.Combo("##existingCharacters", ref selectedCharacter, existingCharacters, existingCharacters.Length);
        if (selectedCharacter != RetainerSelectedCharacter)
        {
            RetainerSelectedCharacter = selectedCharacter;
            RetainerSelectedHistory = 0;
        }

        var selectedChar = characters[RetainerSelectedCharacter];
        var history = selectedChar.VentureStorage.History.Reverse().Select(pair => $"{pair.Key}").ToArray();

        ImGui.Combo("##voyageSelection", ref RetainerSelectedHistory, history, history.Length);
        Helper.DrawArrows(ref RetainerSelectedHistory, history.Length);

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        var ventureResult = selectedChar.VentureStorage.History.Reverse().ToArray()[RetainerSelectedHistory].Value;

        if (ImGui.BeginTable($"##HistoryTable", 3))
        {
            ImGui.TableSetupColumn("##icon", 0, 0.2f);
            ImGui.TableSetupColumn("##item");
            ImGui.TableSetupColumn("##amount", 0, 0.2f);

            ImGui.Indent(10.0f);
            foreach (var ventureItem in ventureResult.Items)
            {
                if (!ventureItem.Valid)
                    continue;

                var item = ItemSheet.GetRow(ventureItem.Item)!;

                ImGui.TableNextColumn();
                DrawIcon(item.Icon);
                ImGui.TableNextColumn();

                var name = Utils.ToStr(item.Name);
                ImGui.TextUnformatted(name);

                ImGui.TableNextColumn();
                ImGui.TextUnformatted($"x{ventureItem.Count}");
                ImGui.TableNextRow();
            }
            ImGui.Unindent(10.0f);

            ImGui.EndTable();
        }
        ImGui.EndTabItem();
    }

    private void VentureCoffers(CharacterConfiguration[] characters)
    {
        if (!ImGui.BeginTabItem("Venture Coffers"))
            return;

        var characterCoffers = characters.Where(c => c.Coffer.Opened > 0).ToList();
        if (!characterCoffers.Any())
        {
            Helper.NoVentureCofferData();
            ImGui.EndTabItem();
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
            var item = ItemSheet.GetRow(pair.Key)!;
            var count = pair.Value;
            var percentage = (pair.Key != 8841 ? count / 2.0 : count) / opened * 100.0;
            return new Utils.SortedEntry(item.RowId, item.Icon, Utils.ToStr(item.Name), count, percentage);
        });

        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Opened: {opened:N0}");
        ImGui.TextColored(ImGuiColors.ParsedOrange, $"Obtained: {dict.Count(pair => pair.Value > 0)} out of {VentureCoffer.Content.Count}");
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

        ImGuiHelpers.ScaledDummy(10.0f);
        if (ImGui.Button("Export to clipboard"))
            ExportToClipboard(dict);

        ImGui.EndTabItem();
    }

    private void RetainerAdvanced()
    {
        if (!ImGui.BeginTabItem("Advanced"))
            return;

        ImGuiHelpers.ScaledDummy(5.0f);

        Helper.WrappedError("This interface is for resetting your retainer history." +
                            "\nBe careful and read the tooltips before doing anything.");

        ImGuiHelpers.ScaledDummy(5.0f);
        ImGui.Separator();
        ImGuiHelpers.ScaledDummy(5.0f);

        if (ImGui.Button("Reset Character") && ImGui.GetIO().KeyCtrl)
        {
            if (Plugin.CharacterStorage.TryGetValue(Plugin.ClientState.LocalContentId, out var character))
            {
                character.VentureStorage = new Retainer();

                Plugin.ConfigurationBase.SaveCharacterConfig();
                Plugin.PluginInterface.UiBuilder.AddNotification("Deleted the retainer history of your current character", "[Tracky Track]", NotificationType.Success);
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
                Plugin.PluginInterface.UiBuilder.AddNotification("Deleted the retainer history of all characters", "[Tracky Track]", NotificationType.Success);
            }

            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
                ImGui.SetTooltip("Deletes the retainer history for all characters." +
                                 "\nThis doesn't touch venture coffer obtained results" +
                                 "\nHold Control to delete");
        }
        else
        {
            ImGuiComponents.DisabledButton("Reset All Characters");
            Helper.WrappedError("Detected multiple FFXIV instances." +
                                "\nPlease close all other instances of the game.");
        }

        ImGui.EndTabItem();
    }

    private void ExportToClipboard(Dictionary<uint, uint> dict)
    {
        try
        {
            using var writer = new StringWriter();
            using var csv = new CsvWriter(writer, CsvConfig);

            csv.Context.RegisterClassMap(new ExportMap());

            csv.WriteHeader<ExportLoot>();
            csv.NextRecord();

            foreach (var detailedLoot in dict.Select(pair => new ExportLoot(pair.Key, pair.Value)))
            {
                csv.WriteRecord(detailedLoot);
                csv.NextRecord();
            }

            ImGui.SetClipboardText(writer.ToString());

            Plugin.ChatGui.Print(Utils.SuccessMessage("Export to clipboard done."));
        }
        catch (Exception e)
        {
            Plugin.Log.Error(e.StackTrace ?? "No Stacktrace");
        }
    }
}
