# mex-orbit-protocol

El contrato de comunicación del juego: **una sola definición de mensajes, generada para C# (server) y GDScript (cliente)**. Si no está aquí, no viaja por el cable.

> **MexOrbit** es nombre temporal del proyecto. Documentación en español; **código en inglés, comentarios en español**.

## Decisiones tomadas

- **Transporte: WebSocket sobre TLS (`wss://`) con payload binario.** Razones:
  - El framing por mensaje viene **del estándar** — la clase entera de bugs de framing del protocolo legado (el fix descartado de ago-2026) desaparece por diseño, no por parche.
  - TLS de fábrica: el protocolo en texto plano del DO clásico era la puerta de los bots (cualquiera lo leía con Wireshark); cifrar el transporte sube el piso del anti-cheat sin costo.
  - Godot 4 lo soporta nativo, atraviesa proxies/firewalls, y deja la puerta abierta a un cliente web.
- **Mensajes binarios tipados**, definidos aquí una sola vez y generados a ambos lenguajes (mecanismo de codegen por definir en el documento del pilar: protobuf vs esquema propio).
- **Versionado del protocolo desde el mensaje de handshake**: cliente y server negocian versión; nada de "adivinar por el formato".

## El anti-cheat como requisito de diseño (no como parche)

El protocolo se diseña para que hacer trampa sea difícil y detectarla sea barato:

1. **El servidor es la única verdad**: el cliente *pide* (intenciones), jamás *afirma* (resultados). Ningún mensaje del cliente contiene daño, posición final ni loot.
2. **Validación estructural gratuita**: mensajes tipados con rangos declarados — un valor fuera de rango se rechaza en la capa de deserialización, antes de tocar lógica.
3. **Números de secuencia y rate limits por tipo de mensaje** definidos en el contrato (N movimientos/s, N disparos/s) — el flooding se corta en la puerta.
4. **Sin canales de depuración en producción**: los comandos de admin/debug no existen en este contrato (viven en `mex-orbit-api-admin`).
5. Los análisis del protocolo legado y sus vectores (bots, packet-injection) están en `mex-orbit-docs/02-investigacion/decompilacion/` — la lista de lo que este diseño hace imposible.

## Qué NO es

- No implementa red: define el contrato. Las implementaciones viven en `mex-orbit-game-server` y `mex-orbit-client`.
- Ningún command-ID, string con pipes ni estructura heredada del protocolo Flash entra aquí.

## Estado

Repo recién creado. Primer paso: el documento de diseño del pilar (en `mex-orbit-docs/04-pilares/01-protocolo.md`) y la elección del mecanismo de codegen.
