using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Панели функций F1 и F2 заметно отличаются от Main — у каждой свой цвет.
///
/// Раньше панель F1 была той же рамкой, просто перекрашенной в бирюзовый (WinterUITabel), а F2 — третьим
/// вариантом; выглядело как случайная смена цвета. Теперь у всех панелей одна рамка (как у Main), а у функции —
/// свой акцент: F1 — янтарный, F2 — фиолетовый. Акцентом подсвечиваются:
///  • рамка панели (лёгкий оттенок),
///  • табличка сверху панели с надписью «F1» / «F2»,
///  • ячейки слотов программы,
///  • вкладка функции.
/// Всё делается кодом при загрузке уровня (PanelController.CheckPanels) — сцену менять не нужно.
/// </summary>
public static class FunctionPanelStyle
{
    public static readonly Color F1 = new Color(1.00f, 0.70f, 0.26f, 1f);  // янтарный
    public static readonly Color F2 = new Color(0.78f, 0.50f, 1.00f, 1f);  // фиолетовый

    const string HeaderName = "FunctionHeader";
    const string SlotSpriteName = "UITable_11"; // рамка пустого слота программы

    public static Color Accent(int panelIndex) => panelIndex == 2 ? F2 : F1;

    public static void Apply(PanelController pc)
    {
        if (pc == null || pc.panels == null || pc.panels.Length < 2 || pc.panels[0] == null) return;
        var mainBg = pc.panels[0].GetComponent<Image>();

        for (int i = 1; i < pc.panels.Length && i <= 2; i++)
        {
            var panel = pc.panels[i];
            if (panel == null) continue;
            var accent = Accent(i);

            // Рамка — как у Main, с лёгким оттенком цвета функции.
            var bg = panel.GetComponent<Image>();
            if (bg != null)
            {
                if (mainBg != null && mainBg.sprite != null)
                {
                    bg.sprite = mainBg.sprite;
                    bg.type = mainBg.type;
                    bg.pixelsPerUnitMultiplier = mainBg.pixelsPerUnitMultiplier;
                }
                bg.color = Color.Lerp(Color.white, accent, 0.32f);
            }

            // Ячейки слотов — цветом функции.
            foreach (var img in panel.GetComponentsInChildren<Image>(true))
                if (img != bg && img.sprite != null && img.sprite.name.StartsWith(SlotSpriteName))
                    img.color = Color.Lerp(Color.white, accent, 0.55f);

            AddHeader(panel.transform as RectTransform, i == 2 ? "F2" : "F1", accent);

            // Вкладка функции — тем же цветом.
            if (pc.buttonPanels != null && i < pc.buttonPanels.Length && pc.buttonPanels[i] != null)
            {
                var tab = pc.buttonPanels[i].GetComponent<Image>();
                if (tab != null) tab.color = Color.Lerp(Color.white, accent, 0.6f);
            }
        }
    }

    /// <summary>Табличка «F1» / «F2» по центру верхнего края панели.</summary>
    static void AddHeader(RectTransform panel, string label, Color accent)
    {
        if (panel == null || panel.Find(HeaderName) != null) return;

        var go = new GameObject(HeaderName, typeof(RectTransform));
        var rt = (RectTransform)go.transform;
        rt.SetParent(panel, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -4f);
        rt.sizeDelta = new Vector2(150f, 50f);
        rt.SetAsLastSibling();

        var img = go.AddComponent<Image>();
        img.sprite = PixelSprites.Tag;
        img.type = Image.Type.Sliced;
        img.pixelsPerUnitMultiplier = 0.2f;
        img.color = accent;
        img.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform));
        var trt = (RectTransform)textGo.transform;
        trt.SetParent(rt, false);
        trt.anchorMin = Vector2.zero;
        trt.anchorMax = Vector2.one;
        trt.offsetMin = trt.offsetMax = Vector2.zero;
        var t = textGo.AddComponent<TextMeshProUGUI>();
        if (GameFont.Main != null) t.font = GameFont.Main;
        t.text = label; // Main / F1 / F2 не переводятся — термины кода
        t.fontSize = 34f;
        t.alignment = TextAlignmentOptions.Center;
        t.color = PixelSprites.Ink;
        t.raycastTarget = false;
    }
}
