// Spike I2 (ruta protobuf): mismos valores canonicos, roundtrip C# propio.
// La validacion por rangos NO existe: hay que escribirla a mano fuera del contrato.
using Google.Protobuf;
using MexOrbit.ProtocolPb;

var move = new MoveIntent { Seq = 42, TargetX = 12345, TargetY = 6789 };
var bytes = move.ToByteArray();
// bytes para la prueba cruzada con godobuf en GDScript
var outDir = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "out");
Directory.CreateDirectory(outDir);
File.WriteAllBytes(Path.Combine(outDir, "move_intent_pb.bin"), bytes);
var back = MoveIntent.Parser.ParseFrom(bytes);
if (back.Seq != 42 || back.TargetX != 12345 || back.TargetY != 6789)
{
    Console.Error.WriteLine("FALLO roundtrip protobuf");
    return 1;
}
// fuera de rango: protobuf lo acepta feliz — la validacion es responsabilidad de otro
var trampa = new MoveIntent { Seq = 1, TargetX = 999_999, TargetY = 0 };
_ = trampa.ToByteArray();   // no lanza: el contrato no conoce rangos
Console.WriteLine($"PROTOBUF-CSHARP OK — roundtrip bien; payload {bytes.Length} bytes; " +
                  "target_x=999999 codificado sin queja (sin rangos en el contrato)");
return 0;
