using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Crystal Adventure → Добавить кнопку подсказки.
/// Создаёт в открытой сцене кнопку «?» рядом с Play и подключает к ней HintSystem.
/// Дальше её можно двигать и перерисовать своим спрайтом — логика останется.
/// </summary>
public static class HintButtonCreator
{
    [MenuItem("Crystal Adventure/Добавить кнопку подсказки")]
    static void Create()
    {
        var existing = Object.FindObjectOfType<HintSystem>(true);
        if (existing != null)
        {
            Selection.activeGameObject = existing.gameObject;
            EditorGUIUtility.PingObject(existing.gameObject);
            EditorUtility.DisplayDialog("Подсказка", "Кнопка подсказки уже есть в сцене — выделил её.", "Ок");
            return;
        }

        var moving = Object.FindObjectOfType<MovingManager>(true);
        if (moving == null || moving.Play == null)
        {
            EditorUtility.DisplayDialog("Подсказка", "Не нашёл MovingManager с кнопкой Play. Открой сцену CollectorLevel.", "Ок");
            return;
        }

        var play = (RectTransform)moving.Play.transform;
        var parent = play.parent;

        var go = new GameObject("HintButton", typeof(RectTransform), typeof(Image), typeof(Button), typeof(HintSystem));
        Undo.RegisterCreatedObjectUndo(go, "Create hint button");
        go.transform.SetParent(parent, false);
        go.layer = play.gameObject.layer;

        // Размер — квадрат высотой с Play, ставим над ним справа.
        var rt = (RectTransform)go.transform;
        rt.anchorMin = play.anchorMin;
        rt.anchorMax = play.anchorMin; // не растягиваем
        rt.pivot = new Vector2(0.5f, 0.5f);
        float size = Mathf.Max(40f, play.rect.height);
        rt.sizeDelta = new Vector2(size, size);
        Vector3 playTopRight = play.localPosition + new Vector3(play.rect.xMax, play.rect.yMax, 0f);
        rt.localPosition = playTopRight + new Vector3(-size * 0.5f, size * 0.5f + 8f, 0f);

        var image = go.GetComponent<Image>();
        image.sprite = AssetDatabase.GetBuiltinExtraResource<Sprite>("UI/Skin/UISprite.psd");
        image.type = Image.Type.Sliced;
        image.color = new Color(0.40f, 0.34f, 0.50f, 1f);

        var label = new GameObject("Text", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(go.transform, false);
        label.layer = go.layer;
        var lrt = (RectTransform)label.transform;
        lrt.anchorMin = Vector2.zero;
        lrt.anchorMax = Vector2.one;
        lrt.offsetMin = lrt.offsetMax = Vector2.zero;
        var text = label.GetComponent<TextMeshProUGUI>();
        text.text = "?";
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10;
        text.fontSizeMax = 200;
        text.color = Color.white;
        text.raycastTarget = false;

        var hint = go.GetComponent<HintSystem>();
        hint.moving = moving;
        hint.panels = Object.FindObjectOfType<PanelController>(true);

        var button = go.GetComponent<Button>();
        UnityEventTools.AddPersistentListener(button.onClick, hint.ShowHint);

        EditorSceneManager.MarkSceneDirty(go.scene);
        Selection.activeGameObject = go;
        Debug.Log("[Подсказка] Кнопка создана рядом с Play. Подвинь/перерисуй её как нужно и сохрани сцену.");
    }
}
