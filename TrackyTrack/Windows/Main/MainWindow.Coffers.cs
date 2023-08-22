using System.Globalization;
using System.IO;
using CsvHelper;
using CsvHelper.Configuration;
using Dalamud.Logging;
using TrackyTrack.Data;

namespace TrackyTrack.Windows.Main;

public partial class MainWindow
{
    private static CsvConfiguration CsvConfig = new(CultureInfo.InvariantCulture) { HasHeaderRecord = false };

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
            return new Utils.SortedEntry(item.Icon, Utils.ToStr(item.Name), count, percentage);
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
        ImGui.EndTabItem();

        ImGuiHelpers.ScaledDummy(10.0f);
        if (ImGui.Button("Export to clipboard"))
            ExportToClipboard(dict);
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

            Plugin.ChatGui.Print("Export to clipboard done.");
        }
        catch (Exception e)
        {
            PluginLog.Error(e.StackTrace ?? "No Stacktrace");
        }
    }
}
