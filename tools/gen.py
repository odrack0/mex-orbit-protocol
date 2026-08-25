# -*- coding: utf-8 -*-
"""Generador del contrato: schema/messages.yaml -> C# y GDScript.

Wire format (estilo protobuf, saltable):
  mensaje  = varint(msg_id) + campos
  campo    = varint(tag<<3 | wiretype) + valor
  wiretype = 0 varint · 2 delimitado (len + bytes) · 5 fixed32 LE
Un decodificador siempre puede saltar tags desconocidos por su wiretype.

Uso:  py -3 tools/gen.py
Emite: gen/csharp/Messages.g.cs  y  gen/gdscript/messages_g.gd
"""
import os

import yaml

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ESQUEMA = os.path.join(RAIZ, 'schema', 'messages.yaml')

WT_VARINT, WT_LEN, WT_F32 = 0, 2, 5


def wiretype(campo):
    return {'uint': WT_VARINT, 'bool': WT_VARINT, 'enum': WT_VARINT,
            'string': WT_LEN, 'bytes': WT_LEN, 'float': WT_F32}[campo['type']]


def pascal(s):
    return ''.join(p.capitalize() for p in s.split('_'))


# ============================================================ C#
def gen_csharp(spec):
    L = []
    w = L.append
    w('// GENERADO por tools/gen.py — no editar a mano. Fuente: schema/messages.yaml')
    w('#nullable enable')
    w('using System;')
    w('using System.IO;')
    w('using System.Text;')
    w('')
    w('namespace MexOrbit.Protocol;')
    w('')
    w('/// <summary>Violación del contrato: campo fuera de rango o mensaje malformado.</summary>')
    w('public sealed class ProtocolViolationException : Exception')
    w('{')
    w('    public ProtocolViolationException(string message) : base(message) { }')
    w('}')
    w('')
    # --- primitivas ---
    w('public static class Wire')
    w('{')
    w('    public static void WriteVarint(MemoryStream s, ulong v)')
    w('    {')
    w('        while (v >= 0x80) { s.WriteByte((byte)(v | 0x80)); v >>= 7; }')
    w('        s.WriteByte((byte)v);')
    w('    }')
    w('    public static ulong ReadVarint(ReadOnlySpan<byte> b, ref int pos)')
    w('    {')
    w('        ulong v = 0; int shift = 0;')
    w('        while (true)')
    w('        {')
    w('            if (pos >= b.Length) throw new ProtocolViolationException("varint truncado");')
    w('            byte x = b[pos++];')
    w('            v |= (ulong)(x & 0x7F) << shift;')
    w('            if ((x & 0x80) == 0) return v;')
    w('            shift += 7;')
    w('            if (shift > 63) throw new ProtocolViolationException("varint demasiado largo");')
    w('        }')
    w('    }')
    w('    public static void WriteTag(MemoryStream s, int tag, int wt) => WriteVarint(s, (ulong)(tag << 3 | wt));')
    w('    public static void WriteString(MemoryStream s, string v)')
    w('    {')
    w('        var bytes = Encoding.UTF8.GetBytes(v);')
    w('        WriteVarint(s, (ulong)bytes.Length);')
    w('        s.Write(bytes, 0, bytes.Length);')
    w('    }')
    w('    public static string ReadString(ReadOnlySpan<byte> b, ref int pos)')
    w('    {')
    w('        int len = checked((int)ReadVarint(b, ref pos));')
    w('        if (pos + len > b.Length) throw new ProtocolViolationException("string truncado");')
    w('        var s = Encoding.UTF8.GetString(b.Slice(pos, len));')
    w('        pos += len;')
    w('        return s;')
    w('    }')
    w('    public static void WriteF32(MemoryStream s, float v)')
    w('    {')
    w('        Span<byte> tmp = stackalloc byte[4];')
    w('        BitConverter.TryWriteBytes(tmp, v);')
    w('        if (!BitConverter.IsLittleEndian) tmp.Reverse();')
    w('        s.Write(tmp);')
    w('    }')
    w('    public static float ReadF32(ReadOnlySpan<byte> b, ref int pos)')
    w('    {')
    w('        if (pos + 4 > b.Length) throw new ProtocolViolationException("fixed32 truncado");')
    w('        Span<byte> tmp = stackalloc byte[4];')
    w('        b.Slice(pos, 4).CopyTo(tmp);')
    w('        if (!BitConverter.IsLittleEndian) tmp.Reverse();')
    w('        pos += 4;')
    w('        return BitConverter.ToSingle(tmp);')
    w('    }')
    w('    public static void Skip(ReadOnlySpan<byte> b, ref int pos, int wt)')
    w('    {')
    w('        switch (wt)')
    w('        {')
    w('            case 0: ReadVarint(b, ref pos); break;')
    w('            case 2: { int len = checked((int)ReadVarint(b, ref pos)); pos += len; break; }')
    w('            case 5: pos += 4; break;')
    w('            default: throw new ProtocolViolationException($"wiretype desconocido {wt}");')
    w('        }')
    w('        if (pos > b.Length) throw new ProtocolViolationException("skip fuera de rango");')
    w('    }')
    w('}')
    # --- enums ---
    for nombre, valores in spec.get('enums', {}).items():
        w('')
        w(f'public enum {nombre}')
        w('{')
        for k, v in valores.items():
            w(f'    {pascal(k.lower())} = {v},')
        w('}')
    # --- mensajes ---
    for nombre, msg in spec['messages'].items():
        w('')
        w(f'public sealed class {nombre}')
        w('{')
        w(f'    public const int MsgId = {msg["id"]};')
        for c in msg['fields']:
            tipo = {'uint': 'ulong', 'string': 'string', 'float': 'float',
                    'bool': 'bool', 'enum': c.get('enum', '')}[c['type']]
            ini = ' = "";' if c['type'] == 'string' else ';'
            w(f'    public {tipo} {pascal(c["name"])}{ini}')
        w('')
        w('    public void Validate()')
        w('    {')
        for c in msg['fields']:
            n = pascal(c['name'])
            if c['type'] == 'uint':
                if 'min' in c:
                    w(f'        if ({n} < {c["min"]}) throw new ProtocolViolationException("{nombre}.{c["name"]} < {c["min"]}");')
                if 'max' in c:
                    w(f'        if ({n} > {c["max"]}) throw new ProtocolViolationException("{nombre}.{c["name"]} > {c["max"]}");')
            elif c['type'] == 'string' and 'max_len' in c:
                w(f'        if ({n}.Length > {c["max_len"]}) throw new ProtocolViolationException("{nombre}.{c["name"]} demasiado largo");')
        w('    }')
        w('')
        w('    public byte[] Encode()')
        w('    {')
        w('        Validate();')
        w('        var s = new MemoryStream();')
        w(f'        Wire.WriteVarint(s, {msg["id"]});')
        for c in msg['fields']:
            n, t = pascal(c['name']), c['tag']
            if c['type'] == 'uint':
                w(f'        Wire.WriteTag(s, {t}, 0); Wire.WriteVarint(s, {n});')
            elif c['type'] == 'enum':
                w(f'        Wire.WriteTag(s, {t}, 0); Wire.WriteVarint(s, (ulong){n});')
            elif c['type'] == 'string':
                w(f'        Wire.WriteTag(s, {t}, 2); Wire.WriteString(s, {n});')
            elif c['type'] == 'float':
                w(f'        Wire.WriteTag(s, {t}, 5); Wire.WriteF32(s, {n});')
        w('        return s.ToArray();')
        w('    }')
        w('')
        w(f'    public static {nombre} Decode(ReadOnlySpan<byte> b)')
        w('    {')
        w('        int pos = 0;')
        w('        ulong id = Wire.ReadVarint(b, ref pos);')
        w(f'        if (id != {msg["id"]}) throw new ProtocolViolationException($"msg_id {{id}} != {msg["id"]}");')
        w(f'        var m = new {nombre}();')
        w('        while (pos < b.Length)')
        w('        {')
        w('            ulong key = Wire.ReadVarint(b, ref pos);')
        w('            int tag = (int)(key >> 3), wt = (int)(key & 7);')
        w('            switch (tag)')
        w('            {')
        for c in msg['fields']:
            n, t = pascal(c['name']), c['tag']
            if c['type'] == 'uint':
                w(f'                case {t}: m.{n} = Wire.ReadVarint(b, ref pos); break;')
            elif c['type'] == 'enum':
                w(f'                case {t}: m.{n} = ({c["enum"]})Wire.ReadVarint(b, ref pos); break;')
            elif c['type'] == 'string':
                w(f'                case {t}: m.{n} = Wire.ReadString(b, ref pos); break;')
            elif c['type'] == 'float':
                w(f'                case {t}: m.{n} = Wire.ReadF32(b, ref pos); break;')
        w('                default: Wire.Skip(b, ref pos, wt); break;')
        w('            }')
        w('        }')
        w('        m.Validate();')
        w('        return m;')
        w('    }')
        w('}')
    return '\n'.join(L) + '\n'


