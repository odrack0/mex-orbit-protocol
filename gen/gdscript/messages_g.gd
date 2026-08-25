# GENERADO por tools/gen.py — no editar a mano. Fuente: schema/messages.yaml
class_name MexProtocol

const WT_VARINT := 0
const WT_LEN := 2
const WT_F32 := 5

enum EntityKind { PLAYER = 0, NPC = 1 }

class Wire:
	static func write_varint(buf: PackedByteArray, v: int) -> void:
		while v >= 0x80:
			buf.append((v & 0x7F) | 0x80)
			v >>= 7
		buf.append(v)

	static func read_varint(b: PackedByteArray, pos: Array) -> int:
		var v := 0
		var shift := 0
		while true:
			assert(pos[0] < b.size(), "varint truncado")
			var x := b[pos[0]]
			pos[0] += 1
			v |= (x & 0x7F) << shift
			if (x & 0x80) == 0:
				return v
			shift += 7
			assert(shift <= 63, "varint demasiado largo")
		return v

	static func write_tag(buf: PackedByteArray, tag: int, wt: int) -> void:
		write_varint(buf, (tag << 3) | wt)

	static func write_string(buf: PackedByteArray, v: String) -> void:
		var bytes := v.to_utf8_buffer()
		write_varint(buf, bytes.size())
		buf.append_array(bytes)

	static func read_string(b: PackedByteArray, pos: Array) -> String:
		var len := read_varint(b, pos)
		assert(pos[0] + len <= b.size(), "string truncado")
		var s := b.slice(pos[0], pos[0] + len).get_string_from_utf8()
		pos[0] += len
		return s

	static func write_f32(buf: PackedByteArray, v: float) -> void:
		var tmp := PackedByteArray()
		tmp.resize(4)
		tmp.encode_float(0, v)
		buf.append_array(tmp)

	static func read_f32(b: PackedByteArray, pos: Array) -> float:
		assert(pos[0] + 4 <= b.size(), "fixed32 truncado")
		var v := b.decode_float(pos[0])
		pos[0] += 4
		return v

	static func skip(b: PackedByteArray, pos: Array, wt: int) -> void:
		match wt:
			WT_VARINT: read_varint(b, pos)
			WT_LEN:
				var len := read_varint(b, pos)
				pos[0] += len
			WT_F32: pos[0] += 4
			_: assert(false, "wiretype desconocido")
		assert(pos[0] <= b.size(), "skip fuera de rango")

class Hello:
	const MSG_ID := 1
	var protocol_version: int = 0
	var game_ticket: String = ""

	func validate() -> void:
		assert(protocol_version >= 1, "Hello.protocol_version < 1")
		assert(protocol_version <= 1000, "Hello.protocol_version > 1000")
		assert(game_ticket.length() <= 512, "Hello.game_ticket demasiado largo")

	func encode() -> PackedByteArray:
		validate()
		var buf := PackedByteArray()
		Wire.write_varint(buf, 1)
		Wire.write_tag(buf, 1, 0)
		Wire.write_varint(buf, protocol_version)
		Wire.write_tag(buf, 2, 2)
		Wire.write_string(buf, game_ticket)
		return buf

	static func decode(b: PackedByteArray) -> Hello:
		var pos := [0]
		var id := Wire.read_varint(b, pos)
		assert(id == 1, "msg_id inesperado")
		var m := Hello.new()
		while pos[0] < b.size():
			var key := Wire.read_varint(b, pos)
			var tag := key >> 3
			var wt := key & 7
			match tag:
				1: m.protocol_version = Wire.read_varint(b, pos)
				2: m.game_ticket = Wire.read_string(b, pos)
				_: Wire.skip(b, pos, wt)
		m.validate()
		return m

