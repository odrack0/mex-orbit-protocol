# -*- coding: utf-8 -*-
"""Generador del contrato: schema/messages.yaml -> C# y GDScript.

Wire format (estilo protobuf, saltable):
  mensaje    = varint(msg_id) + campos
  campo      = varint(tag<<3 | wiretype) + valor
  wiretype   = 0 varint · 2 delimitado (len + bytes) · 5 fixed32 LE
  submensaje = wiretype 2: len + sus campos (sin msg_id propio)
  repeated   = el mismo tag aparece N veces
  sint       = varint con zigzag (negativos baratos)
Un decodificador siempre puede saltar tags desconocidos por su wiretype.

Uso:  py -3 tools/gen.py
Emite: gen/csharp/Messages.g.cs  y  gen/gdscript/messages_g.gd
"""
import os

import yaml

RAIZ = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
ESQUEMA = os.path.join(RAIZ, 'schema', 'messages.yaml')


def wt_de(campo):
    return {'uint': 0, 'sint': 0, 'bool': 0, 'enum': 0,
            'string': 2, 'struct': 2, 'float': 5}[campo['type']]


def pascal(s):
    return ''.join(p.capitalize() for p in s.split('_'))


# ============================================================ C#
def cs_tipo(c):
    base = {'uint': 'ulong', 'sint': 'long', 'bool': 'bool', 'float': 'float',
            'string': 'string', 'enum': c.get('enum', ''), 'struct': c.get('struct', '')}[c['type']]
    return f'List<{base}>' if c.get('repeated') else base


def cs_default(c):
    if c.get('repeated'):
        return ' = new();'
    if c['type'] == 'string':
        return ' = "";'
    if c['type'] == 'struct':
        return ' = new();'
    return ';'


def cs_write_uno(w, c, expr, ind):
    t = c['tag']
    tipo = c['type']
    if tipo == 'uint':
        w(f'{ind}Wire.WriteTag(s, {t}, 0); Wire.WriteVarint(s, {expr});')
    elif tipo == 'enum':
        w(f'{ind}Wire.WriteTag(s, {t}, 0); Wire.WriteVarint(s, (ulong){expr});')
    elif tipo == 'sint':
        w(f'{ind}Wire.WriteTag(s, {t}, 0); Wire.WriteVarint(s, Wire.Zig({expr}));')
    elif tipo == 'bool':
        w(f'{ind}Wire.WriteTag(s, {t}, 0); Wire.WriteVarint(s, {expr} ? 1UL : 0UL);')
    elif tipo == 'string':
        w(f'{ind}Wire.WriteTag(s, {t}, 2); Wire.WriteString(s, {expr});')
    elif tipo == 'float':
        w(f'{ind}Wire.WriteTag(s, {t}, 5); Wire.WriteF32(s, {expr});')
    elif tipo == 'struct':
        w(f'{ind}Wire.WriteTag(s, {t}, 2); Wire.WriteStruct(s, {expr}.EncodeFields);')


def cs_read_expr(c):
    tipo = c['type']
    if tipo == 'uint':
        return 'Wire.ReadVarint(b, ref pos)'
    if tipo == 'enum':
        return f'({c["enum"]})Wire.ReadVarint(b, ref pos)'
    if tipo == 'sint':
        return 'Wire.Zag(Wire.ReadVarint(b, ref pos))'
    if tipo == 'bool':
        return 'Wire.ReadVarint(b, ref pos) != 0'
    if tipo == 'string':
        return 'Wire.ReadString(b, ref pos)'
    if tipo == 'float':
        return 'Wire.ReadF32(b, ref pos)'
    if tipo == 'struct':
        return f'{c["struct"]}.DecodeStruct(Wire.ReadSlice(b, ref pos))'
    raise ValueError(tipo)


