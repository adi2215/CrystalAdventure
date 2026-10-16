using System;
using UnityEngine;

/// <summary>
/// Палитра «погоды» острова: небо, облака, свет. Одна тема — одна глава (утро, закат, ночь).
/// Значения можно крутить в инспекторе IslandAtmosphere прямо во время игры.
/// </summary>
[Serializable]
public class IslandTheme
{
    public string name = "Утро";

    [Header("Небо (сверху вниз)")]
    public Color skyTop = new Color32(98, 166, 230, 255);
    public Color skyMid = new Color32(170, 214, 245, 255);
    public Color skyHorizon = new Color32(252, 236, 210, 255);
    [Tooltip("Сияние солнца в левом верхнем углу")] public Color sunGlow = new Color32(255, 248, 222, 255);
    [Range(0f, 1f)] public float sunGlowStrength = 0.55f;
    [Tooltip("Звёзды на небе (для ночи)")] public bool stars;

    [Header("Облака")]
    public Color cloudLight = new Color32(255, 255, 255, 255);
    public Color cloudBase = new Color32(240, 245, 255, 255);
    public Color cloudShadow = new Color32(196, 212, 240, 255);
    [Tooltip("Море облаков внизу (под островом)")] public Color cloudSeaDeep = new Color32(176, 198, 236, 255);

    [Header("Свет")]
    [Tooltip("Рассеянный свет — им освещена теневая сторона блоков")] public Color ambient = new Color(0.80f, 0.85f, 1.00f);
    [Range(0f, 1.5f)] public float ambientIntensity = 0.70f;
    [Tooltip("Солнце — светит слева сверху")] public Color sun = new Color(1.00f, 0.93f, 0.80f);
    [Range(0f, 1.5f)] public float sunIntensity = 0.55f;
    [Tooltip("Свечение кристаллов")] public Color crystalLight = new Color(0.88f, 0.62f, 1.00f);
    [Range(0f, 2f)] public float crystalLightIntensity = 0.65f;

    [Header("Пылинки в воздухе")]
    public Color mote = new Color(1.00f, 0.97f, 0.86f, 1f);

    public static IslandTheme Morning() => new IslandTheme();

    public static IslandTheme Sunset() => new IslandTheme
    {
        name = "Закат",
        skyTop = new Color32(84, 92, 168, 255),
        skyMid = new Color32(236, 144, 140, 255),
        skyHorizon = new Color32(255, 206, 134, 255),
        sunGlow = new Color32(255, 222, 160, 255),
        sunGlowStrength = 0.65f,
        cloudLight = new Color32(255, 222, 196, 255),
        cloudBase = new Color32(246, 176, 168, 255),
        cloudShadow = new Color32(166, 124, 170, 255),
        cloudSeaDeep = new Color32(132, 104, 160, 255),
        ambient = new Color(0.76f, 0.68f, 0.96f),
        ambientIntensity = 0.62f,
        sun = new Color(1.00f, 0.74f, 0.52f),
        sunIntensity = 0.65f,
        crystalLight = new Color(0.95f, 0.62f, 1.00f),
        crystalLightIntensity = 0.8f,
        mote = new Color(1.00f, 0.86f, 0.62f, 1f),
    };

    public static IslandTheme Night() => new IslandTheme
    {
        name = "Ночь",
        skyTop = new Color32(16, 20, 56, 255),
        skyMid = new Color32(34, 44, 98, 255),
        skyHorizon = new Color32(70, 70, 134, 255),
        sunGlow = new Color32(206, 218, 255, 255),
        sunGlowStrength = 0.35f,
        stars = true,
        cloudLight = new Color32(142, 152, 218, 255),
        cloudBase = new Color32(86, 96, 160, 255),
        cloudShadow = new Color32(52, 58, 110, 255),
        cloudSeaDeep = new Color32(40, 44, 92, 255),
        ambient = new Color(0.46f, 0.52f, 0.88f),
        ambientIntensity = 0.55f,
        sun = new Color(0.76f, 0.84f, 1.00f),
        sunIntensity = 0.38f,
        crystalLight = new Color(0.90f, 0.62f, 1.00f),
        crystalLightIntensity = 1.2f,
        mote = new Color(0.80f, 0.86f, 1.00f, 1f),
    };
}
