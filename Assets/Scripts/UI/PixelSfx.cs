using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 8-битные звуки, синтезированные кодом (квадрат/треугольник/шум) — ни одного аудиофайла.
/// PixelSfx.Play(Sfx.Pop) — откуда угодно. Громкость: PixelSfx.Volume (сохраняется в PlayerPrefs).
/// </summary>
public enum Sfx
{
    Blip,       // голос в диалоге (буква)
    Click,      // нажатие кнопки
    Pop,        // карточка появилась
    Remove,     // карточка убрана
    Deny,       // нельзя
    Step,       // шаг куба
    Jump,       // прыжок
    Land,       // приземление
    Crystal,    // взял кристалл
    Edge,       // «ой!» на краю
    Star,       // звезда в окне победы
    Plus,       // строка итогов «+»
    Minus,      // строка итогов «−»
    Win,        // победа
    Whoosh,     // улёт в небо / смена экрана
    Ghost,      // призрак-подсказка
    Open,       // открыть окно
    Close,      // закрыть окно
}

public static class PixelSfx
{
    const int Rate = 44100;
    const string VolumeKey = "sfx_volume";

    static readonly Dictionary<Sfx, AudioClip> Clips = new Dictionary<Sfx, AudioClip>();
    static AudioSource[] _pool;
    static int _next;
    static float _volume = -1f;
    static readonly Dictionary<Sfx, float> LastPlayed = new Dictionary<Sfx, float>();

    /// <summary>Общая громкость звуков 0..1.</summary>
    public static float Volume
    {
        get { if (_volume < 0f) _volume = PlayerPrefs.GetFloat(VolumeKey, 0.6f); return _volume; }
        set { _volume = Mathf.Clamp01(value); PlayerPrefs.SetFloat(VolumeKey, _volume); }
    }

    /// <param name="pitch">1 — как есть; 1.12 — на тон выше и т.д.</param>
    /// <param name="minInterval">не чаще, чем раз в N секунд (для частых звуков)</param>
    public static void Play(Sfx sfx, float volume = 1f, float pitch = 1f, float minInterval = 0.02f)
    {
        if (Volume <= 0f) return;
        float now = Time.unscaledTime;
        if (LastPlayed.TryGetValue(sfx, out var last) && now - last < minInterval) return;
        LastPlayed[sfx] = now;

        EnsurePool();
        var src = _pool[_next];
        _next = (_next + 1) % _pool.Length;
        if (src == null) return;
        if (sfx == Sfx.Win) GameMusic.Duck(0.3f, 1.4f);   // джингл победы поверх музыки
        src.pitch = pitch;
        src.PlayOneShot(Get(sfx), Volume * volume);
    }

    static void EnsurePool()
    {
        if (_pool != null && _pool[0] != null) return;
        var go = new GameObject("PixelSfx");
        UnityEngine.Object.DontDestroyOnLoad(go);
        _pool = new AudioSource[8];
        for (int i = 0; i < _pool.Length; i++)
        {
            var s = go.AddComponent<AudioSource>();
            s.playOnAwake = false;
            s.spatialBlend = 0f;
            _pool[i] = s;
        }
    }

    static AudioClip Get(Sfx sfx)
    {
        if (Clips.TryGetValue(sfx, out var clip) && clip != null) return clip;
        clip = Build(sfx);
        Clips[sfx] = clip;
        return clip;
    }

    // ───────────── синтез ─────────────