def cs_clase(w, nombre, definicion, msg_id):
    w('')
    w(f'public sealed class {nombre}')
    w('{')
    if msg_id is not None:
        w(f'    public const int MsgId = {msg_id};')
    for c in definicion['fields']:
        w(f'    public {cs_tipo(c)} {pascal(c["name"])}{cs_default(c)}')
    # ---- Validate ----
    w('')
    w('    public void Validate()')
    w('    {')
    for c in definicion['fields']:
        n = pascal(c['name'])
        objetivo = 'v' if c.get('repeated') else n
        lineas = []
        if c['type'] in ('uint', 'sint'):
            if 'min' in c:
                lineas.append(f'if ({objetivo} < {c["min"]}) throw new ProtocolViolationException("{nombre}.{c["name"]} < {c["min"]}");')
            if 'max' in c:
                lineas.append(f'if ({objetivo} > {c["max"]}) throw new ProtocolViolationException("{nombre}.{c["name"]} > {c["max"]}");')
        elif c['type'] == 'string' and 'max_len' in c:
            lineas.append(f'if ({objetivo}.Length > {c["max_len"]}) throw new ProtocolViolationException("{nombre}.{c["name"]} demasiado largo");')
        elif c['type'] == 'struct':
            lineas.append(f'{objetivo}.Validate();')
        if not lineas:
            continue
        if c.get('repeated'):
            w(f'        foreach (var v in {n})')
            w('        {')
            for ln in lineas:
                w('            ' + ln)
            w('        }')
        else:
            for ln in lineas:
                w('        ' + ln)
    w('    }')
    # ---- EncodeFields ----
    w('')
    w('    internal void EncodeFields(MemoryStream s)')
    w('    {')
    for c in definicion['fields']:
        n = pascal(c['name'])
        if c.get('repeated'):
            w(f'        foreach (var v in {n})')
            w('        {')
            cs_write_uno(w, c, 'v', '            ')
            w('        }')
        else:
            cs_write_uno(w, c, n, '        ')
    w('    }')
    # ---- DecodeFrom (nucleo compartido) ----
    w('')
    w(f'    internal static {nombre} DecodeFrom(ReadOnlySpan<byte> b, int pos)')
    w('    {')
    w(f'        var m = new {nombre}();')
    w('        while (pos < b.Length)')
    w('        {')
    w('            ulong key = Wire.ReadVarint(b, ref pos);')
    w('            int tag = (int)(key >> 3), wt = (int)(key & 7);')
    w('            switch (tag)')
    w('            {')
    for c in definicion['fields']:
        n, t = pascal(c['name']), c['tag']
        if c.get('repeated'):
            w(f'                case {t}: m.{n}.Add({cs_read_expr(c)}); break;')
        else:
            w(f'                case {t}: m.{n} = {cs_read_expr(c)}; break;')
    w('                default: Wire.Skip(b, ref pos, wt); break;')
    w('            }')
    w('        }')
    w('        m.Validate();')
    w('        return m;')
    w('    }')
    if msg_id is None:
        # struct: entrada por slice delimitado
        w('')
        w(f'    internal static {nombre} DecodeStruct(ReadOnlySpan<byte> b) => DecodeFrom(b, 0);')
    else:
        w('')
        w('    public byte[] Encode()')
        w('    {')
        w('        Validate();')
        w('        var s = new MemoryStream();')
        w(f'        Wire.WriteVarint(s, {msg_id});')
        w('        EncodeFields(s);')
        w('        return s.ToArray();')
        w('    }')
        w('')
        w(f'    public static {nombre} Decode(ReadOnlySpan<byte> b)')
        w('    {')
        w('        int pos = 0;')
        w('        ulong id = Wire.ReadVarint(b, ref pos);')
        w(f'        if (id != {msg_id}) throw new ProtocolViolationException($"msg_id {{id}} != {msg_id}");')
        w('        return DecodeFrom(b, pos);')
        w('    }')
    w('}')


