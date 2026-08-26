// GENERADO por tools/gen.py — no editar a mano. Fuente: schema/messages.yaml
#nullable enable
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace MexOrbit.Protocol;

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
    public static ulong Zig(long v) => (ulong)((v << 1) ^ (v >> 63));
    public static long Zag(ulong u) => (long)(u >> 1) ^ -(long)(u & 1);
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
    public static void WriteStruct(MemoryStream s, Action<MemoryStream> encodeFields)
    {
        var tmp = new MemoryStream();
        encodeFields(tmp);
        WriteVarint(s, (ulong)tmp.Length);
        tmp.WriteTo(s);
    }
    public static ReadOnlySpan<byte> ReadSlice(ReadOnlySpan<byte> b, ref int pos)
    {
        int len = checked((int)ReadVarint(b, ref pos));
        if (pos + len > b.Length) throw new ProtocolViolationException("submensaje truncado");
        var s = b.Slice(pos, len);
        pos += len;
        return s;
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

public enum DespawnReason
{
    Range = 0,
    Left = 1,
    Dead = 2,
}

public enum BoxDespawnReason
{
    Collected = 0,
    Expired = 1,
    Range = 2,
}

public enum Weapon
{
    Laser = 0,
}

public enum DeathCause
{
    Npc = 0,
    Player = 1,
}

public enum StorageReason
{
    Collect = 0,
    RefineIn = 1,
    RefineOut = 2,
    Sell = 3,
    Unload = 4,
}

public enum ChatChannel
{
    Global = 0,
    Faction = 1,
    Clan = 2,
}

public enum ErrorCode
{
    Generic = 0,
    BadTicket = 1,
    VersionUnsupported = 2,
    Banned = 3,
    ResumeExpired = 4,
    TooFar = 5,
    Gone = 6,
    Insufficient = 7,
    RateLimited = 8,
    Invalid = 9,
}

public sealed class MaterialAmount
{
    public string MaterialId = "";
    public ulong Amount;

    public void Validate()
    {
        if (MaterialId.Length > 64) throw new ProtocolViolationException("MaterialAmount.material_id demasiado largo");
        if (Amount > 1000000) throw new ProtocolViolationException("MaterialAmount.amount > 1000000");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 2); Wire.WriteString(s, MaterialId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, Amount);
    }

    internal static MaterialAmount DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new MaterialAmount();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.MaterialId = Wire.ReadString(b, ref pos); break;
                case 2: m.Amount = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    internal static MaterialAmount DecodeStruct(ReadOnlySpan<byte> b) => DecodeFrom(b, 0);
}

public sealed class RespawnOption
{
    public ulong OptionId;
    public string LabelKey = "";
    public ulong CostCredits;
    public bool Available;

    public void Validate()
    {
        if (OptionId > 16) throw new ProtocolViolationException("RespawnOption.option_id > 16");
        if (LabelKey.Length > 64) throw new ProtocolViolationException("RespawnOption.label_key demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, OptionId);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, LabelKey);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, CostCredits);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, Available ? 1UL : 0UL);
    }

    internal static RespawnOption DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new RespawnOption();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.OptionId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.LabelKey = Wire.ReadString(b, ref pos); break;
                case 3: m.CostCredits = Wire.ReadVarint(b, ref pos); break;
                case 4: m.Available = Wire.ReadVarint(b, ref pos) != 0; break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    internal static RespawnOption DecodeStruct(ReadOnlySpan<byte> b) => DecodeFrom(b, 0);
}

public sealed class MaterialPrice
{
    public string MaterialId = "";
    public ulong PriceCredits;

    public void Validate()
    {
        if (MaterialId.Length > 64) throw new ProtocolViolationException("MaterialPrice.material_id demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 2); Wire.WriteString(s, MaterialId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, PriceCredits);
    }

    internal static MaterialPrice DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new MaterialPrice();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.MaterialId = Wire.ReadString(b, ref pos); break;
                case 2: m.PriceCredits = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    internal static MaterialPrice DecodeStruct(ReadOnlySpan<byte> b) => DecodeFrom(b, 0);
}

public sealed class MapPortal
{
    public ulong PortalId;
    public ulong X;
    public ulong Y;
    public string TargetMapCode = "";
    public bool IsWorking;

    public void Validate()
    {
        if (X > 100000) throw new ProtocolViolationException("MapPortal.x > 100000");
        if (Y > 100000) throw new ProtocolViolationException("MapPortal.y > 100000");
        if (TargetMapCode.Length > 16) throw new ProtocolViolationException("MapPortal.target_map_code demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, PortalId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, X);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, Y);
        Wire.WriteTag(s, 4, 2); Wire.WriteString(s, TargetMapCode);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, IsWorking ? 1UL : 0UL);
    }

    internal static MapPortal DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new MapPortal();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.PortalId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.X = Wire.ReadVarint(b, ref pos); break;
                case 3: m.Y = Wire.ReadVarint(b, ref pos); break;
                case 4: m.TargetMapCode = Wire.ReadString(b, ref pos); break;
                case 5: m.IsWorking = Wire.ReadVarint(b, ref pos) != 0; break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    internal static MapPortal DecodeStruct(ReadOnlySpan<byte> b) => DecodeFrom(b, 0);
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

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, ProtocolVersion);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, GameTicket);
    }

    internal static Hello DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
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

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 1);
        EncodeFields(s);
        return s.ToArray();
    }

    public static Hello Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 1) throw new ProtocolViolationException($"msg_id {id} != 1");
        return DecodeFrom(b, pos);
    }
}

