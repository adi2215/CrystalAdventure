using System;
using System.Reflection;
using UnityEngine;

/// <summary>
/// Свет острова на 2D-светах URP (Light2D).
///
/// Глобальный свет сцены становится «рассеянным» (холодным), сверху слева добавляется
/// далёкое «солнце» с картами нормалей — верх и левая грань блоков освещены тёпло,
/// правая остаётся в холодной тени. Кристаллы получают свой маленький свет.
///
/// К Light2D обращаемся через reflection по сохранённым полям (m_LightType и т.д.) —
/// так код не зависит от версии URP и компилируется без лишних ссылок.
/// Новые света — копии глобального света сцены (с теми же слоями сортировки).
/// </summary>
public static class IslandLighting
{
    const int Point = 3, Global = 4;
    const int NormalsAccurate = 1, NormalsDisabled = 2;

    static Type _type;
    static bool _searched;

    public static Type LightType
    {
        get
        {
            if (_searched) return _type;
            _searched = true;
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType("UnityEngine.Rendering.Universal.Light2D");
                if (t != null) { _type = t; break; }
            }
            return _type;
        }
    }

    /// <summary>Глобальный свет сцены (null — нет).</summary>
    public static Component FindGlobal()
    {
        var t = LightType;
        if (t == null) return null;
        foreach (var o in UnityEngine.Object.FindObjectsOfType(t))
        {
            var c = o as Component;
            if (c == null || !c.gameObject.activeInHierarchy) continue;
            if (GetInt(c, "m_LightType") == Global) return c;
        }
        return null;
    }

    /// <summary>Поменять цвет и силу света.</summary>
    public static void SetColor(Component light, Color color, float intensity)
    {
        if (light == null) return;
        color.a = 1f;
        if (!SetProperty(light, "color", color)) SetField(light, "m_Color", color);
        if (!SetProperty(light, "intensity", intensity)) SetField(light, "m_Intensity", intensity);
    }

    /// <summary>
    /// Точечный свет — копия <paramref name="template"/> (берём слои сортировки), перенастроенная до первого включения.
    /// normalDistance &gt; 0 — свет учитывает карты нормалей (насколько «над» картинкой висит источник).
    /// </summary>
    public static Component CreatePoint(Component template, string name, Transform parent, Vector3 position,
                                        Color color, float intensity, float innerRadius, float outerRadius,
                                        float falloff, float normalDistance)
    {
        if (template == null || LightType == null) return null;

        // Пока родитель выключен, Awake/OnEnable у копии не вызываются — успеваем сменить тип света.
        var holder = new GameObject("LightHolder");
        holder.SetActive(false);
        var go = UnityEngine.Object.Instantiate(template.gameObject, holder.transform);
        go.name = name;
        foreach (Transform child in go.transform) UnityEngine.Object.Destroy(child.gameObject);
        var light = go.GetComponent(LightType);
        if (light == null) { UnityEngine.Object.Destroy(holder); return null; }

        color.a = 1f;
        SetField(light, "m_LightType", Point);
        SetField(light, "m_Color", color);
        SetField(light, "m_Intensity", intensity);
        SetField(light, "m_PointLightInnerRadius", innerRadius);
        SetField(light, "m_PointLightOuterRadius", outerRadius);
        SetField(light, "m_PointLightInnerAngle", 360f);
        SetField(light, "m_PointLightOuterAngle", 360f);
        SetField(light, "m_FalloffIntensity", falloff);
        SetField(light, "m_LightOrder", 0);
        SetField(light, "m_ShadowIntensityEnabled", false);
        SetField(light, "m_NormalMapQuality", normalDistance > 0f ? NormalsAccurate : NormalsDisabled);
        SetField(light, "m_NormalMapDistance", Mathf.Max(0.01f, normalDistance));

        go.transform.SetParent(parent, false);
        go.transform.position = position;
        UnityEngine.Object.Destroy(holder);
        go.SetActive(true);
        // На всякий случай пересобрать форму света под новый тип (метод внутренний — если есть).
        Invoke(light, "UpdateMesh");
        Invoke(light, "UpdateBoundingSphere");
        return light;
    }

    static void Invoke(object o, string method)
    {
        try
        {
            var m = o.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public, null, Type.EmptyTypes, null);
            if (m != null) m.Invoke(o, null);
        }
        catch { /* необязательно */ }
    }

    // ───────────────────────── reflection ─────────────────────────

    static FieldInfo Field(Type t, string name)
    {
        for (; t != null; t = t.BaseType)
        {
            var f = t.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f != null) return f;
        }
        return null;
    }

    static int GetInt(object o, string field)
    {
        var f = Field(o.GetType(), field);
        if (f == null) return -1;
        try { return Convert.ToInt32(f.GetValue(o)); } catch { return -1; }
    }

    static void SetField(object o, string field, object value)
    {
        var f = Field(o.GetType(), field);
        if (f == null) return;
        try
        {
            if (f.FieldType.IsEnum) value = Enum.ToObject(f.FieldType, Convert.ToInt32(value));
            else if (f.FieldType != value.GetType()) value = Convert.ChangeType(value, f.FieldType);
            f.SetValue(o, value);
        }
        catch (Exception e) { Debug.LogWarning($"[IslandLighting] {field}: {e.Message}"); }
    }

    static bool SetProperty(object o, string name, object value)
    {
        var p = o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public);
        if (p == null || !p.CanWrite || p.PropertyType != value.GetType()) return false;
        try { p.SetValue(o, value); return true; }
        catch { return false; }
    }
}