def gen_csharp(spec):
    L = []
    w = L.append
    w('// GENERADO por tools/gen.py — no editar a mano. Fuente: schema/messages.yaml')
    w('#nullable enable')
    w('using System;')
    w('using System.Collections.Generic;')
    w('using System.IO;')
    w('using System.Text;')
    w('')
    w('namespace MexOrbit.Protocol;')
    w('')
    w('public sealed class ProtocolViolationException : Exception')
    w('{')
    w('    public ProtocolViolationException(string message) : base(message) { }')
    w('}')
    w('')
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
    w('    public static ulong Zig(long v) => (ulong)((v << 1) ^ (v >> 63));')
    w('    public static long Zag(ulong u) => (long)(u >> 1) ^ -(long)(u & 1);')
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
    w('    public static void WriteStruct(MemoryStream s, Action<MemoryStream> encodeFields)')
    w('    {')
    w('        var tmp = new MemoryStream();')
    w('        encodeFields(tmp);')
    w('        WriteVarint(s, (ulong)tmp.Length);')
    w('        tmp.WriteTo(s);')
    w('    }')
    w('    public static ReadOnlySpan<byte> ReadSlice(ReadOnlySpan<byte> b, ref int pos)')
    w('    {')
    w('        int len = checked((int)ReadVarint(b, ref pos));')
    w('        if (pos + len > b.Length) throw new ProtocolViolationException("submensaje truncado");')
    w('        var s = b.Slice(pos, len);')
    w('        pos += len;')
    w('        return s;')
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
    for nombre, valores in spec.get('enums', {}).items():
        w('')
        w(f'public enum {nombre}')
        w('{')
        for k, v in valores.items():
            w(f'    {pascal(k.lower())} = {v},')
        w('}')
    for nombre, definicion in spec.get('structs', {}).items():
        cs_clase(w, nombre, definicion, None)
    for nombre, definicion in spec['messages'].items():
        cs_clase(w, nombre, definicion, definicion['id'])
    return '\n'.join(L) + '\n'


# ============================================================ GDScript
def gd_default(c):
    if c.get('repeated'):
        return 'Array = []'
    return {'uint': 'int = 0', 'sint': 'int = 0', 'enum': 'int = 0', 'bool': 'bool = false',
            'float': 'float = 0.0', 'string': 'String = ""',
            'struct': 'RefCounted = null'}[c['type']] if c['type'] != 'struct' else None


def gd_write_uno(w, c, expr, ind):
    t = c['tag']
    tipo = c['type']
    if tipo in ('uint', 'enum'):
        w(f'{ind}Wire.write_tag(buf, {t}, 0)')
        w(f'{ind}Wire.write_varint(buf, {expr})')
    elif tipo == 'sint':
        w(f'{ind}Wire.write_tag(buf, {t}, 0)')
        w(f'{ind}Wire.write_varint(buf, Wire.zig({expr}))')
    elif tipo == 'bool':
        w(f'{ind}Wire.write_tag(buf, {t}, 0)')
        w(f'{ind}Wire.write_varint(buf, 1 if {expr} else 0)')
    elif tipo == 'string':
        w(f'{ind}Wire.write_tag(buf, {t}, 2)')
        w(f'{ind}Wire.write_string(buf, {expr})')
    elif tipo == 'float':
        w(f'{ind}Wire.write_tag(buf, {t}, 5)')
        w(f'{ind}Wire.write_f32(buf, {expr})')
    elif tipo == 'struct':
        w(f'{ind}Wire.write_tag(buf, {t}, 2)')
        w(f'{ind}var sub_{t} := PackedByteArray()')
        w(f'{ind}{expr}.encode_fields(sub_{t})')
        w(f'{ind}Wire.write_varint(buf, sub_{t}.size())')
        w(f'{ind}buf.append_array(sub_{t})')


def gd_read_expr(c):
    tipo = c['type']
    if tipo in ('uint', 'enum'):
        return 'Wire.read_varint(b, pos)'
    if tipo == 'sint':
        return 'Wire.zag(Wire.read_varint(b, pos))'
    if tipo == 'bool':
        return 'Wire.read_varint(b, pos) != 0'
    if tipo == 'string':
        return 'Wire.read_string(b, pos)'
    if tipo == 'float':
        return 'Wire.read_f32(b, pos)'
    if tipo == 'struct':
        return f'{c["struct"]}.decode_struct(Wire.read_slice(b, pos))'
    raise ValueError(tipo)