public sealed class Welcome
{
    public const int MsgId = 2;
    public ulong AccountId;
    public string ReconnectToken = "";
    public ulong ServerTimeMs;
    public ulong TickRate;

    public void Validate()
    {
        if (ReconnectToken.Length > 128) throw new ProtocolViolationException("Welcome.reconnect_token demasiado largo");
        if (TickRate < 1) throw new ProtocolViolationException("Welcome.tick_rate < 1");
        if (TickRate > 100) throw new ProtocolViolationException("Welcome.tick_rate > 100");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, AccountId);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, ReconnectToken);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, ServerTimeMs);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, TickRate);
    }

    internal static Welcome DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new Welcome();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.AccountId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.ReconnectToken = Wire.ReadString(b, ref pos); break;
                case 3: m.ServerTimeMs = Wire.ReadVarint(b, ref pos); break;
                case 4: m.TickRate = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 2);
        EncodeFields(s);
        return s.ToArray();
    }

    public static Welcome Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 2) throw new ProtocolViolationException($"msg_id {id} != 2");
        return DecodeFrom(b, pos);
    }
}

public sealed class Resume
{
    public const int MsgId = 3;
    public ulong ProtocolVersion;
    public string ReconnectToken = "";

    public void Validate()
    {
        if (ProtocolVersion < 1) throw new ProtocolViolationException("Resume.protocol_version < 1");
        if (ProtocolVersion > 1000) throw new ProtocolViolationException("Resume.protocol_version > 1000");
        if (ReconnectToken.Length > 128) throw new ProtocolViolationException("Resume.reconnect_token demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, ProtocolVersion);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, ReconnectToken);
    }

    internal static Resume DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new Resume();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.ProtocolVersion = Wire.ReadVarint(b, ref pos); break;
                case 2: m.ReconnectToken = Wire.ReadString(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 3);
        EncodeFields(s);
        return s.ToArray();
    }

    public static Resume Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 3) throw new ProtocolViolationException($"msg_id {id} != 3");
        return DecodeFrom(b, pos);
    }
}

public sealed class ResumeOk
{
    public const int MsgId = 4;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
    }

    internal static ResumeOk DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new ResumeOk();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 4);
        EncodeFields(s);
        return s.ToArray();
    }

    public static ResumeOk Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 4) throw new ProtocolViolationException($"msg_id {id} != 4");
        return DecodeFrom(b, pos);
    }
}

public sealed class Ping
{
    public const int MsgId = 5;
    public ulong Nonce;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, Nonce);
    }

    internal static Ping DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new Ping();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Nonce = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 5);
        EncodeFields(s);
        return s.ToArray();
    }

    public static Ping Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 5) throw new ProtocolViolationException($"msg_id {id} != 5");
        return DecodeFrom(b, pos);
    }
}

public sealed class Pong
{
    public const int MsgId = 6;
    public ulong Nonce;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, Nonce);
    }

    internal static Pong DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new Pong();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Nonce = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 6);
        EncodeFields(s);
        return s.ToArray();
    }

    public static Pong Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 6) throw new ProtocolViolationException($"msg_id {id} != 6");
        return DecodeFrom(b, pos);
    }
}

public sealed class ErrorReply
{
    public const int MsgId = 7;
    public ulong RequestId;
    public ErrorCode Code;
    public string Detail = "";

    public void Validate()
    {
        if (Detail.Length > 256) throw new ProtocolViolationException("ErrorReply.detail demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, (ulong)Code);
        Wire.WriteTag(s, 3, 2); Wire.WriteString(s, Detail);
    }

    internal static ErrorReply DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new ErrorReply();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Code = (ErrorCode)Wire.ReadVarint(b, ref pos); break;
                case 3: m.Detail = Wire.ReadString(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 7);
        EncodeFields(s);
        return s.ToArray();
    }

    public static ErrorReply Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 7) throw new ProtocolViolationException($"msg_id {id} != 7");
        return DecodeFrom(b, pos);
    }
}

public sealed class SessionReplaced
{
    public const int MsgId = 8;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
    }

    internal static SessionReplaced DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new SessionReplaced();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 8);
        EncodeFields(s);
        return s.ToArray();
    }

    public static SessionReplaced Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 8) throw new ProtocolViolationException($"msg_id {id} != 8");
        return DecodeFrom(b, pos);
    }
}

public sealed class LogoutRequest
{
    public const int MsgId = 9;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
    }

    internal static LogoutRequest DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new LogoutRequest();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 9);
        EncodeFields(s);
        return s.ToArray();
    }

    public static LogoutRequest Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 9) throw new ProtocolViolationException($"msg_id {id} != 9");
        return DecodeFrom(b, pos);
    }
}

public sealed class LogoutCountdown
{
    public const int MsgId = 10;
    public ulong SecondsLeft;

