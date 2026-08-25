// Spike I2: prueba C# del contrato generado.
// 1) Construye los tres mensajes con valores canonicos.
// 2) encode -> spike/out/*.bin (los lee la prueba GDScript para el roundtrip entre lenguajes).
// 3) decode de sus propios bytes y verificacion campo a campo.
// 4) Verifica que la validacion rechaza valores fuera de rango.
using MexOrbit.Protocol;

var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "out");
Directory.CreateDirectory(outDir);

var hello = new Hello { ProtocolVersion = 1, GameTicket = "TICKET-SPIKE-0001" };
var move = new MoveIntent { Seq = 42, TargetX = 12345, TargetY = 6789 };
var spawn = new EntitySpawn
{
    EntityId = 9001,
    Kind = EntityKind.Npc,
    TypeId = "vex",
    Name = "Vex",
    Faction = 0,
    X = 10400,
    Y = 6400,
    HpPct = 0.875f,
    Speed = 270,
};

// mensaje compuesto: repeated de submensajes (drops) — la prueba de la I1 de E2
var collect = new CollectResult
{
    RequestId = 7,
    Drops =
    {
        new MaterialAmount { MaterialId = "material_asterium", Amount = 24 },
        new MaterialAmount { MaterialId = "material_coronium", Amount = 3 },
    },
};
// sint: un delta negativo (consumo del refinado)
var delta = new StorageDelta { MaterialId = "material_asterium", Delta = -30, Reason = StorageReason.RefineIn };

File.WriteAllBytes(Path.Combine(outDir, "hello.bin"), hello.Encode());
File.WriteAllBytes(Path.Combine(outDir, "move_intent.bin"), move.Encode());
File.WriteAllBytes(Path.Combine(outDir, "entity_spawn.bin"), spawn.Encode());
File.WriteAllBytes(Path.Combine(outDir, "collect_result.bin"), collect.Encode());
File.WriteAllBytes(Path.Combine(outDir, "storage_delta.bin"), delta.Encode());

// roundtrip propio
var h2 = Hello.Decode(hello.Encode());
var m2 = MoveIntent.Decode(move.Encode());
var s2 = EntitySpawn.Decode(spawn.Encode());
Assert(h2.ProtocolVersion == 1 && h2.GameTicket == "TICKET-SPIKE-0001", "Hello roundtrip");
Assert(m2.Seq == 42 && m2.TargetX == 12345 && m2.TargetY == 6789, "MoveIntent roundtrip");
Assert(s2.EntityId == 9001 && s2.Kind == EntityKind.Npc && s2.TypeId == "vex"
       && s2.X == 10400 && s2.Y == 6400 && Math.Abs(s2.HpPct - 0.875f) < 1e-6 && s2.Speed == 270,
       "EntitySpawn roundtrip");

var c2 = CollectResult.Decode(collect.Encode());
Assert(c2.RequestId == 7 && c2.Drops.Count == 2
       && c2.Drops[0].MaterialId == "material_asterium" && c2.Drops[0].Amount == 24
       && c2.Drops[1].MaterialId == "material_coronium" && c2.Drops[1].Amount == 3,
       "CollectResult roundtrip (repeated struct)");
var d2 = StorageDelta.Decode(delta.Encode());
Assert(d2.Delta == -30 && d2.Reason == StorageReason.RefineIn, "StorageDelta roundtrip (sint negativo)");

// la validacion por rangos rechaza en el encode y en el decode
try
{
    new MoveIntent { Seq = 1, TargetX = 999_999, TargetY = 0 }.Encode();
    Assert(false, "target_x fuera de rango debio rechazarse");
}
catch (ProtocolViolationException) { }

// un tag desconocido se salta sin romper (evolucion del contrato):
// se anexa a mano el campo tag=15 wiretype=varint con valor 777
var conExtra = move.Encode().ToList();
conExtra.Add(15 << 3 | 0);      // key
conExtra.Add(0x89);             // 777 en varint, byte bajo
conExtra.Add(0x06);             // 777 en varint, byte alto
var m3 = MoveIntent.Decode(conExtra.ToArray());
Assert(m3.TargetX == 12345, "campos conocidos sobreviven a un tag desconocido");

Console.WriteLine("CSHARP OK — catalogo completo: 5 mensajes codificados (incl. repeated struct y sint), rangos y skip probados");
return 0;

static void Assert(bool cond, string que)
{
    if (!cond) { Console.Error.WriteLine("FALLO: " + que); Environment.Exit(1); }
}