def gd_clase(w, nombre, definicion, msg_id):
    w('')
    w(f'class {nombre}:')
    if msg_id is not None:
        w(f'\tconst MSG_ID := {msg_id}')
    for c in definicion['fields']:
        if c.get('repeated'):
            w(f'\tvar {c["name"]}: Array = []')
        elif c['type'] == 'struct':
            w(f'\tvar {c["name"]} = null')
        else:
            w(f'\tvar {c["name"]}: {gd_default(c)}')
    if not definicion['fields'] and msg_id is None:
        w('\tpass')
    # ---- validate ----
    w('')
    w('\tfunc validate() -> void:')
    tiene = False
    for c in definicion['fields']:
        n = c['name']
        objetivo = 'v' if c.get('repeated') else n
        lineas = []
        if c['type'] in ('uint', 'sint'):
            if 'min' in c:
                lineas.append(f'assert({objetivo} >= {c["min"]}, "{nombre}.{n} < {c["min"]}")')
            if 'max' in c:
                lineas.append(f'assert({objetivo} <= {c["max"]}, "{nombre}.{n} > {c["max"]}")')
        elif c['type'] == 'string' and 'max_len' in c:
            lineas.append(f'assert({objetivo}.length() <= {c["max_len"]}, "{nombre}.{n} demasiado largo")')
        elif c['type'] == 'struct':
            lineas.append(f'{objetivo}.validate()')
        if not lineas:
            continue
        tiene = True
        if c.get('repeated'):
            w(f'\t\tfor v in {n}:')
            for ln in lineas:
                w('\t\t\t' + ln)
        else:
            for ln in lineas:
                w('\t\t' + ln)
    if not tiene:
        w('\t\tpass')
    # ---- encode_fields ----
    w('')
    w('\tfunc encode_fields(buf: PackedByteArray) -> void:')
    if not definicion['fields']:
        w('\t\tpass')
    for c in definicion['fields']:
        n = c['name']
        if c.get('repeated'):
            w(f'\t\tfor v in {n}:')
            gd_write_uno(w, c, 'v', '\t\t\t')
        else:
            gd_write_uno(w, c, n, '\t\t')
    # ---- decode_from ----
    w('')
    w(f'\tstatic func decode_from(b: PackedByteArray, pos: Array) -> {nombre}:')
    w(f'\t\tvar m := {nombre}.new()')
    w('\t\twhile pos[0] < b.size():')
    w('\t\t\tvar key := Wire.read_varint(b, pos)')
    w('\t\t\tvar tag := key >> 3')
    w('\t\t\tvar wt := key & 7')
    w('\t\t\tmatch tag:')
    if definicion['fields']:
        for c in definicion['fields']:
            n, t = c['name'], c['tag']
            if c.get('repeated'):
                w(f'\t\t\t\t{t}: m.{n}.append({gd_read_expr(c)})')
            else:
                w(f'\t\t\t\t{t}: m.{n} = {gd_read_expr(c)}')
    w('\t\t\t\t_: Wire.skip(b, pos, wt)')
    w('\t\tm.validate()')
    w('\t\treturn m')
    if msg_id is None:
        w('')
        w(f'\tstatic func decode_struct(b: PackedByteArray) -> {nombre}:')
        w('\t\treturn decode_from(b, [0])')
    else:
        w('')
        w('\tfunc encode() -> PackedByteArray:')
        w('\t\tvalidate()')
        w('\t\tvar buf := PackedByteArray()')
        w(f'\t\tWire.write_varint(buf, {msg_id})')
        w('\t\tencode_fields(buf)')
        w('\t\treturn buf')
        w('')
        w(f'\tstatic func decode(b: PackedByteArray) -> {nombre}:')
        w('\t\tvar pos := [0]')
        w('\t\tvar id := Wire.read_varint(b, pos)')
        w(f'\t\tassert(id == {msg_id}, "msg_id inesperado")')
        w('\t\treturn decode_from(b, pos)')