    public void Validate()
    {
        if (SecondsLeft > 60) throw new ProtocolViolationException("LogoutCountdown.seconds_left > 60");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, SecondsLeft);
    }

    internal static LogoutCountdown DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new LogoutCountdown();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.SecondsLeft = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 10);
        EncodeFields(s);
        return s.ToArray();
    }

    public static LogoutCountdown Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 10) throw new ProtocolViolationException($"msg_id {id} != 10");
        return DecodeFrom(b, pos);
    }
}

public sealed class LogoutDone
{
    public const int MsgId = 11;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
    }

    internal static LogoutDone DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new LogoutDone();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 11);
        EncodeFields(s);
        return s.ToArray();
    }

    public static LogoutDone Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 11) throw new ProtocolViolationException($"msg_id {id} != 11");
        return DecodeFrom(b, pos);
    }
}

public sealed class EnterMap
{
    public const int MsgId = 50;
    public ulong MapId;
    public string MapCode = "";
    public ulong LimitsX;
    public ulong LimitsY;
    public ulong CargoRiskPct;
    public ulong StationX;
    public ulong StationY;
    public ulong StationRange;
    public List<MapPortal> Portals = new();

    public void Validate()
    {
        if (MapCode.Length > 16) throw new ProtocolViolationException("EnterMap.map_code demasiado largo");
        if (LimitsX < 1000) throw new ProtocolViolationException("EnterMap.limits_x < 1000");
        if (LimitsX > 100000) throw new ProtocolViolationException("EnterMap.limits_x > 100000");
        if (LimitsY < 1000) throw new ProtocolViolationException("EnterMap.limits_y < 1000");
        if (LimitsY > 100000) throw new ProtocolViolationException("EnterMap.limits_y > 100000");
        if (CargoRiskPct > 100) throw new ProtocolViolationException("EnterMap.cargo_risk_pct > 100");
        if (StationX > 100000) throw new ProtocolViolationException("EnterMap.station_x > 100000");
        if (StationY > 100000) throw new ProtocolViolationException("EnterMap.station_y > 100000");
        if (StationRange > 10000) throw new ProtocolViolationException("EnterMap.station_range > 10000");
        foreach (var v in Portals)
        {
            v.Validate();
        }
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, MapId);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, MapCode);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, LimitsX);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, LimitsY);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, CargoRiskPct);
        Wire.WriteTag(s, 6, 0); Wire.WriteVarint(s, StationX);
        Wire.WriteTag(s, 7, 0); Wire.WriteVarint(s, StationY);
        Wire.WriteTag(s, 8, 0); Wire.WriteVarint(s, StationRange);
        foreach (var v in Portals)
        {
            Wire.WriteTag(s, 9, 2); Wire.WriteStruct(s, v.EncodeFields);
        }
    }

    internal static EnterMap DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new EnterMap();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.MapId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.MapCode = Wire.ReadString(b, ref pos); break;
                case 3: m.LimitsX = Wire.ReadVarint(b, ref pos); break;
                case 4: m.LimitsY = Wire.ReadVarint(b, ref pos); break;
                case 5: m.CargoRiskPct = Wire.ReadVarint(b, ref pos); break;
                case 6: m.StationX = Wire.ReadVarint(b, ref pos); break;
                case 7: m.StationY = Wire.ReadVarint(b, ref pos); break;
                case 8: m.StationRange = Wire.ReadVarint(b, ref pos); break;
                case 9: m.Portals.Add(MapPortal.DecodeStruct(Wire.ReadSlice(b, ref pos))); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 50);
        EncodeFields(s);
        return s.ToArray();
    }

    public static EnterMap Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 50) throw new ProtocolViolationException($"msg_id {id} != 50");
        return DecodeFrom(b, pos);
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
    public float ShieldPct;

    public void Validate()
    {
        if (TypeId.Length > 64) throw new ProtocolViolationException("EntitySpawn.type_id demasiado largo");
        if (Name.Length > 64) throw new ProtocolViolationException("EntitySpawn.name demasiado largo");
        if (Faction > 8) throw new ProtocolViolationException("EntitySpawn.faction > 8");
        if (X > 100000) throw new ProtocolViolationException("EntitySpawn.x > 100000");
        if (Y > 100000) throw new ProtocolViolationException("EntitySpawn.y > 100000");
        if (Speed > 2000) throw new ProtocolViolationException("EntitySpawn.speed > 2000");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, (ulong)Kind);
        Wire.WriteTag(s, 3, 2); Wire.WriteString(s, TypeId);
        Wire.WriteTag(s, 4, 2); Wire.WriteString(s, Name);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, Faction);
        Wire.WriteTag(s, 6, 0); Wire.WriteVarint(s, X);
        Wire.WriteTag(s, 7, 0); Wire.WriteVarint(s, Y);
        Wire.WriteTag(s, 8, 5); Wire.WriteF32(s, HpPct);
        Wire.WriteTag(s, 9, 0); Wire.WriteVarint(s, Speed);
        Wire.WriteTag(s, 10, 5); Wire.WriteF32(s, ShieldPct);
    }

    internal static EntitySpawn DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
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
                case 10: m.ShieldPct = Wire.ReadF32(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 52);
        EncodeFields(s);
        return s.ToArray();
    }

    public static EntitySpawn Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 52) throw new ProtocolViolationException($"msg_id {id} != 52");
        return DecodeFrom(b, pos);
    }
}

