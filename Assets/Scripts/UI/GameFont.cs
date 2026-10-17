using System.Collections.Generic;
using TMPro;
using UnityEngine;

/// <summary>
/// Шрифт игры для текстов, которые создаются кодом (диалоги туториала, карта глав, окно победы,
/// кнопка скорости, счётчик цикла).
///
/// Исправлено: в диалогах туториала пропал пиксельный шрифт. Раньше каждый скрипт брал шрифт у
/// «первого попавшегося» текста в сцене (FindObjectOfType&lt;TextMeshProUGUI&gt;), а после появления
/// кнопки скорости первым стал её текст со стандартным шрифтом TMP (LiberationSans — он же у «?»).
/// Теперь берётся самый частый шрифт среди текстов сцены, не считая стандартного.
/// </summary>
public static class GameFont
{
    static TMP_FontAsset _main;

    public static TMP_FontAsset Main
    {
        get
        {
            if (_main != null) return _main;
            var fallback = TMP_Settings.defaultFontAsset;
            var counts = new Dictionary<TMP_FontAsset, int>();
            foreach (var t in Object.FindObjectsOfType<TMP_Text>(true))
            {
                if (t == null || t.font == null || t.font == fallback) continue;
                counts.TryGetValue(t.font, out var n);
                counts[t.font] = n + 1;
            }
            TMP_FontAsset best = null;
            int bestCount = 0;
            foreach (var kv in counts)
                if (kv.Value > bestCount) { best = kv.Key; bestCount = kv.Value; }
            // Не кешируем «ничего не нашли» — сцена могла ещё не загрузиться.
            if (best != null) _main = best;
            return best != null ? best : fallback;
        }
    }
}
