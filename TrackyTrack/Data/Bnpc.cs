using System.Runtime.InteropServices;

namespace TrackyTrack.Data;

public static class Bnpc
{
    public static readonly HashSet<string> UploadHashes = [];
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct SpawnEffectLayout
{
    public ushort Id;
    public ushort Param;
    public float Duration;
    public uint SourceActorId;
}

[StructLayout(LayoutKind.Sequential, Pack = 1)]
public unsafe struct SpawnPacketLayout
{
    public uint GimmikId;
    private byte Unk2;
    private byte Unk3;
    public byte GMRank;
    private byte Unk5;

    public byte AgressionMode;
    public byte OnlineStatus;

    private byte Unk6;

    public byte Pose;

    private uint Unk7;

    public uint TargetEntityId;

    private uint Unk8;
    private uint Unk9;
    private uint Unk10;

    public ulong MainWeaponModel;
    public ulong OffHandModel;
    public ulong CraftingToolModel;

    private uint Unk11;
    private uint Unk12;

    public uint BNPCBaseId;
    public uint BNPCNameId;
    public uint LevelId;

    private uint Unk13;

    public uint DirectoryId;
    public uint SpawnerEntityId;  // This gets compared with LocalEntityId
    public uint ParentActorId;

    public uint MaxHP;
    public uint CurrHP;

    public uint DisplayFlags;

    public ushort FateId;

    public ushort CurrMP;
    public ushort MaxMP;

    private ushort Unk14;

    public ushort ModelChar;
    public ushort Rotation;

    // Unknown Shifted
    private ushort ActiveMinion;
    private byte SpawnIndex;
    private byte State;
    private byte PersistentEmote;
    private byte ModelType;
    private byte SubType;
    private byte Voice;
    private ushort Unk15;

    // Extra bytes before things make sense
    public byte CharacterManagerDeleteIndex;
    private fixed byte Padding1[3];
    public byte ByteCheckedInCode;
    private fixed byte Padding2[2];

    // Things are correct again
    public byte EnemyType;
    public byte Level;
    public byte ClassJob;

    private byte Unk16;
    private ushort Unk17;

    public byte CurrentMount;
    public byte MountHead;
    public byte MountBody;
    public byte MountFeet;
    public byte MountColor;
    public byte Scale;
    public ushort ElementalLevel;
    public ushort Element;

    [MarshalAs(UnmanagedType.ByValArray, SizeConst = 30)]
    public SpawnEffectLayout[] NpcSpawnEffects;

    private fixed byte Padding[3];

    public float X;
    public float Y;
    public float Z;
}