public sealed class EntityDespawn
{
    public const int MsgId = 53;
    public ulong EntityId;
    public DespawnReason Reason;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, (ulong)Reason);
    }

    internal static EntityDespawn DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new EntityDespawn();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.EntityId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Reason = (DespawnReason)Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 53);
        EncodeFields(s);
        return s.ToArray();
    }

    public static EntityDespawn Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 53) throw new ProtocolViolationException($"msg_id {id} != 53");
        return DecodeFrom(b, pos);
    }
}

public sealed class EntityMove
{
    public const int MsgId = 54;
    public ulong EntityId;
    public ulong X;
    public ulong Y;
    public ulong TargetX;
    public ulong TargetY;
    public ulong Speed;
    public bool Teleport;

    public void Validate()
    {
        if (X > 100000) throw new ProtocolViolationException("EntityMove.x > 100000");
        if (Y > 100000) throw new ProtocolViolationException("EntityMove.y > 100000");
        if (TargetX > 100000) throw new ProtocolViolationException("EntityMove.target_x > 100000");
        if (TargetY > 100000) throw new ProtocolViolationException("EntityMove.target_y > 100000");
        if (Speed > 2000) throw new ProtocolViolationException("EntityMove.speed > 2000");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, X);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, Y);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, TargetX);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, TargetY);
        Wire.WriteTag(s, 6, 0); Wire.WriteVarint(s, Speed);
        Wire.WriteTag(s, 7, 0); Wire.WriteVarint(s, Teleport ? 1UL : 0UL);
    }

    internal static EntityMove DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new EntityMove();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.EntityId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.X = Wire.ReadVarint(b, ref pos); break;
                case 3: m.Y = Wire.ReadVarint(b, ref pos); break;
                case 4: m.TargetX = Wire.ReadVarint(b, ref pos); break;
                case 5: m.TargetY = Wire.ReadVarint(b, ref pos); break;
                case 6: m.Speed = Wire.ReadVarint(b, ref pos); break;
                case 7: m.Teleport = Wire.ReadVarint(b, ref pos) != 0; break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 54);
        EncodeFields(s);
        return s.ToArray();
    }

    public static EntityMove Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 54) throw new ProtocolViolationException($"msg_id {id} != 54");
        return DecodeFrom(b, pos);
    }
}

public sealed class SpeedChange
{
    public const int MsgId = 55;
    public ulong EntityId;
    public ulong Speed;

    public void Validate()
    {
        if (Speed > 2000) throw new ProtocolViolationException("SpeedChange.speed > 2000");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, Speed);
    }

    internal static SpeedChange DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new SpeedChange();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.EntityId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Speed = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 55);
        EncodeFields(s);
        return s.ToArray();
    }

    public static SpeedChange Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 55) throw new ProtocolViolationException($"msg_id {id} != 55");
        return DecodeFrom(b, pos);
    }
}

public sealed class HeroStats
{
    public const int MsgId = 56;
    public ulong Hp;
    public ulong MaxHp;
    public ulong Shield;
    public ulong MaxShield;
    public ulong Cargo;
    public ulong MaxCargo;
    public ulong Credits;
    public ulong Experience;
    public ulong Level;

    public void Validate()
    {
        if (Level > 200) throw new ProtocolViolationException("HeroStats.level > 200");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, Hp);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, MaxHp);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, Shield);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, MaxShield);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, Cargo);
        Wire.WriteTag(s, 6, 0); Wire.WriteVarint(s, MaxCargo);
        Wire.WriteTag(s, 7, 0); Wire.WriteVarint(s, Credits);
        Wire.WriteTag(s, 8, 0); Wire.WriteVarint(s, Experience);
        Wire.WriteTag(s, 9, 0); Wire.WriteVarint(s, Level);
    }

    internal static HeroStats DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new HeroStats();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Hp = Wire.ReadVarint(b, ref pos); break;
                case 2: m.MaxHp = Wire.ReadVarint(b, ref pos); break;
                case 3: m.Shield = Wire.ReadVarint(b, ref pos); break;
                case 4: m.MaxShield = Wire.ReadVarint(b, ref pos); break;
                case 5: m.Cargo = Wire.ReadVarint(b, ref pos); break;
                case 6: m.MaxCargo = Wire.ReadVarint(b, ref pos); break;
                case 7: m.Credits = Wire.ReadVarint(b, ref pos); break;
                case 8: m.Experience = Wire.ReadVarint(b, ref pos); break;
                case 9: m.Level = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 56);
        EncodeFields(s);
        return s.ToArray();
    }

    public static HeroStats Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 56) throw new ProtocolViolationException($"msg_id {id} != 56");
        return DecodeFrom(b, pos);
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
        if (TargetX > 100000) throw new ProtocolViolationException("MoveIntent.target_x > 100000");
        if (TargetY < 0) throw new ProtocolViolationException("MoveIntent.target_y < 0");
        if (TargetY > 100000) throw new ProtocolViolationException("MoveIntent.target_y > 100000");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, Seq);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, TargetX);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, TargetY);
    }

    internal static MoveIntent DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
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

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 60);
        EncodeFields(s);
        return s.ToArray();
    }

    public static MoveIntent Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 60) throw new ProtocolViolationException($"msg_id {id} != 60");
        return DecodeFrom(b, pos);
    }
}

