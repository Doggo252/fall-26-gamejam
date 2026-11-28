// Just enough of Unity for Assets/Scripts/Dungeon.cs and Assets/Scripts/DungeonSolver.cs to compile as-is.
namespace UnityEngine
{
    public readonly record struct Vector2Int(int x, int y)
    {
        public static Vector2Int up => new(0, 1);
        public static Vector2Int down => new(0, -1);
        public static Vector2Int left => new(-1, 0);
        public static Vector2Int right => new(1, 0);
        public static Vector2Int zero => new(0, 0);
        public static Vector2Int one => new(1, 1);
        public static Vector2Int operator +(Vector2Int a, Vector2Int b) => new(a.x + b.x, a.y + b.y);
        public static Vector2Int operator -(Vector2Int a, Vector2Int b) => new(a.x - b.x, a.y - b.y);
        public static Vector2Int operator -(Vector2Int a) => new(-a.x, -a.y);
        public static explicit operator Vector2Int(Vector3Int v) => new(v.x, v.y);
        public override string ToString() => $"({x}, {y})";
    }

    public readonly record struct Vector3Int(int x, int y, int z);

    public static class Mathf
    {
        public static int Abs(int v) => System.Math.Abs(v);
    }

    // Only for DungeonSolver's "Solve Open Level" menu item, which never runs here.
    public class Object
    {
        public static T FindAnyObjectByType<T>() where T : class => null;
    }

    public static class Debug
    {
        public static void Log(object o) => System.Console.WriteLine(o);
        public static void LogWarning(object o) => System.Console.WriteLine(o);
    }
}

namespace UnityEditor
{
    public class MenuItemAttribute : System.Attribute
    {
        public MenuItemAttribute(string path) { }
    }
}

public class DungeonGame
{
    public Dungeon ReadLevel() => null;
}
