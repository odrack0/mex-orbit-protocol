# Protocolo v1 — diseño (E1)

**Estado: borrador para revisión** (2026-08-25). Pilar rector: [01-protocolo](https://github.com/odrack0/mex-orbit-docs/blob/main/04-pilares/01-protocolo.md). Insumo: análisis completo de la capa `net/` del prototipo Godot y de `docs/protocol-spec.md` + `pendientes-server.md` del legado.

## 0. Lo que el prototipo enseñó (y este diseño hace imposible)

| Pecado del legado | Respuesta estructural en v1 |
|---|---|
| Tres protocolos conviviendo (binario TCP:8080, canal de texto 4224, chat de texto TCP:9338) | **Un solo transporte, un solo canal**: todo mensaje es tipado y viaja por el mismo socket, chat incluido |
| Fallo = silencio (login malo, refinado rechazado, sesión zombi: nada distingue "rechazado" de "server colgado") | **Ningún silencio jamás**: todo request lleva `request_id` y todo rechazo responde `Error{request_id, code}` |
| Rotaciones de bits por campo (~400 constantes sin clave = ofuscación, no seguridad) | **Cero ofuscación**; la confidencialidad la da TLS |
| Server ignora el `len`, un frame por `receive()`, buffer 1024, resincronización ciega | El framing lo da **WebSocket** (RFC 6455): mensajes delimitados por el estándar, sin resincronización artesanal |
| Módulos inline sin longitud → imposible saltar lo desconocido | Wire format con **campos etiquetados y auto-descriptivos**: lo desconocido siempre se puede saltar |
| Cliente decide (distancia de colección, clamps, validación del laboratorio "en el cliente") | **El cliente solo manda intenciones**; toda validación vive en el server y en la capa de deserialización |
| Sin reconexión, sin secuencia, sin versión real | Handshake versionado + **resume de sesión** con token y ventana de gracia |
| Sesiones zombi y reemplazo silencioso | Sesión única **explícita**: `SessionReplaced` a la conexión vieja, siempre |
| `y` antes que `x`, semánticas invertidas, 40 % de campos basura | El contrato se **genera desde una sola definición**: no existe "orden memorizado", ni pads, ni magics |

## 1. Transporte y framing

- **`wss://` (WebSocket sobre TLS) nativo**, un solo endpoint del game server. Sin puente WS↔TCP, sin socket de chat aparte.
- Cada mensaje WS (binario) contiene exactamente **un mensaje del protocolo**:

```
[ msg_type : varint ][ payload : campos etiquetados ]
```

- El payload usa campos `tag + wire-type` (estilo protobuf: varint, fixed32, fixed64, length-delimited). **Regla de oro**: un decodificador siempre puede saltar un campo o mensaje que no conoce.
- Enteros en **varint zig-zag** donde aplique; strings UTF-8 length-delimited; sin floats asimétricos: `fixed32` IEEE-754 little-endian único.
- Tamaño máximo de mensaje: **64 KB** entrante (el server corta la conexión por encima), sin límite artificial de 1024.

## 2. Sesión: handshake, heartbeat, reconexión

### Handshake

```
C→S  Hello      { protocol_version, game_ticket }        ← ticket JWT de la api (ver auth-v1)
S→C  Welcome    { account_id, reconnect_token, server_time_ms, tick_rate, motd? }
  ó  ErrorReply { code: BAD_TICKET | VERSION_UNSUPPORTED | BANNED | ... }
```

- La versión se **negocia y valida** (el server acepta `N` y `N−1` durante rollouts). Nunca "se lee y se ignora".
- Si la cuenta ya tiene sesión viva: la vieja recibe **`SessionReplaced{}`** y se cierra limpia; la nueva entra. Sin silencios de minutos.

### Heartbeat

- `Ping{nonce}` del server cada **10 s**; el cliente responde `Pong{nonce}`. 3 fallos → socket muerto → ventana de gracia.
- Cualquier mensaje válido refresca la actividad (no solo "IDs registrados").

### Reconexión (resume)

```
C→S  Resume     { protocol_version, reconnect_token }
S→C  ResumeOk   { }  + re-sincronización completa (igual que EnterMap)
  ó  ErrorReply { code: RESUME_EXPIRED }
```

- Tras caída de socket, la nave permanece en el mundo **60 s**; dentro de esa ventana el resume evita pasar por la api. La re-sincronización es **estado completo**, no replay de deltas (simple y suficiente en v1).

## 3. Modelo de sincronización

- **Tick fijo del server: 80 ms (12,5 Hz)** — hereda el ritmo real del prototipo (~84 ms) como punto de partida calibrable.
- **Relevancia por rango** (valores iniciales, calibrables en BD): naves/NPCs 2000 unidades, cajas/minas 1250; portales, estaciones y POIs se envían completos al entrar al mapa. El objetivo seleccionado nunca sale de relevancia.
- **Movimiento**:
  - `C→S MoveIntent{ seq, target_x, target_y }` — solo intención. El server **clampea el destino a los límites del mapa** (adiós al Moving eterno del §7b) y valida cadencia.
  - `S→C EntityMove{ entity_id, x, y, target_x, target_y, speed, teleport? }` — **incluido el propio héroe**: eco autoritativo con la posición de origen; el cliente predice en optimista y **se reconcilia** contra el eco (el legado no corregía nunca).
  - Interpolación lineal en cliente con el par destino+velocidad (tiempo se deriva; no se transmite redundante).
- **Cambio de mapa**: mensaje explícito `EnterMap{ map_id, limits_x, limits_y, zone_flags }` seguido de la sincronización de entidades. El tamaño del mapa **se transmite**, no se adivina por id.

## 4. Catálogo de mensajes v1 (el vertical slice)

Cobertura E2: *login → conectar → volar → matar un Vex → recoger su carga → volver a base → refinado automático → almacén → vender al NPC*, más chat. Rangos de `msg_type` reservados por dominio (los dominios E3+ tienen rango pero no mensajes todavía).

### 1–49 · Sesión
| Msg | Dir | Campos |
|---|---|---|
| `Hello` | C→S | protocol_version, game_ticket |
| `Welcome` | S→C | account_id, reconnect_token, server_time_ms, tick_rate |
| `Resume` / `ResumeOk` | C→S / S→C | reconnect_token / — |
| `Ping` / `Pong` | S→C / C→S | nonce |
| `ErrorReply` | S→C | request_id?, code, detail? — el nombre evita la colisión con el tipo nativo `Error` de Godot |
| `SessionReplaced` | S→C | — |
| `LogoutRequest` / `LogoutCountdown` / `LogoutDone` | C→S / S→C / S→C | — / seconds_left (0=abortado) / — |

### 50–99 · Mundo y movimiento
| Msg | Dir | Campos |
|---|---|---|
| `EnterMap` | S→C | map_id, limits_x, limits_y, zone_flags (riesgo de carga, DMZ) |
| `EntitySpawn` | S→C | entity_id, kind (PLAYER/NPC), type_id, name?, faction, x, y, hp_pct, shield_pct, speed, flags |
| `EntityDespawn` | S→C | entity_id, reason (RANGE/LEFT/DEAD) — la razón viaja, no se infiere |
| `EntityMove` | S→C | entity_id, x, y, target_x, target_y, speed, teleport? |
| `MoveIntent` | C→S | seq, target_x, target_y |
| `SpeedChange` | S→C | entity_id, speed |
| `HeroStats` | S→C | hp, max_hp, shield, max_shield, cargo, max_cargo, credits, experience, level — **valores POST-evento, deltas etiquetados con razón** |

### 100–149 · Combate
| Msg | Dir | Campos |
|---|---|---|
| `SelectTarget` | C→S | entity_id (0 = deseleccionar) |
| `TargetInfo` | S→C | entity_id, hp, max_hp, shield, max_shield |
| `LaserToggle` | C→S | on (bool) — objetivo = el seleccionado server-side |
| `AttackEvent` | S→C | attacker_id, target_id, weapon (LASER/…), damage, target_hp, target_shield, missed — **valores POST-daño** |
| `EntityDestroyed` | S→C | entity_id, killer_id, explosion_type |
| `RespawnOptions` | S→C | options[{id, label_key, cost_credits, available}], cause, killer_name? |
| `RespawnSelect` | C→S | option_id |

### 150–199 · Loot y economía del slice
| Msg | Dir | Campos |
|---|---|---|
| `BoxSpawn` | S→C | box_id, box_type, x, y |
| `BoxDespawn` | S→C | box_id, reason (COLLECTED/EXPIRED/RANGE) — sin bools mentirosos |
| `CollectBox` | C→S | request_id, box_id — **el server valida distancia y estado** |
| `CollectResult` | S→C | request_id, drops[{material_id, amount}] — o `ErrorReply{TOO_FAR/GONE}` |
| `StorageState` | S→C | materials[{material_id, amount}] — almacén completo al entrar; deltas después |
| `StorageDelta` | S→C | material_id, delta, reason (COLLECT/REFINE/SELL) |
| `SellToNpc` | C→S | request_id, material_id, amount |
| `SellResult` | S→C | request_id, credits_gained, new_credits |

### 200–249 · Chat (mismo socket, mensajes tipados)
| Msg | Dir | Campos |
|---|---|---|
| `ChatSend` | C→S | request_id, channel, text (≤ 256) — **campos tipados: sin separadores que escapar** |
| `ChatMessage` | S→C | channel, from_name, from_clan?, text, server_time_ms |
| `ChatSystem` | S→C | text_key, params[] — localizable en cliente |
| `ChatWhisper` | C↔S | to_name / from_name, text |

### Rangos reservados (sin mensajes en v1)
250–299 misiones (E3) · 300–349 grupo (E5) · 350–399 mercado (E3) · 400–449 Materializador/Eclipses (E4) · 450–499 PET (fase 2) · 500+ libre.

**Total v1: ~35 mensajes** (contra ~160 estructuras + 2 protocolos de texto del legado).

## 5. Anti-cheat estructural (requisitos del contrato)

1. El cliente **solo** emite intenciones (`MoveIntent`, `LaserToggle`, `CollectBox`, `SellToNpc`); jamás posiciones propias como verdad ni resultados.
2. **Validación en la deserialización**: rangos por campo declarados en el esquema (coordenadas ≥ 0 y ≤ límite+margen, amounts > 0, strings con longitud máxima); violación = mensaje descartado + strike.
3. **Rate limits por tipo declarados en el contrato** (y generados en el server): `MoveIntent` 10/s · `LaserToggle` 4/s · `CollectBox` 4/s · `ChatSend` 2/s + burst 5 · `SellToNpc` 2/s. Exceso: descarte silencioso + strike; N strikes → desconexión auditada.
4. `seq` en `MoveIntent` (monótono por sesión): lo viejo o duplicado se descarta.
5. **Cero comandos de debug** en el contrato de producción: los flujos e2e usan cuentas de rol TestBot por la misma puerta que todos.

## 6. Codegen: protobuf vs esquema propio (decisión del spike I2)

**Una sola definición → C# (server/api) y GDScript (cliente).** Nada se escribe dos veces; el catálogo de §4 es la fuente.

| Criterio | protobuf | esquema propio (YAML → generadores) |
|---|---|---|
| Madurez C# | excelente (Google.Protobuf) | a construir |
| GDScript | plugins de terceros, calidad variable — **el riesgo a medir** | generador simple bajo nuestro control |
| Skippable/evolución | de fábrica | mismo wire format (tag+wiretype), lo heredamos |
| Rate limits/rangos en el contrato | no expresable (extensiones custom) | **de primera clase en el esquema** |
| Peso runtime en Godot | dependencia externa | cero dependencias |

**DECISIÓN (spike corrido el 2026-08-25): esquema propio.** Ambas rutas alcanzaron el roundtrip byte-exacto
C# ↔ GDScript (godobuf funcionó incluso en Godot 4.7.1); el factor decisivo fue que **los rangos y rate limits
son inexpresables en protobuf** — `target_x=999999` codifica sin queja en Google.Protobuf, mientras el esquema
propio lo rechaza en encode y decode desde el contrato mismo. Con protobuf, los metadatos anti-cheat serían un
sidecar paralelo que puede desincronizarse: la clase de bug que este protocolo existe para impedir. Evidencia
completa y reproducible en [`../spike/README.md`](../spike/README.md); el generador vive en `tools/gen.py` y
el esquema en `schema/messages.yaml`.

## 7. Ejemplo de definición (formato del esquema propio, si gana el spike)

```yaml
MoveIntent:
  id: 60
  dir: c2s
  rate_limit: { per_second: 10, burst: 4 }
  fields:
    - { tag: 1, name: seq,      type: varint }
    - { tag: 2, name: target_x, type: varint, min: 0, max: 30000 }
    - { tag: 3, name: target_y, type: varint, min: 0, max: 30000 }
```

## 8. Fuera de v1 (con rango reservado)

Misiones, grupos, mercado de órdenes, Materializador, PET, clanes, ajustes/keybindings sincronizados (v1: locales en el cliente), sistema estelar. Cada uno entra en su etapa **agregando mensajes, nunca cambiando los existentes** (campos nuevos = tags nuevos; los viejos jamás se renumeran).