public sealed class SelectTarget
{
    public const int MsgId = 100;
    public ulong EntityId;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
    }

    internal static SelectTarget DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new SelectTarget();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.EntityId = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 100);
        EncodeFields(s);
        return s.ToArray();
    }

    public static SelectTarget Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 100) throw new ProtocolViolationException($"msg_id {id} != 100");
        return DecodeFrom(b, pos);
    }
}

public sealed class TargetInfo
{
    public const int MsgId = 101;
    public ulong EntityId;
    public ulong Hp;
    public ulong MaxHp;
    public ulong Shield;
    public ulong MaxShield;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, Hp);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, MaxHp);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, Shield);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, MaxShield);
    }

    internal static TargetInfo DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new TargetInfo();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.EntityId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Hp = Wire.ReadVarint(b, ref pos); break;
                case 3: m.MaxHp = Wire.ReadVarint(b, ref pos); break;
                case 4: m.Shield = Wire.ReadVarint(b, ref pos); break;
                case 5: m.MaxShield = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 101);
        EncodeFields(s);
        return s.ToArray();
    }

    public static TargetInfo Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 101) throw new ProtocolViolationException($"msg_id {id} != 101");
        return DecodeFrom(b, pos);
    }
}

public sealed class LaserToggle
{
    public const int MsgId = 102;
    public bool Active;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, Active ? 1UL : 0UL);
    }

    internal static LaserToggle DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new LaserToggle();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Active = Wire.ReadVarint(b, ref pos) != 0; break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 102);
        EncodeFields(s);
        return s.ToArray();
    }

    public static LaserToggle Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 102) throw new ProtocolViolationException($"msg_id {id} != 102");
        return DecodeFrom(b, pos);
    }
}

public sealed class AttackEvent
{
    public const int MsgId = 103;
    public ulong AttackerId;
    public ulong TargetId;
    public Weapon Weapon;
    public ulong Damage;
    public ulong TargetHp;
    public ulong TargetShield;
    public bool Missed;
    public string AmmoId = "";
    public bool Skilled;

    public void Validate()
    {
        if (AmmoId.Length > 64) throw new ProtocolViolationException("AttackEvent.ammo_id demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, AttackerId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, TargetId);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, (ulong)Weapon);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, Damage);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, TargetHp);
        Wire.WriteTag(s, 6, 0); Wire.WriteVarint(s, TargetShield);
        Wire.WriteTag(s, 7, 0); Wire.WriteVarint(s, Missed ? 1UL : 0UL);
        Wire.WriteTag(s, 8, 2); Wire.WriteString(s, AmmoId);
        Wire.WriteTag(s, 9, 0); Wire.WriteVarint(s, Skilled ? 1UL : 0UL);
    }

    internal static AttackEvent DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new AttackEvent();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.AttackerId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.TargetId = Wire.ReadVarint(b, ref pos); break;
                case 3: m.Weapon = (Weapon)Wire.ReadVarint(b, ref pos); break;
                case 4: m.Damage = Wire.ReadVarint(b, ref pos); break;
                case 5: m.TargetHp = Wire.ReadVarint(b, ref pos); break;
                case 6: m.TargetShield = Wire.ReadVarint(b, ref pos); break;
                case 7: m.Missed = Wire.ReadVarint(b, ref pos) != 0; break;
                case 8: m.AmmoId = Wire.ReadString(b, ref pos); break;
                case 9: m.Skilled = Wire.ReadVarint(b, ref pos) != 0; break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 103);
        EncodeFields(s);
        return s.ToArray();
    }

    public static AttackEvent Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 103) throw new ProtocolViolationException($"msg_id {id} != 103");
        return DecodeFrom(b, pos);
    }
}

public sealed class EntityDestroyed
{
    public const int MsgId = 104;
    public ulong EntityId;
    public ulong KillerId;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, EntityId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, KillerId);
    }

    internal static EntityDestroyed DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new EntityDestroyed();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.EntityId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.KillerId = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 104);
        EncodeFields(s);
        return s.ToArray();
    }

    public static EntityDestroyed Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 104) throw new ProtocolViolationException($"msg_id {id} != 104");
        return DecodeFrom(b, pos);
    }
}

public sealed class RespawnOptions
{
    public const int MsgId = 105;
    public List<RespawnOption> Options = new();
    public DeathCause Cause;
    public string KillerName = "";

    public void Validate()
    {
        foreach (var v in Options)
        {
            v.Validate();
        }
        if (KillerName.Length > 64) throw new ProtocolViolationException("RespawnOptions.killer_name demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        foreach (var v in Options)
        {
            Wire.WriteTag(s, 1, 2); Wire.WriteStruct(s, v.EncodeFields);
        }
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, (ulong)Cause);
        Wire.WriteTag(s, 3, 2); Wire.WriteString(s, KillerName);
    }

