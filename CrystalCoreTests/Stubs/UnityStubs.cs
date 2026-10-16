// Минимальные заглушки Unity API — только чтобы проверить, что код компилируется.
using System.Collections;
using System.Collections.Generic;

namespace UnityEngine
{
    public class Object
    {
        public string name;
        public int GetInstanceID() => 0;
        public static T Instantiate<T>(T original, Transform parent) where T : Object => original;
        public static T Instantiate<T>(T original, Vector3 position, Quaternion rotation) where T : Object => original;
        public static void Destroy(Object obj, float t = 0f) { }
        public static implicit operator bool(Object o) => o != null;
    }
    public class Component : Object
    {
        public Transform transform;
        public GameObject gameObject;
        public T GetComponent<T>() => default;
        public T GetComponentInChildren<T>() => default;
    }
    public class Behaviour : Component { public bool enabled; }
    public class MonoBehaviour : Behaviour
    {
        public Coroutine StartCoroutine(IEnumerator routine) => null;
        public void StopAllCoroutines() { }
    }
    public class ScriptableObject : Object { }
    public class GameObject : Object
    {
        public Transform transform;
        public bool activeSelf;
        public void SetActive(bool v) { }
    }
    public class Transform : Component
    {
        public Vector3 position, localScale;
        public Quaternion rotation;
    }
    public class Sprite : Object { }
    public class SpriteRenderer : Component { public Color color; public int sortingOrder; public bool flipX; public Sprite sprite; }
    public class RuntimeAnimatorController : Object { }
    public class Animator : Component { public RuntimeAnimatorController runtimeAnimatorController; public void SetTrigger(string n) { } }
    public sealed class Coroutine { }
    public class YieldInstruction { }
    public sealed class WaitForSeconds : YieldInstruction { public WaitForSeconds(float s) { } }

    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 one => new Vector3(1, 1, 1);
        public static Vector3 zero => new Vector3(0, 0, 0);
        public static Vector3 up => new Vector3(0, 1, 0);
        public static Vector3 down => new Vector3(0, -1, 0);
        public static Vector3 operator +(Vector3 a, Vector3 b) => new Vector3(a.x + b.x, a.y + b.y, a.z + b.z);
        public static Vector3 operator -(Vector3 a, Vector3 b) => new Vector3(a.x - b.x, a.y - b.y, a.z - b.z);
        public static Vector3 operator *(Vector3 a, float d) => new Vector3(a.x * d, a.y * d, a.z * d);
        public static Vector3 Lerp(Vector3 a, Vector3 b, float t) => a + (b - a) * t;
    }
    public struct Vector3Int { public int x, y, z; public Vector3Int(int x, int y, int z) { this.x = x; this.y = y; this.z = z; } }
    public struct Vector2Int { public int x, y; public Vector2Int(int x, int y) { this.x = x; this.y = y; } }
    public struct Quaternion
    {
        public static Quaternion identity => default;
        public static Quaternion Euler(float x, float y, float z) => default;
    }
    public struct Color
    {
        public float r, g, b, a;
        public Color(float r, float g, float b, float a) { this.r = r; this.g = g; this.b = b; this.a = a; }
        public static Color white => new Color(1, 1, 1, 1);
        public static Color operator *(Color c, float f) => new Color(c.r * f, c.g * f, c.b * f, c.a * f);
        public static Color Lerp(Color a, Color b, float t) => a;
    }
    public static class Mathf
    {
        public const float PI = 3.14159265f;
        public const float Rad2Deg = 57.29578f;
        public static float SmoothStep(float a, float b, float t) => t;
        public static float Sin(float f) => (float)System.Math.Sin(f);
        public static float Atan2(float y, float x) => (float)System.Math.Atan2(y, x);
        public static int Max(int a, int b) => a > b ? a : b;
        public static float Max(float a, float b) => a > b ? a : b;
        public static int Clamp(int v, int a, int b) => v < a ? a : v > b ? b : v;
    }
    public static class Time { public static float deltaTime; }
    public static class Debug
    {
        public static void Log(object m) { }
        public static void LogWarning(object m) { }
        public static void LogError(object m) { }
    }

    [System.AttributeUsage(System.AttributeTargets.Field)] public sealed class SerializeField : System.Attribute { }
    [System.AttributeUsage(System.AttributeTargets.Field)] public sealed class HideInInspector : System.Attribute { }
    public sealed class HeaderAttribute : System.Attribute { public HeaderAttribute(string h) { } }
    public sealed class TooltipAttribute : System.Attribute { public TooltipAttribute(string t) { } }
    public sealed class TextAreaAttribute : System.Attribute { public TextAreaAttribute(int a, int b) { } }
    public sealed class RangeAttribute : System.Attribute { public RangeAttribute(float a, float b) { } }
    public sealed class MinAttribute : System.Attribute { public MinAttribute(float a) { } }
    public sealed class CreateAssetMenuAttribute : System.Attribute { public string menuName; public string fileName; }
}

namespace UnityEngine.Tilemaps
{
    public class TileBase : ScriptableObject { }
    public class Tile : TileBase { public Sprite sprite; }
    public class Tilemap : Component
    {
        public Vector3 GetCellCenterWorld(Vector3Int c) => default;
        public void SetTile(Vector3Int c, TileBase t) { }
    }
}

namespace UnityEngine.Events
{
    public class UnityEvent { public void Invoke() { } }
    public class UnityEvent<T0> { public void Invoke(T0 a) { } }
    public class UnityEvent<T0, T1> { public void Invoke(T0 a, T1 b) { } }
    public class UnityEvent<T0, T1, T2> { public void Invoke(T0 a, T1 b, T2 c) { } }
}

namespace UnityEditor
{
    public sealed class MenuItem : System.Attribute { public MenuItem(string p) { } }
    public static class AssetDatabase
    {
        public static string[] FindAssets(string f) => new string[0];
        public static string GUIDToAssetPath(string g) => g;
        public static T LoadAssetAtPath<T>(string p) where T : UnityEngine.Object => null;
    }
}