    static AudioClip Build(Sfx sfx)
    {
        switch (sfx)
        {
            case Sfx.Blip: return Tone("blip", 0.05f, t => 880f, Square(0.25f), Decay(0.05f), 0.22f);
            case Sfx.Click: return Tone("click", 0.045f, t => Mathf.Lerp(660f, 990f, t / 0.045f), Square(0.5f), Decay(0.045f), 0.2f);
            case Sfx.Pop: return Tone("pop", 0.09f, t => Mathf.Lerp(380f, 920f, t / 0.09f), Square(0.25f), Decay(0.09f), 0.25f);
            case Sfx.Remove: return Tone("remove", 0.11f, t => Mathf.Lerp(700f, 240f, t / 0.11f), Square(0.5f), Decay(0.11f), 0.22f);
            case Sfx.Deny: return Tone("deny", 0.2f, t => t < 0.08f ? 150f : (t < 0.11f ? 0f : 130f), Square(0.5f), t => t < 0.2f ? 1f - t / 0.2f * 0.6f : 0f, 0.22f);
            case Sfx.Step: return Mix("step", 0.05f, (t, ph) => Noise() * 0.5f * (1f - t / 0.05f) + Tri(ph) * 0.5f, t => 200f, 0.12f);
            case Sfx.Jump: return Tone("jump", 0.16f, t => Mathf.Lerp(300f, 820f, t / 0.16f), Square(0.25f), Decay(0.16f), 0.22f);
            case Sfx.Land: return Mix("land", 0.13f, (t, ph) => Noise() * 0.6f * (1f - t / 0.13f) + Tri(ph) * (1f - t / 0.13f), t => Mathf.Lerp(130f, 55f, t / 0.13f), 0.28f);
            case Sfx.Crystal: return Arpeggio("crystal", new[] { 1046.5f, 1318.5f, 1568f, 2093f }, 0.055f, Square(0.5f), 0.18f, 0.12f);
            case Sfx.Edge: return Tone("edge", 0.32f, t => t < 0.13f ? 520f + 15f * Mathf.Sin(t * 120f) : 390f + 15f * Mathf.Sin(t * 120f), Square(0.25f),
                                       t => t < 0.13f ? 1f : (t < 0.15f ? 0.2f : 1f - (t - 0.15f) / 0.17f), 0.2f);
            case Sfx.Star: return Mix("star", 0.35f, (t, ph) => (Tri(ph) * 0.6f + Square(0.5f)(ph * 2f) * 0.25f) * Mathf.Exp(-t * 9f), t => 1046.5f, 0.3f);
            case Sfx.Plus: return Arpeggio("plus", new[] { 784f, 1175f }, 0.07f, Square(0.25f), 0.08f, 0.2f);
            case Sfx.Minus: return Arpeggio("minus", new[] { 311f, 233f }, 0.09f, Square(0.5f), 0.1f, 0.18f);
            case Sfx.Win: return Arpeggio("win", new[] { 523f, 659f, 784f, 1046.5f, 784f, 1046.5f }, 0.085f, Square(0.5f), 0.35f, 0.18f);
            case Sfx.Whoosh: return Noisy("whoosh", 0.5f, t => Mathf.Sin(Mathf.PI * t / 0.5f), t => Mathf.Lerp(0.15f, 0.9f, t / 0.5f), 0.22f);
            case Sfx.Ghost: return Mix("ghost", 0.5f, (t, ph) => (Tri(ph) + Tri(ph * 1.5f) * 0.5f) * Mathf.Sin(Mathf.PI * t / 0.5f) * 0.6f,
                                       t => 660f + 20f * Mathf.Sin(t * 40f), 0.18f);
            case Sfx.Open: return Tone("open", 0.15f, t => Mathf.Lerp(300f, 640f, t / 0.15f), Tri, Decay(0.15f), 0.3f);
            case Sfx.Close: return Tone("close", 0.15f, t => Mathf.Lerp(640f, 300f, t / 0.15f), Tri, Decay(0.15f), 0.3f);
        }
        return Tone("none", 0.01f, t => 0f, Tri, t => 0f, 0f);
    }

    static Func<float, float> Square(float duty) => ph => (ph - Mathf.Floor(ph)) < duty ? 1f : -1f;
    static float Tri(float ph) { float x = ph - Mathf.Floor(ph); return x < 0.5f ? x * 4f - 1f : 3f - x * 4f; }
    static Func<float, float> Decay(float dur) => t => Mathf.Clamp01(1f - t / dur);

    static readonly System.Random Rng = new System.Random(7);
    static float Noise() => (float)(Rng.NextDouble() * 2.0 - 1.0);

    static AudioClip Tone(string name, float dur, Func<float, float> freq, Func<float, float> wave, Func<float, float> env, float gain)
        => Mix(name, dur, (t, ph) => wave(ph) * env(t), freq, gain);

    /// <summary>Общий генератор: фаза интегрируется по частоте, потом мягкий фильтр, чтобы «квадрат» не резал уши.</summary>
    static AudioClip Mix(string name, float dur, Func<float, float, float> sample, Func<float, float> freq, float gain)
    {
        int n = Mathf.Max(1, Mathf.CeilToInt(dur * Rate));
        var data = new float[n];
        float phase = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            phase += freq(t) / Rate;
            float s = sample(t, phase);
            lp += (s - lp) * 0.35f;                     // однополюсный ФНЧ
            float fade = Mathf.Min(1f, (n - i) / 200f); // без щелчка в конце
            data[i] = lp * gain * fade;
        }
        var clip = AudioClip.Create("sfx_" + name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }

    static AudioClip Arpeggio(string name, float[] notes, float noteDur, Func<float, float> wave, float tail, float gain)
    {
        float dur = notes.Length * noteDur + tail;
        return Mix(name, dur, (t, ph) =>
        {
            int idx = Mathf.Min(notes.Length - 1, (int)(t / noteDur));
            float local = t - idx * noteDur;
            float env = idx == notes.Length - 1 ? Mathf.Exp(-local * 7f) : 1f - local / noteDur * 0.5f;
            return wave(ph) * env;
        }, t => notes[Mathf.Min(notes.Length - 1, (int)(t / noteDur))], gain);
    }

    /// <summary>Шум с «частотой выборки», которая растёт — получается свист.</summary>
    static AudioClip Noisy(string name, float dur, Func<float, float> env, Func<float, float> brightness, float gain)
    {
        int n = Mathf.CeilToInt(dur * Rate);
        var data = new float[n];
        float hold = 0f, acc = 0f, lp = 0f;
        for (int i = 0; i < n; i++)
        {
            float t = i / (float)Rate;
            acc += brightness(t);
            if (acc >= 1f) { acc -= 1f; hold = Noise(); }
            lp += (hold - lp) * 0.2f;
            data[i] = lp * env(t) * gain * Mathf.Min(1f, (n - i) / 200f);
        }
        var clip = AudioClip.Create("sfx_" + name, n, 1, Rate, false);
        clip.SetData(data, 0);
        return clip;
    }
}