    internal static RespawnOptions DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new RespawnOptions();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Options.Add(RespawnOption.DecodeStruct(Wire.ReadSlice(b, ref pos))); break;
                case 2: m.Cause = (DeathCause)Wire.ReadVarint(b, ref pos); break;
                case 3: m.KillerName = Wire.ReadString(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 105);
        EncodeFields(s);
        return s.ToArray();
    }

    public static RespawnOptions Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 105) throw new ProtocolViolationException($"msg_id {id} != 105");
        return DecodeFrom(b, pos);
    }
}

public sealed class RespawnSelect
{
    public const int MsgId = 106;
    public ulong OptionId;

    public void Validate()
    {
        if (OptionId > 16) throw new ProtocolViolationException("RespawnSelect.option_id > 16");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, OptionId);
    }

    internal static RespawnSelect DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new RespawnSelect();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.OptionId = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 106);
        EncodeFields(s);
        return s.ToArray();
    }

    public static RespawnSelect Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 106) throw new ProtocolViolationException($"msg_id {id} != 106");
        return DecodeFrom(b, pos);
    }
}

public sealed class BoxSpawn
{
    public const int MsgId = 150;
    public ulong BoxId;
    public string BoxType = "";
    public ulong X;
    public ulong Y;

    public void Validate()
    {
        if (BoxType.Length > 32) throw new ProtocolViolationException("BoxSpawn.box_type demasiado largo");
        if (X > 100000) throw new ProtocolViolationException("BoxSpawn.x > 100000");
        if (Y > 100000) throw new ProtocolViolationException("BoxSpawn.y > 100000");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, BoxId);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, BoxType);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, X);
        Wire.WriteTag(s, 4, 0); Wire.WriteVarint(s, Y);
    }

    internal static BoxSpawn DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new BoxSpawn();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.BoxId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.BoxType = Wire.ReadString(b, ref pos); break;
                case 3: m.X = Wire.ReadVarint(b, ref pos); break;
                case 4: m.Y = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 150);
        EncodeFields(s);
        return s.ToArray();
    }

    public static BoxSpawn Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 150) throw new ProtocolViolationException($"msg_id {id} != 150");
        return DecodeFrom(b, pos);
    }
}

public sealed class BoxDespawn
{
    public const int MsgId = 151;
    public ulong BoxId;
    public BoxDespawnReason Reason;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, BoxId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, (ulong)Reason);
    }

    internal static BoxDespawn DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new BoxDespawn();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.BoxId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Reason = (BoxDespawnReason)Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 151);
        EncodeFields(s);
        return s.ToArray();
    }

    public static BoxDespawn Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 151) throw new ProtocolViolationException($"msg_id {id} != 151");
        return DecodeFrom(b, pos);
    }
}

public sealed class CollectBox
{
    public const int MsgId = 152;
    public ulong RequestId;
    public ulong BoxId;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, BoxId);
    }

    internal static CollectBox DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new CollectBox();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.BoxId = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 152);
        EncodeFields(s);
        return s.ToArray();
    }

    public static CollectBox Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 152) throw new ProtocolViolationException($"msg_id {id} != 152");
        return DecodeFrom(b, pos);
    }
}

public sealed class CollectResult
{
    public const int MsgId = 153;
    public ulong RequestId;
    public List<MaterialAmount> Drops = new();

    public void Validate()
    {
        foreach (var v in Drops)
        {
            v.Validate();
        }
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        foreach (var v in Drops)
        {
            Wire.WriteTag(s, 2, 2); Wire.WriteStruct(s, v.EncodeFields);
        }
    }

    internal static CollectResult DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new CollectResult();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Drops.Add(MaterialAmount.DecodeStruct(Wire.ReadSlice(b, ref pos))); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 153);
        EncodeFields(s);
        return s.ToArray();
    }

    public static CollectResult Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 153) throw new ProtocolViolationException($"msg_id {id} != 153");
        return DecodeFrom(b, pos);
    }
}

public sealed class StorageState
{
    public const int MsgId = 154;
    public List<MaterialAmount> Materials = new();

    public void Validate()
    {
        foreach (var v in Materials)
        {
            v.Validate();
        }
    }

    internal void EncodeFields(MemoryStream s)
    {
        foreach (var v in Materials)
        {
            Wire.WriteTag(s, 1, 2); Wire.WriteStruct(s, v.EncodeFields);
        }
    }

    internal static StorageState DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new StorageState();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Materials.Add(MaterialAmount.DecodeStruct(Wire.ReadSlice(b, ref pos))); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 154);
        EncodeFields(s);
        return s.ToArray();
    }

    public static StorageState Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 154) throw new ProtocolViolationException($"msg_id {id} != 154");
        return DecodeFrom(b, pos);
    }
}

public sealed class StorageDelta
{
    public const int MsgId = 155;
    public string MaterialId = "";
    public long Delta;
    public StorageReason Reason;

    public void Validate()
    {
        if (MaterialId.Length > 64) throw new ProtocolViolationException("StorageDelta.material_id demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 2); Wire.WriteString(s, MaterialId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, Wire.Zig(Delta));
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, (ulong)Reason);
    }