# ============================================================ GDScript
def gen_gdscript(spec):
    L = []
    w = L.append
    w('# GENERADO por tools/gen.py — no editar a mano. Fuente: schema/messages.yaml')
    w('class_name MexProtocol')
    w('')
    w('const WT_VARINT := 0')
    w('const WT_LEN := 2')
    w('const WT_F32 := 5')
    w('')
    for nombre, valores in spec.get('enums', {}).items():
        w(f'enum {nombre} {{ ' + ', '.join(f'{k} = {v}' for k, v in valores.items()) + ' }')
    w('')
    w('class Wire:')
    w('\tstatic func write_varint(buf: PackedByteArray, v: int) -> void:')
    w('\t\twhile v >= 0x80:')
    w('\t\t\tbuf.append((v & 0x7F) | 0x80)')
    w('\t\t\tv >>= 7')
    w('\t\tbuf.append(v)')
    w('')
    w('\tstatic func read_varint(b: PackedByteArray, pos: Array) -> int:')
    w('\t\tvar v := 0')
    w('\t\tvar shift := 0')
    w('\t\twhile true:')
    w('\t\t\tassert(pos[0] < b.size(), "varint truncado")')
    w('\t\t\tvar x := b[pos[0]]')
    w('\t\t\tpos[0] += 1')
    w('\t\t\tv |= (x & 0x7F) << shift')
    w('\t\t\tif (x & 0x80) == 0:')
    w('\t\t\t\treturn v')
    w('\t\t\tshift += 7')
    w('\t\t\tassert(shift <= 63, "varint demasiado largo")')
    w('\t\treturn v')
    w('')
    w('\tstatic func write_tag(buf: PackedByteArray, tag: int, wt: int) -> void:')
    w('\t\twrite_varint(buf, (tag << 3) | wt)')
    w('')
    w('\tstatic func write_string(buf: PackedByteArray, v: String) -> void:')
    w('\t\tvar bytes := v.to_utf8_buffer()')
    w('\t\twrite_varint(buf, bytes.size())')
    w('\t\tbuf.append_array(bytes)')
    w('')
    w('\tstatic func read_string(b: PackedByteArray, pos: Array) -> String:')
    w('\t\tvar len := read_varint(b, pos)')
    w('\t\tassert(pos[0] + len <= b.size(), "string truncado")')
    w('\t\tvar s := b.slice(pos[0], pos[0] + len).get_string_from_utf8()')
    w('\t\tpos[0] += len')
    w('\t\treturn s')
    w('')
    w('\tstatic func write_f32(buf: PackedByteArray, v: float) -> void:')
    w('\t\tvar tmp := PackedByteArray()')
    w('\t\ttmp.resize(4)')
    w('\t\ttmp.encode_float(0, v)')
    w('\t\tbuf.append_array(tmp)')
    w('')
    w('\tstatic func read_f32(b: PackedByteArray, pos: Array) -> float:')
    w('\t\tassert(pos[0] + 4 <= b.size(), "fixed32 truncado")')
    w('\t\tvar v := b.decode_float(pos[0])')
    w('\t\tpos[0] += 4')
    w('\t\treturn v')
    w('')
    w('\tstatic func skip(b: PackedByteArray, pos: Array, wt: int) -> void:')
    w('\t\tmatch wt:')
    w('\t\t\tWT_VARINT: read_varint(b, pos)')
    w('\t\t\tWT_LEN:')
    w('\t\t\t\tvar len := read_varint(b, pos)')
    w('\t\t\t\tpos[0] += len')
    w('\t\t\tWT_F32: pos[0] += 4')
    w('\t\t\t_: assert(false, "wiretype desconocido")')
    w('\t\tassert(pos[0] <= b.size(), "skip fuera de rango")')
    for nombre, msg in spec['messages'].items():
        w('')
        w(f'class {nombre}:')
        w(f'\tconst MSG_ID := {msg["id"]}')
        for c in msg['fields']:
            tipo = {'uint': 'int', 'string': 'String', 'float': 'float',
                    'bool': 'bool', 'enum': 'int'}[c['type']]
            defecto = {'int': '0', 'String': '""', 'float': '0.0', 'bool': 'false'}[tipo]
            w(f'\tvar {c["name"]}: {tipo} = {defecto}')
        w('')
        w('\tfunc validate() -> void:')
        tiene = False
        for c in msg['fields']:
            n = c['name']
            if c['type'] == 'uint':
                if 'min' in c:
                    w(f'\t\tassert({n} >= {c["min"]}, "{nombre}.{n} < {c["min"]}")')
                    tiene = True
                if 'max' in c:
                    w(f'\t\tassert({n} <= {c["max"]}, "{nombre}.{n} > {c["max"]}")')
                    tiene = True
            elif c['type'] == 'string' and 'max_len' in c:
                w(f'\t\tassert({n}.length() <= {c["max_len"]}, "{nombre}.{n} demasiado largo")')
                tiene = True
        if not tiene:
            w('\t\tpass')
        w('')
        w('\tfunc encode() -> PackedByteArray:')
        w('\t\tvalidate()')
        w('\t\tvar buf := PackedByteArray()')
        w(f'\t\tWire.write_varint(buf, {msg["id"]})')
        for c in msg['fields']:
            n, t = c['name'], c['tag']
            if c['type'] in ('uint', 'enum'):
                w(f'\t\tWire.write_tag(buf, {t}, 0)')
                w(f'\t\tWire.write_varint(buf, {n})')
            elif c['type'] == 'string':
                w(f'\t\tWire.write_tag(buf, {t}, 2)')
                w(f'\t\tWire.write_string(buf, {n})')
            elif c['type'] == 'float':
                w(f'\t\tWire.write_tag(buf, {t}, 5)')
                w(f'\t\tWire.write_f32(buf, {n})')
        w('\t\treturn buf')
        w('')
        w(f'\tstatic func decode(b: PackedByteArray) -> {nombre}:')
        w('\t\tvar pos := [0]')
        w('\t\tvar id := Wire.read_varint(b, pos)')
        w(f'\t\tassert(id == {msg["id"]}, "msg_id inesperado")')
        w(f'\t\tvar m := {nombre}.new()')
        w('\t\twhile pos[0] < b.size():')
        w('\t\t\tvar key := Wire.read_varint(b, pos)')
        w('\t\t\tvar tag := key >> 3')
        w('\t\t\tvar wt := key & 7')
        w('\t\t\tmatch tag:')
        for c in msg['fields']:
            n, t = c['name'], c['tag']
            if c['type'] in ('uint', 'enum'):
                w(f'\t\t\t\t{t}: m.{n} = Wire.read_varint(b, pos)')
            elif c['type'] == 'string':
                w(f'\t\t\t\t{t}: m.{n} = Wire.read_string(b, pos)')
            elif c['type'] == 'float':
                w(f'\t\t\t\t{t}: m.{n} = Wire.read_f32(b, pos)')
        w('\t\t\t\t_: Wire.skip(b, pos, wt)')
        w('\t\tm.validate()')
        w('\t\treturn m')
    return '\n'.join(L) + '\n'


if __name__ == '__main__':
    with open(ESQUEMA, encoding='utf-8') as f:
        spec = yaml.safe_load(f)
    os.makedirs(os.path.join(RAIZ, 'gen', 'csharp'), exist_ok=True)
    os.makedirs(os.path.join(RAIZ, 'gen', 'gdscript'), exist_ok=True)
    cs = os.path.join(RAIZ, 'gen', 'csharp', 'Messages.g.cs')
    gd = os.path.join(RAIZ, 'gen', 'gdscript', 'messages_g.gd')
    with open(cs, 'w', encoding='utf-8') as f:
        f.write(gen_csharp(spec))
    with open(gd, 'w', encoding='utf-8') as f:
        f.write(gen_gdscript(spec))
    print('generado:', os.path.relpath(cs, RAIZ))
    print('generado:', os.path.relpath(gd, RAIZ))
