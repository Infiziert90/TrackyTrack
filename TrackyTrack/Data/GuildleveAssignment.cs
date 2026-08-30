namespace TrackyTrack.Data;

public class GuildleveAssignmentData
{
    public uint RowId; // GuildleveAssignment RowId
    public byte CategoryRowId; // GuildleveAssignmentCategory RowId
    public byte CategoryIndex; // GuildleveAssignmentCategory.Category index
    public List<ushort> LeveIds = [];
}