    internal static StorageDelta DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new StorageDelta();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.MaterialId = Wire.ReadString(b, ref pos); break;
                case 2: m.Delta = Wire.Zag(Wire.ReadVarint(b, ref pos)); break;
                case 3: m.Reason = (StorageReason)Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 155);
        EncodeFields(s);
        return s.ToArray();
    }

    public static StorageDelta Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 155) throw new ProtocolViolationException($"msg_id {id} != 155");
        return DecodeFrom(b, pos);
    }
}

public sealed class SellToNpc
{
    public const int MsgId = 156;
    public ulong RequestId;
    public string MaterialId = "";
    public ulong Amount;

    public void Validate()
    {
        if (MaterialId.Length > 64) throw new ProtocolViolationException("SellToNpc.material_id demasiado largo");
        if (Amount > 1000000) throw new ProtocolViolationException("SellToNpc.amount > 1000000");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, MaterialId);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, Amount);
    }

    internal static SellToNpc DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new SellToNpc();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.MaterialId = Wire.ReadString(b, ref pos); break;
                case 3: m.Amount = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 156);
        EncodeFields(s);
        return s.ToArray();
    }

    public static SellToNpc Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 156) throw new ProtocolViolationException($"msg_id {id} != 156");
        return DecodeFrom(b, pos);
    }
}

public sealed class SellResult
{
    public const int MsgId = 157;
    public ulong RequestId;
    public ulong CreditsGained;
    public ulong NewCredits;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, CreditsGained);
        Wire.WriteTag(s, 3, 0); Wire.WriteVarint(s, NewCredits);
    }

    internal static SellResult DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new SellResult();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.CreditsGained = Wire.ReadVarint(b, ref pos); break;
                case 3: m.NewCredits = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 157);
        EncodeFields(s);
        return s.ToArray();
    }

    public static SellResult Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 157) throw new ProtocolViolationException($"msg_id {id} != 157");
        return DecodeFrom(b, pos);
    }
}

public sealed class UnloadCargo
{
    public const int MsgId = 158;
    public ulong RequestId;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
    }

    internal static UnloadCargo DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new UnloadCargo();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 158);
        EncodeFields(s);
        return s.ToArray();
    }

    public static UnloadCargo Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 158) throw new ProtocolViolationException($"msg_id {id} != 158");
        return DecodeFrom(b, pos);
    }
}

public sealed class UnloadResult
{
    public const int MsgId = 159;
    public ulong RequestId;
    public List<MaterialAmount> Stored = new();
    public List<MaterialAmount> Refined = new();

    public void Validate()
    {
        foreach (var v in Stored)
        {
            v.Validate();
        }
        foreach (var v in Refined)
        {
            v.Validate();
        }
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        foreach (var v in Stored)
        {
            Wire.WriteTag(s, 2, 2); Wire.WriteStruct(s, v.EncodeFields);
        }
        foreach (var v in Refined)
        {
            Wire.WriteTag(s, 3, 2); Wire.WriteStruct(s, v.EncodeFields);
        }
    }

    internal static UnloadResult DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new UnloadResult();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Stored.Add(MaterialAmount.DecodeStruct(Wire.ReadSlice(b, ref pos))); break;
                case 3: m.Refined.Add(MaterialAmount.DecodeStruct(Wire.ReadSlice(b, ref pos))); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 159);
        EncodeFields(s);
        return s.ToArray();
    }

    public static UnloadResult Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 159) throw new ProtocolViolationException($"msg_id {id} != 159");
        return DecodeFrom(b, pos);
    }
}

public sealed class NpcPrices
{
    public const int MsgId = 160;
    public List<MaterialPrice> Prices = new();

    public void Validate()
    {
        foreach (var v in Prices)
        {
            v.Validate();
        }
    }

    internal void EncodeFields(MemoryStream s)
    {
        foreach (var v in Prices)
        {
            Wire.WriteTag(s, 1, 2); Wire.WriteStruct(s, v.EncodeFields);
        }
    }

    internal static NpcPrices DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new NpcPrices();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Prices.Add(MaterialPrice.DecodeStruct(Wire.ReadSlice(b, ref pos))); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 160);
        EncodeFields(s);
        return s.ToArray();
    }

    public static NpcPrices Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 160) throw new ProtocolViolationException($"msg_id {id} != 160");
        return DecodeFrom(b, pos);
    }
}

public sealed class StationRange
{
    public const int MsgId = 161;
    public bool InRange;
    public ulong StationId;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, InRange ? 1UL : 0UL);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, StationId);
    }

    internal static StationRange DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new StationRange();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.InRange = Wire.ReadVarint(b, ref pos) != 0; break;
                case 2: m.StationId = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 161);
        EncodeFields(s);
        return s.ToArray();
    }

    public static StationRange Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 161) throw new ProtocolViolationException($"msg_id {id} != 161");
        return DecodeFrom(b, pos);
    }
}

public sealed class JumpRequest
{
    public const int MsgId = 162;
    public ulong RequestId;
    public ulong PortalId;

    public void Validate()
    {
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, PortalId);
    }

    internal static JumpRequest DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new JumpRequest();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.PortalId = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 162);
        EncodeFields(s);
        return s.ToArray();
    }

    public static JumpRequest Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 162) throw new ProtocolViolationException($"msg_id {id} != 162");
        return DecodeFrom(b, pos);
    }
}

public sealed class ChatSend
{
    public const int MsgId = 200;
    public ulong RequestId;
    public ChatChannel Channel;
    public string Text = "";

