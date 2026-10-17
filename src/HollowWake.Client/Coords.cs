namespace HollowWake.Client;

/// <summary>
/// The simulation is X east, Y north, Z up. Godot is X east, Y up, Z south.
/// Every conversion between the two goes through here and nowhere else.
/// </summary>
public static class Coords
{
    public static Godot.Vector3 ToGodot(System.Numerics.Vector3 v) => new(v.X, v.Z, -v.Y);
    public static Godot.Vector3 ToGodot(float x, float y, float z) => new(x, z, -y);
    public static System.Numerics.Vector3 ToCore(Godot.Vector3 v) => new(v.X, -v.Z, v.Y);
    public static Godot.Vector2 GroundToGodot(System.Numerics.Vector2 v) => new(v.X, -v.Y);
    public static System.Numerics.Vector2 GroundToCore(Godot.Vector2 v) => new(v.X, -v.Y);
}
