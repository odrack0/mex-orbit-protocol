# Spike I2: prueba GDScript del contrato generado.
# Lee los .bin que escribio la prueba C#, los decodifica, verifica campo a campo,
# re-codifica y compara byte a byte -> roundtrip C# <-> GDScript demostrado.
# Correr:  godot --headless --path spike/gdscript --script res://test.gd
extends SceneTree

const P := preload("res://messages_g.gd")


func _fallo(que: String) -> void:
	push_error("FALLO: " + que)
	quit(1)


func _leer(nombre: String) -> PackedByteArray:
	var ruta := ProjectSettings.globalize_path("res://") + "../out/" + nombre
	var f := FileAccess.open(ruta, FileAccess.READ)
	if f == null:
		_fallo("no pude abrir " + ruta)
		return PackedByteArray()
	return f.get_buffer(f.get_length())


func _initialize() -> void:
	var ok := true

	# --- Hello ---
	var hb := _leer("hello.bin")
	var h := P.Hello.decode(hb)
	if h.protocol_version != 1 or h.game_ticket != "TICKET-SPIKE-0001":
		_fallo("Hello: valores inesperados")
		ok = false
	if h.encode() != hb:
		_fallo("Hello: re-encode no es byte-exacto")
		ok = false

	# --- MoveIntent ---
	var mb := _leer("move_intent.bin")
	var m := P.MoveIntent.decode(mb)
	if m.seq != 42 or m.target_x != 12345 or m.target_y != 6789:
		_fallo("MoveIntent: valores inesperados")
		ok = false
	if m.encode() != mb:
		_fallo("MoveIntent: re-encode no es byte-exacto")
		ok = false

	# --- EntitySpawn ---
	var sb := _leer("entity_spawn.bin")
	var s := P.EntitySpawn.decode(sb)
	if s.entity_id != 9001 or s.kind != P.EntityKind.NPC or s.type_id != "vex" \
			or s.x != 10400 or s.y != 6400 or absf(s.hp_pct - 0.875) > 1e-6 or s.speed != 270:
		_fallo("EntitySpawn: valores inesperados")
		ok = false
	if s.encode() != sb:
		_fallo("EntitySpawn: re-encode no es byte-exacto")
		ok = false

	# --- CollectResult: repeated de submensajes ---
	var cb := _leer("collect_result.bin")
	var c := P.CollectResult.decode(cb)
	if c.request_id != 7 or c.drops.size() != 2 \
			or c.drops[0].material_id != "material_asterium" or c.drops[0].amount != 24 \
			or c.drops[1].material_id != "material_coronium" or c.drops[1].amount != 3:
		_fallo("CollectResult: valores inesperados")
		ok = false
	if c.encode() != cb:
		_fallo("CollectResult: re-encode no es byte-exacto")
		ok = false

	# --- StorageDelta: sint negativo ---
	var db := _leer("storage_delta.bin")
	var d := P.StorageDelta.decode(db)
	if d.delta != -30 or d.reason != P.StorageReason.REFINE_IN:
		_fallo("StorageDelta: valores inesperados")
		ok = false
	if d.encode() != db:
		_fallo("StorageDelta: re-encode no es byte-exacto")
		ok = false

	if ok:
		print("GDSCRIPT OK — catalogo completo: decodifica lo de C# y re-codifica byte-exacto (incl. repeated struct y sint)")
		quit(0)