    public void Validate()
    {
        if (Text.Length > 256) throw new ProtocolViolationException("ChatSend.text demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, RequestId);
        Wire.WriteTag(s, 2, 0); Wire.WriteVarint(s, (ulong)Channel);
        Wire.WriteTag(s, 3, 2); Wire.WriteString(s, Text);
    }

    internal static ChatSend DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new ChatSend();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.RequestId = Wire.ReadVarint(b, ref pos); break;
                case 2: m.Channel = (ChatChannel)Wire.ReadVarint(b, ref pos); break;
                case 3: m.Text = Wire.ReadString(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 200);
        EncodeFields(s);
        return s.ToArray();
    }

    public static ChatSend Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 200) throw new ProtocolViolationException($"msg_id {id} != 200");
        return DecodeFrom(b, pos);
    }
}

public sealed class ChatMessage
{
    public const int MsgId = 201;
    public ChatChannel Channel;
    public string FromName = "";
    public string FromClan = "";
    public string Text = "";
    public ulong ServerTimeMs;

    public void Validate()
    {
        if (FromName.Length > 64) throw new ProtocolViolationException("ChatMessage.from_name demasiado largo");
        if (FromClan.Length > 16) throw new ProtocolViolationException("ChatMessage.from_clan demasiado largo");
        if (Text.Length > 256) throw new ProtocolViolationException("ChatMessage.text demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 0); Wire.WriteVarint(s, (ulong)Channel);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, FromName);
        Wire.WriteTag(s, 3, 2); Wire.WriteString(s, FromClan);
        Wire.WriteTag(s, 4, 2); Wire.WriteString(s, Text);
        Wire.WriteTag(s, 5, 0); Wire.WriteVarint(s, ServerTimeMs);
    }

    internal static ChatMessage DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new ChatMessage();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.Channel = (ChatChannel)Wire.ReadVarint(b, ref pos); break;
                case 2: m.FromName = Wire.ReadString(b, ref pos); break;
                case 3: m.FromClan = Wire.ReadString(b, ref pos); break;
                case 4: m.Text = Wire.ReadString(b, ref pos); break;
                case 5: m.ServerTimeMs = Wire.ReadVarint(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 201);
        EncodeFields(s);
        return s.ToArray();
    }

    public static ChatMessage Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 201) throw new ProtocolViolationException($"msg_id {id} != 201");
        return DecodeFrom(b, pos);
    }
}

public sealed class ChatSystem
{
    public const int MsgId = 202;
    public string TextKey = "";
    public List<string> Params = new();

    public void Validate()
    {
        if (TextKey.Length > 64) throw new ProtocolViolationException("ChatSystem.text_key demasiado largo");
        foreach (var v in Params)
        {
            if (v.Length > 128) throw new ProtocolViolationException("ChatSystem.params demasiado largo");
        }
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 2); Wire.WriteString(s, TextKey);
        foreach (var v in Params)
        {
            Wire.WriteTag(s, 2, 2); Wire.WriteString(s, v);
        }
    }

    internal static ChatSystem DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new ChatSystem();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.TextKey = Wire.ReadString(b, ref pos); break;
                case 2: m.Params.Add(Wire.ReadString(b, ref pos)); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 202);
        EncodeFields(s);
        return s.ToArray();
    }

    public static ChatSystem Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 202) throw new ProtocolViolationException($"msg_id {id} != 202");
        return DecodeFrom(b, pos);
    }
}

public sealed class ChatWhisper
{
    public const int MsgId = 203;
    public string PeerName = "";
    public string Text = "";

    public void Validate()
    {
        if (PeerName.Length > 64) throw new ProtocolViolationException("ChatWhisper.peer_name demasiado largo");
        if (Text.Length > 256) throw new ProtocolViolationException("ChatWhisper.text demasiado largo");
    }

    internal void EncodeFields(MemoryStream s)
    {
        Wire.WriteTag(s, 1, 2); Wire.WriteString(s, PeerName);
        Wire.WriteTag(s, 2, 2); Wire.WriteString(s, Text);
    }

    internal static ChatWhisper DecodeFrom(ReadOnlySpan<byte> b, int pos)
    {
        var m = new ChatWhisper();
        while (pos < b.Length)
        {
            ulong key = Wire.ReadVarint(b, ref pos);
            int tag = (int)(key >> 3), wt = (int)(key & 7);
            switch (tag)
            {
                case 1: m.PeerName = Wire.ReadString(b, ref pos); break;
                case 2: m.Text = Wire.ReadString(b, ref pos); break;
                default: Wire.Skip(b, ref pos, wt); break;
            }
        }
        m.Validate();
        return m;
    }

    public byte[] Encode()
    {
        Validate();
        var s = new MemoryStream();
        Wire.WriteVarint(s, 203);
        EncodeFields(s);
        return s.ToArray();
    }

    public static ChatWhisper Decode(ReadOnlySpan<byte> b)
    {
        int pos = 0;
        ulong id = Wire.ReadVarint(b, ref pos);
        if (id != 203) throw new ProtocolViolationException($"msg_id {id} != 203");
        return DecodeFrom(b, pos);
    }
}
