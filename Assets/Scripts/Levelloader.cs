using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Переход между уровнями. Все уровни живут в одной сцене (индекс 0),
/// номер уровня хранится в Data.NextLevel.
///
/// Исправлено: двойной клик по «Next Level» запускал две загрузки и
/// перепрыгивал через уровень. Теперь пока идёт загрузка, новые вызовы игнорируются.
/// </summary>
public class Levelloader : MonoBehaviour
{
    public Animator transition;

    public float transitionTime = 1f;

    public Data tran;

    [Header("Улёт героя перед облаками")]
    [Tooltip("Перед переходом герой прыгает в небо и тает")] public bool heroFliesAway = true;
    [Tooltip("Через сколько секунд после начала улёта пустить облака")] public float cloudsDelay = 0.6f;

    private bool _loading;

    /// <summary>Идёт переход на другой уровень (кнопки меню прячутся).</summary>
    public static bool IsLoading { get; private set; }

    void OnDestroy() => IsLoading = false;

    /// <summary>Открыть уровень по номеру в списке (0 — первый). Для меню уровней.</summary>
    public void OpenLevel(int levelIndex)
    {
        if (_loading) return;
        StartCoroutine(LoadLevel(Mathf.Max(0, levelIndex), false));
    }

    void Update()
    {
        if (tran != null && tran.trans)
        {
            tran.trans = false;
            LoadNextLeve(0);
        }
    }

    /// <summary>level = 0 — следующий уровень, -1 — перезапуск текущего.</summary>
    public void LoadNextLeve(int level)
    {
        if (_loading) return;
        var scene = SceneManager.GetActiveScene();
        int next = scene.name == "Menu" ? scene.buildIndex : tran.NextLevel + 1 + level;
        StartCoroutine(LoadLevel(next, scene.buildIndex == 0));
    }

    public void LoadLevelIndex(int index)
    {
        if (_loading) return;
        var scene = SceneManager.GetActiveScene();
        int next = scene.name == "Menu" ? index + 1 : tran.NextLevel + 1;
        StartCoroutine(LoadLevel(next, index + 1 == 0));
    }

    /// <summary>Кнопки выбора уровня: index = -1..N → уровень index + 1.</summary>
    public void LoadLevelIndex2(int index)
    {
        if (_loading) return;
        StartCoroutine(LoadLevel(index + 1, index + 1 == 0));
    }

    IEnumerator LoadLevel(int nextLevel, bool openMenu)
    {
        _loading = true;
        IsLoading = true;
        tran.music = false;
        if (openMenu) tran.menuclose = false;
        tran.NextLevel = Mathf.Max(0, nextLevel);

        // Герой подпрыгивает и улетает в небо, следом идут облака перехода.
        var hero = heroFliesAway ? FindObjectOfType<CharacterCode>() : null;
        if (hero != null && hero.isActiveAndEnabled && hero.IsLanded)
        {
            hero.StartCoroutine(hero.FlyAway());
            yield return new WaitForSeconds(cloudsDelay);
        }

        if (transition != null) transition.SetTrigger("Start");
        yield return new WaitForSeconds(transitionTime);

        SceneManager.LoadScene(0);
    }
}