class MoveIntent:
	const MSG_ID := 60
	var seq: int = 0
	var target_x: int = 0
	var target_y: int = 0

	func validate() -> void:
		assert(target_x >= 0, "MoveIntent.target_x < 0")
		assert(target_x <= 60000, "MoveIntent.target_x > 60000")
		assert(target_y >= 0, "MoveIntent.target_y < 0")
		assert(target_y <= 60000, "MoveIntent.target_y > 60000")

	func encode() -> PackedByteArray:
		validate()
		var buf := PackedByteArray()
		Wire.write_varint(buf, 60)
		Wire.write_tag(buf, 1, 0)
		Wire.write_varint(buf, seq)
		Wire.write_tag(buf, 2, 0)
		Wire.write_varint(buf, target_x)
		Wire.write_tag(buf, 3, 0)
		Wire.write_varint(buf, target_y)
		return buf

	static func decode(b: PackedByteArray) -> MoveIntent:
		var pos := [0]
		var id := Wire.read_varint(b, pos)
		assert(id == 60, "msg_id inesperado")
		var m := MoveIntent.new()
		while pos[0] < b.size():
			var key := Wire.read_varint(b, pos)
			var tag := key >> 3
			var wt := key & 7
			match tag:
				1: m.seq = Wire.read_varint(b, pos)
				2: m.target_x = Wire.read_varint(b, pos)
				3: m.target_y = Wire.read_varint(b, pos)
				_: Wire.skip(b, pos, wt)
		m.validate()
		return m

class EntitySpawn:
	const MSG_ID := 52
	var entity_id: int = 0
	var kind: int = 0
	var type_id: String = ""
	var name: String = ""
	var faction: int = 0
	var x: int = 0
	var y: int = 0
	var hp_pct: float = 0.0
	var speed: int = 0

	func validate() -> void:
		assert(type_id.length() <= 64, "EntitySpawn.type_id demasiado largo")
		assert(name.length() <= 64, "EntitySpawn.name demasiado largo")
		assert(faction <= 8, "EntitySpawn.faction > 8")
		assert(x <= 60000, "EntitySpawn.x > 60000")
		assert(y <= 60000, "EntitySpawn.y > 60000")
		assert(speed <= 2000, "EntitySpawn.speed > 2000")

	func encode() -> PackedByteArray:
		validate()
		var buf := PackedByteArray()
		Wire.write_varint(buf, 52)
		Wire.write_tag(buf, 1, 0)
		Wire.write_varint(buf, entity_id)
		Wire.write_tag(buf, 2, 0)
		Wire.write_varint(buf, kind)
		Wire.write_tag(buf, 3, 2)
		Wire.write_string(buf, type_id)
		Wire.write_tag(buf, 4, 2)
		Wire.write_string(buf, name)
		Wire.write_tag(buf, 5, 0)
		Wire.write_varint(buf, faction)
		Wire.write_tag(buf, 6, 0)
		Wire.write_varint(buf, x)
		Wire.write_tag(buf, 7, 0)
		Wire.write_varint(buf, y)
		Wire.write_tag(buf, 8, 5)
		Wire.write_f32(buf, hp_pct)
		Wire.write_tag(buf, 9, 0)
		Wire.write_varint(buf, speed)
		return buf

	static func decode(b: PackedByteArray) -> EntitySpawn:
		var pos := [0]
		var id := Wire.read_varint(b, pos)
		assert(id == 52, "msg_id inesperado")
		var m := EntitySpawn.new()
		while pos[0] < b.size():
			var key := Wire.read_varint(b, pos)
			var tag := key >> 3
			var wt := key & 7
			match tag:
				1: m.entity_id = Wire.read_varint(b, pos)
				2: m.kind = Wire.read_varint(b, pos)
				3: m.type_id = Wire.read_string(b, pos)
				4: m.name = Wire.read_string(b, pos)
				5: m.faction = Wire.read_varint(b, pos)
				6: m.x = Wire.read_varint(b, pos)
				7: m.y = Wire.read_varint(b, pos)
				8: m.hp_pct = Wire.read_f32(b, pos)
				9: m.speed = Wire.read_varint(b, pos)
				_: Wire.skip(b, pos, wt)
		m.validate()
		return m
