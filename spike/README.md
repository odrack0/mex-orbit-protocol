# Spike I2 — codegen: esquema propio vs protobuf (resultados)

**Corrido: 2026-08-25**, en esta máquina (dotnet 10.0.300, Godot 4.7.1, Python 3.12). Ambas rutas llegaron a
la prueba reina: **roundtrip byte-exacto entre C# y GDScript** con los mismos 3 mensajes (`Hello`,
`MoveIntent`, `EntitySpawn`).

## Cómo reproducir

```bash
# Ruta A - esquema propio
py -3 tools/gen.py                                                    # YAML -> C# + GDScript
dotnet run --project spike/csharp                                     # codifica, valida, escribe spike/out/*.bin
godot --headless --path spike/gdscript --script res://test.gd         # decodifica lo de C#, re-codifica byte-exacto

# Ruta B - protobuf
dotnet run --project spike/protobuf-csharp                            # Google.Protobuf + Grpc.Tools; escribe move_intent_pb.bin
# (messages_pb.gd se genero con godobuf headless: godot --headless -s addons/godobuf/godobuf_cmdln.gd --input=... --output=...)
godot --headless --path spike/gdscript --script res://test_pb.gd      # godobuf decodifica lo de Google.Protobuf
```

## Resultados

| Criterio | A · esquema propio | B · protobuf |
|---|---|---|
| C# compila y roundtrip | ✅ (generador de ~350 líneas, cero dependencias) | ✅ (Google.Protobuf + Grpc.Tools por NuGet) |
| GDScript compila y decodifica lo de C# **byte-exacto** | ✅ | ✅ (godobuf v0.7 headless, generó bien **incluso en Godot 4.7.1** siendo su master para 4.6) |
| Rangos (min/max) en el contrato | ✅ de primera clase: `target_x=999999` **rechazado en encode y decode** | ❌ inexpresable: `target_x=999999` codifica sin queja; la validación habría que duplicarla a mano en ambos lados |
| Rate limits declarados en el contrato | ✅ en el YAML, listos para generarse en el server | ❌ requeriría un sidecar paralelo al `.proto` |
| Campos desconocidos se saltan (evolución) | ✅ probado | ✅ (de fábrica) |
| Dependencias de runtime | ninguna | C#: Google.Protobuf · GDScript: runtime godobuf de 31 KB por archivo generado |
| Riesgo de terceros | el generador es nuestro | godobuf es de un tercero, apunta a la versión de Godot anterior a la nuestra (hoy funcionó; cada upgrade de Godot es una apuesta) |
| Fricción encontrada | ninguna (más allá de escribir el generador) | feed NuGet privado de la máquina bloqueaba el restore (se aisló con nuget.config); descarga y aprendizaje del flujo godobuf |

## Decisión

**Esquema propio** (ruta A). El factor decisivo no fue la fricción — godobuf resultó mejor de lo esperado —
sino que **los metadatos anti-cheat son parte del contrato**: los rangos por campo y los rate limits viven en
el YAML y se generan en ambos lados. Con protobuf serían un segundo sistema paralelo que puede desincronizarse,
que es exactamente la clase de bug que este protocolo existe para impedir. El wire format es compatible en
espíritu con protobuf (tag+wiretype, varint, saltable), así que no se pierde la propiedad de evolución.

Registrada en `docs/protocolo-v1.md` §6. Lo que sigue (E2): extender `tools/gen.py` al catálogo completo
(~35 mensajes), zigzag para enteros con signo cuando haga falta, y el generador de rate-limiters del server.
