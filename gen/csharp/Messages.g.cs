// GENERADO por tools/gen.py — no editar a mano. Fuente: schema/messages.yaml
#nullable enable
using System;
using System.IO;
using System.Text;

namespace MexOrbit.Protocol;

/// <summary>Violación del contrato: campo fuera de rango o mensaje malformado.</summary>
public sealed class ProtocolViolationException : Exception
{
    public ProtocolViolationException(string message) : base(message) { }
}

public static class Wire
{
    public static void WriteVarint(MemoryStream s, ulong v)
    {
        while (v >= 0x80) { s.WriteByte((byte)(v | 0x80)); v >>= 7; }
        s.WriteByte((byte)v);
    }
    public static ulong ReadVarint(ReadOnlySpan<byte> b, ref int pos)
    {
        ulong v = 0; int shift = 0;
        while (true)
        {
            if (pos >= b.Length) throw new ProtocolViolationException("varint truncado");
            byte x = b[pos++];
            v |= (ulong)(x & 0x7F) << shift;
            if ((x & 0x80) == 0) return v;
            shift += 7;
            if (shift > 63) throw new ProtocolViolationException("varint demasiado largo");
        }
    }
    public static void WriteTag(MemoryStream s, int tag, int wt) => WriteVarint(s, (ulong)(tag << 3 | wt));
    public static void WriteString(MemoryStream s, string v)
    {
        var bytes = Encoding.UTF8.GetBytes(v);
        WriteVarint(s, (ulong)bytes.Length);
        s.Write(bytes, 0, bytes.Length);
    }
    public static string ReadString(ReadOnlySpan<byte> b, ref int pos)
    {
        int len = checked((int)ReadVarint(b, ref pos));
        if (pos + len > b.Length) throw new ProtocolViolationException("string truncado");
        var s = Encoding.UTF8.GetString(b.Slice(pos, len));
        pos += len;
        return s;
    }
    public static void WriteF32(MemoryStream s, float v)
    {
        Span<byte> tmp = stackalloc byte[4];
        BitConverter.TryWriteBytes(tmp, v);
        if (!BitConverter.IsLittleEndian) tmp.Reverse();
        s.Write(tmp);
    }
    public static float ReadF32(ReadOnlySpan<byte> b, ref int pos)
    {
        if (pos + 4 > b.Length) throw new ProtocolViolationException("fixed32 truncado");
        Span<byte> tmp = stackalloc byte[4];
        b.Slice(pos, 4).CopyTo(tmp);
        if (!BitConverter.IsLittleEndian) tmp.Reverse();
        pos += 4;
        return BitConverter.ToSingle(tmp);
    }
    public static void Skip(ReadOnlySpan<byte> b, ref int pos, int wt)
    {
        switch (wt)
        {
            case 0: ReadVarint(b, ref pos); break;
            case 2: { int len = checked((int)ReadVarint(b, ref pos)); pos += len; break; }
            case 5: pos += 4; break;
            default: throw new ProtocolViolationException($"wiretype desconocido {wt}");
        }
        if (pos > b.Length) throw new ProtocolViolationException("skip fuera de rango");
    }
}

public enum EntityKind
{
    Player = 0,
    Npc = 1,
}

public sealed class Hello
{
    public const int MsgId = 1;
    public ulong ProtocolVersion;
    public string GameTicket = "";

    public void Validate()
    {
        if (ProtocolVersion < 1) throw new ProtocolViolationException("Hello.protocol_version < 1");
        if (ProtocolVersion > 1000) throw new ProtocolViolationException("Hello.protocol_version > 1000");
        if (GameTicket.Length > 512) throw new ProtocolViolationException("Hello.game_ticket demasiado largo");
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 1);
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, ProtocolVersion);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, GameTicket);
        return s.ToArray();
    }

    public static Hello Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 1) throw new ProtocolViolationException($"msg_id {id} != 1");
        var m = new Hello();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.ProtocolVersion = Wire.ReadVarint(b, ref pos); break;
                case 2: m.GameTicket = Wire.ReadString(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }
}

public sealed class MoveIntent
{
    public const int MsgId = 60;
    public ulong Seq;
    public ulong TargetX;
    public ulong TargetY;

    public void Validate()
    {
        if (TargetX < 0) throw new ProtocolViolationException("MoveIntent.target_x < 0");
        if (TargetX > 60000) throw new ProtocolViolationException("MoveIntent.target_x > 60000");
        if (TargetY < 0) throw new ProtocolViolationException("MoveIntent.target_y < 0");
        if (TargetY > 60000) throw new ProtocolViolationException("MoveIntent.target_y > 60000");
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 60);
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, Seq);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, TargetX);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, TargetY);
        return s.ToArray();
    }

    public static MoveIntent Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 60) throw new ProtocolViolationException($"msg_id {id} != 60");
        var m = new MoveIntent();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Seq = Wire.ReadVarint(b, ref pos); break;
                case 2: m.TargetX = Wire.ReadVarint(b, ref pos); break;
                case 3: m.TargetY = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }
}

public sealed class EntitySpawn
{
    public const int MsgId = 52;
    public ulong EntityId;
    public EntityKind Kind;
    public string TypeId = "";
    public string Name = "";
    public ulong Faction;
    public ulong X;
    public ulong Y;
    public float HpPct;
    public ulong Speed;

    public void Validate()
    {
        if (TypeId.Length > 64) throw new ProtocolViolationException("EntitySpawn.type_id demasiado largo");
        if (Name.Length > 64) throw new ProtocolViolationException("EntitySpawn.name demasiado largo");
        if (Faction > 8) throw new ProtocolViolationException("EntitySpawn.faction > 8");
        if (X > 60000) throw new ProtocolViolationException("EntitySpawn.x > 60000");
        if (Y > 60000) throw new ProtocolViolationException("EntitySpawn.y > 60000");
        if (Speed > 2000) throw new ProtocolViolationException("EntitySpawn.speed > 2000");
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 52);
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, (ulong)Kind);
        Wire.WriteTag(s, 3, 2); Wire.WriteString(s, TypeId);
        Wire.WriteTag(s, 4, 2); Wire.WriteString(s, Name);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, Faction);
        Wire.WriteTag(s, 6, 0); Wire.WriteVarint(s, X);
        Wire.WriteTag(s, 7, 0); Wire.WriteVarint(s, Y);
        Wire.WriteTag(s, 8, 5); Wire.WriteF32(s, HpPct);
        Wire.WriteTag(s, 9, 0); Wire.WriteVarint(s, Speed);
        return s.ToArray();
    }

    public static EntitySpawn Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 52) throw new ProtocolViolationException($"msg_id {id} != 52");
        var m = new EntitySpawn();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.EntityId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Kind = (EntityKind)Wire.ReadVarint(b, ref pos); break;
                case 3: m.TypeId = Wire.ReadString(b, ref pos); break;
                case 4: m.Name = Wire.ReadString(b, ref pos); break;
                case 5: m.Faction = Wire.ReadVarint(b, ref pos); break;
                case 6: m.X = Wire.ReadVarint(b, ref pos); break;
                case 7: m.Y = Wire.ReadVarint(b, ref pos); break;
                case 8: m.HpPct = Wire.ReadF32(b, ref pos); break;
                case 9: m.Speed = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }
}
