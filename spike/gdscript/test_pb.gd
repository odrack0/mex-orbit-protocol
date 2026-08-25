# Spike I2 (ruta protobuf): el GDScript generado por godobuf decodifica los bytes
# que produjo Google.Protobuf en C#, y re-codifica byte-exacto.
# Correr:  godot --headless --path spike/gdscript --script res://test_pb.gd
# (requiere messages_pb.gd generado por godobuf en esta carpeta)
extends SceneTree

const PB := preload("res://messages_pb.gd")


func _initialize() -> void:
	var ruta := ProjectSettings.globalize_path("res://") + "../out/move_intent_pb.bin"
	var f := FileAccess.open(ruta, FileAccess.READ)
	if f == null:
		push_error("FALLO: no pude abrir " + ruta)
		quit(1)
		return
	var datos := f.get_buffer(f.get_length())

	var m := PB.MoveIntent.new()
	var res := m.from_bytes(datos)
	if res != PB.PB_ERR.NO_ERRORS:
		push_error("FALLO: from_bytes devolvio " + str(res))
		quit(1)
		return
	if m.get_seq() != 42 or m.get_target_x() != 12345 or m.get_target_y() != 6789:
		push_error("FALLO: valores inesperados")
		quit(1)
		return
	if m.to_bytes() != datos:
		push_error("FALLO: re-encode no es byte-exacto")
		quit(1)
		return
	print("GODOBUF OK — decodifica lo de Google.Protobuf C# y re-codifica byte-exacto")
	quit(0)