def gen_gdscript(spec):
    L = []
    w = L.append
    w('# GENERADO por tools/gen.py — no editar a mano. Fuente: schema/messages.yaml')
    w('class_name MexProtocol')
    w('')
    for nombre, valores in spec.get('enums', {}).items():
        w(f'enum {nombre} {{ ' + ', '.join(f'{k} = {v}' for k, v in valores.items()) + ' }')
    w('')
    w('class Wire:')
    w('\tstatic func write_varint(buf: PackedByteArray, v: int) -> void:')
    w('\t\twhile v >= 0x80 or v < 0:')
    w('\t\t\tbuf.append((v & 0x7F) | 0x80)')
    w('\t\t\tv = v >> 7 if v >= 0 else (v >> 7) & 0x1FFFFFFFFFFFFFF')
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
    w('\tstatic func zig(v: int) -> int:')
    w('\t\treturn (v << 1) ^ (v >> 63)')
    w('')
    w('\tstatic func zag(u: int) -> int:')
    w('\t\treturn (u >> 1) ^ -(u & 1)')
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
    w('\tstatic func read_slice(b: PackedByteArray, pos: Array) -> PackedByteArray:')
    w('\t\tvar len := read_varint(b, pos)')
    w('\t\tassert(pos[0] + len <= b.size(), "submensaje truncado")')
    w('\t\tvar s := b.slice(pos[0], pos[0] + len)')
    w('\t\tpos[0] += len')
    w('\t\treturn s')
    w('')
    w('\tstatic func skip(b: PackedByteArray, pos: Array, wt: int) -> void:')
    w('\t\tmatch wt:')
    w('\t\t\t0: read_varint(b, pos)')
    w('\t\t\t2:')
    w('\t\t\t\tvar len := read_varint(b, pos)')
    w('\t\t\t\tpos[0] += len')
    w('\t\t\t5: pos[0] += 4')
    w('\t\t\t_: assert(false, "wiretype desconocido")')
    w('\t\tassert(pos[0] <= b.size(), "skip fuera de rango")')
    for nombre, definicion in spec.get('structs', {}).items():
        gd_clase(w, nombre, definicion, None)
    for nombre, definicion in spec['messages'].items():
        gd_clase(w, nombre, definicion, definicion['id'])
    return '\n'.join(L) + '\n'


# Nombres que colisionan con tipos nativos de GDScript o C#: el generador los
# rechaza en seco para que el error salga aqui y no como cuelgue en Godot.
RESERVADOS = {'Error', 'Object', 'Node', 'Resource', 'RefCounted', 'Signal', 'Callable',
              'Array', 'Dictionary', 'String', 'Variant', 'Wire'}


def validar_esquema(spec):
    """Invariantes del contrato: ids unicos, tags unicos por mensaje, structs existentes."""
    for nombre in list(spec.get('structs', {})) + list(spec['messages']):
        if nombre in RESERVADOS:
            raise SystemExit(f'"{nombre}" colisiona con un tipo nativo; renombrar en el esquema')
    ids = {}
    for nombre, m in spec['messages'].items():
        if m['id'] in ids:
            raise SystemExit(f'id {m["id"]} duplicado: {nombre} y {ids[m["id"]]}')
        ids[m['id']] = nombre
    for nombre, d in list(spec.get('structs', {}).items()) + list(spec['messages'].items()):
        tags = set()
        for c in d['fields']:
            if c['tag'] in tags:
                raise SystemExit(f'tag {c["tag"]} duplicado en {nombre}')
            tags.add(c['tag'])
            if c['type'] == 'struct' and c['struct'] not in spec.get('structs', {}):
                raise SystemExit(f'{nombre}.{c["name"]}: struct {c["struct"]} no existe')


if __name__ == '__main__':
    with open(ESQUEMA, encoding='utf-8') as f:
        spec = yaml.safe_load(f)
    validar_esquema(spec)
    os.makedirs(os.path.join(RAIZ, 'gen', 'csharp'), exist_ok=True)
    os.makedirs(os.path.join(RAIZ, 'gen', 'gdscript'), exist_ok=True)
    cs = os.path.join(RAIZ, 'gen', 'csharp', 'Messages.g.cs')
    gd = os.path.join(RAIZ, 'gen', 'gdscript', 'messages_g.gd')
    with open(cs, 'w', encoding='utf-8') as f:
        f.write(gen_csharp(spec))
    with open(gd, 'w', encoding='utf-8') as f:
        f.write(gen_gdscript(spec))
    n_msg = len(spec['messages'])
    n_str = len(spec.get('structs', {}))
    print(f'generado: {os.path.relpath(cs, RAIZ)}  ({n_msg} mensajes, {n_str} structs)')
    print(f'generado: {os.path.relpath(gd, RAIZ)}')
