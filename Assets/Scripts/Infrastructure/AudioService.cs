using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Weiqi.Infrastructure
{
    public enum Sfx { Hover, Click, StoneBlack, StoneWhite, Capture, Illegal, Pass, Start, Win, Lose }

    /// <summary>Music playback with crossfade plus fully synthesised sound effects (no audio assets needed).</summary>
    public sealed class AudioService : MonoBehaviour
    {
        const string MusicKey = "wq_music", SfxKey = "wq_sfx";
        const int Rate = 44100;

        public static AudioService Instance { get; private set; }

        AudioSource bgm, bgmB, sfx;
        AudioSource activeBgm;
        readonly Dictionary<Sfx, AudioClip[]> clips = new Dictionary<Sfx, AudioClip[]>();
        readonly Dictionary<string, AudioClip> music = new Dictionary<string, AudioClip>();
        string currentTrack;
        Coroutine fade;

        public bool MusicOn { get; private set; }
        public bool SfxOn { get; private set; }

        public static AudioService Create()
        {
            var go = new GameObject("AudioService");
            DontDestroyOnLoad(go);
            return go.AddComponent<AudioService>();
        }

        void Awake()
        {
            Instance = this;
            MusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;
            SfxOn = PlayerPrefs.GetInt(SfxKey, 1) == 1;

            bgm = NewSource("BGM A", true);
            bgmB = NewSource("BGM B", true);
            sfx = NewSource("SFX", false);
            activeBgm = bgm;

            BuildClips();
        }

        AudioSource NewSource(string n, bool loop)
        {
            var s = new GameObject(n).AddComponent<AudioSource>();
            s.transform.SetParent(transform, false);
            s.playOnAwake = false;
            s.loop = loop;
            s.volume = 0f;
            return s;
        }

        // ------------------------------------------------------------ public API

        public void SetMusic(bool on)
        {
            MusicOn = on;
            PlayerPrefs.SetInt(MusicKey, on ? 1 : 0);
            if (on) { if (currentTrack != null) { var t = currentTrack; currentTrack = null; PlayMusic(t); } }
            else StartFade(null, 0.4f);
        }

        public void SetSfx(bool on)
        {
            SfxOn = on;
            PlayerPrefs.SetInt(SfxKey, on ? 1 : 0);
        }

        public void PlayMusic(string resourceName, float fadeTime = 1.2f)
        {
            if (currentTrack == resourceName) return;
            currentTrack = resourceName;
            if (!MusicOn) return;
            if (!music.TryGetValue(resourceName, out var clip))
            {
                clip = Resources.Load<AudioClip>("Audio/" + resourceName);
                music[resourceName] = clip;
            }
            StartFade(clip, fadeTime);
        }

        public void Play(Sfx kind, float volume = 1f, float pitch = 1f)
        {
            if (!SfxOn || !clips.TryGetValue(kind, out var set) || set.Length == 0) return;
            var clip = set[UnityEngine.Random.Range(0, set.Length)];
            sfx.pitch = pitch;
            sfx.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------ music crossfade

        void StartFade(AudioClip next, float time)
        {
            if (fade != null) StopCoroutine(fade);
            fade = StartCoroutine(Crossfade(next, time));
        }

        IEnumerator Crossfade(AudioClip next, float time)
        {
            AudioSource from = activeBgm;
            AudioSource to = from == bgm ? bgmB : bgm;
            const float target = 0.42f;
            if (next != null)
            {
                to.clip = next;
                to.volume = 0f;
                to.Play();
                activeBgm = to;
            }
            float fromStart = from.volume;
            for (float t = 0; t < time; t += Time.unscaledDeltaTime)
            {
                float k = Mathf.Clamp01(t / time);
                from.volume = Mathf.Lerp(fromStart, 0f, k);
                if (next != null) to.volume = Mathf.Lerp(0f, target, k);
                yield return null;
            }
            from.volume = 0f;
            from.Stop();
            if (next != null) to.volume = target;
            fade = null;
        }

        // ------------------------------------------------------------ synthesis

        void BuildClips()
        {
            var rng = new System.Random(7);
            clips[Sfx.Hover] = new[] { Make("hover", 0.05f, (t, n) => Tone(t, 2200f, 70f) * 0.20f) };
            clips[Sfx.Click] = new[]
            {
                Make("click", 0.22f, (t, n) => Tone(t, 880f, 16f) * 0.30f + Tone(t, 1320f, 22f) * 0.14f + Tone(t, 190f, 70f) * 0.35f)
            };

            clips[Sfx.StoneBlack] = MakeStoneSet(rng, 0.97f, 4);
            clips[Sfx.StoneWhite] = MakeStoneSet(rng, 1.08f, 4);

            // rattle of several stones being lifted
            var cap = new List<AudioClip>();
            for (int v = 0; v < 2; v++)
            {
                var offs = new float[6]; var pit = new float[6];
                for (int i = 0; i < 6; i++) { offs[i] = i * 0.055f + (float)rng.NextDouble() * 0.03f; pit[i] = 0.85f + (float)rng.NextDouble() * 0.5f; }
                cap.Add(Make("capture" + v, 0.7f, (t, n) =>
                {
                    float s = 0;
                    for (int i = 0; i < 6; i++) { float tt = t - offs[i]; if (tt >= 0) s += Clack(tt, pit[i], n) * (1f - i * 0.1f); }
                    s += Tone(t, 330f, 6f) * 0.12f * Mathf.Exp(-t * 2f);
                    return s * 0.7f;
                }));
            }
            clips[Sfx.Capture] = cap.ToArray();

            clips[Sfx.Illegal] = new[]
            {
                Make("illegal", 0.28f, (t, n) =>
                {
                    float env = Mathf.Exp(-t * 14f);
                    float sq = Mathf.Sign(Mathf.Sin(2 * Mathf.PI * (150f - t * 120f) * t));
                    return (sq * 0.5f + Mathf.Sin(2 * Mathf.PI * 75f * t) * 0.5f) * env * 0.35f;
                })
            };

            clips[Sfx.Pass] = new[]
            {
                Make("pass", 0.7f, (t, n) => (Tone(t, 523f, 5f) + Tone(t, 784f, 6f) * 0.5f + (t > 0.14f ? Tone(t - 0.14f, 392f, 5f) * 0.6f : 0f)) * 0.22f)
            };

            // temple gong: inharmonic partials with long decay
            clips[Sfx.Start] = new[]
            {
                Make("start", 2.6f, (t, n) =>
                {
                    float s = Tone(t, 98f, 1.4f) + Tone(t, 147f, 1.9f) * 0.7f + Tone(t, 231f, 2.4f) * 0.5f
                            + Tone(t, 312f, 3.2f) * 0.35f + Tone(t, 463f, 4.4f) * 0.22f + Tone(t, 701f, 6f) * 0.12f;
                    s *= 1f - Mathf.Exp(-t * 90f);                      // soft mallet attack
                    s *= 1f + 0.12f * Mathf.Sin(2 * Mathf.PI * 3.1f * t);  // slow beating
                    return s * 0.30f;
                })
            };

            clips[Sfx.Win] = new[] { Arpeggio(new[] { 0, 4, 7, 12, 16, 19, 24 }, 0.13f, 392f, 3.2f) };
            clips[Sfx.Lose] = new[] { Arpeggio(new[] { 12, 9, 5, 0 }, 0.30f, 261.6f, 2.6f) };
        }

        AudioClip[] MakeStoneSet(System.Random rng, float basePitch, int count)
        {
            var arr = new AudioClip[count];
            for (int v = 0; v < count; v++)
            {
                float p = basePitch * (0.92f + (float)rng.NextDouble() * 0.2f);
                arr[v] = Make("stone" + v, 0.32f, (t, n) => Clack(t, p, n) * 0.95f);
            }
            return arr;
        }

        /// <summary>Wooden "clack": short noise burst, resonant body modes and a low thump.</summary>
        static float Clack(float t, float pitch, System.Random n)
        {
            float noise = ((float)n.NextDouble() * 2f - 1f) * Mathf.Exp(-t * 520f) * 0.55f;
            float body = Tone(t, 1180f * pitch, 38f) * 0.40f + Tone(t, 1870f * pitch, 55f) * 0.20f
                       + Tone(t, 560f * pitch, 24f) * 0.34f;
            float thump = Tone(t, 150f * pitch, 60f) * 0.50f;
            return (noise + body + thump) * (1f - Mathf.Exp(-t * 2500f));
        }

        static float Tone(float t, float freq, float decay) => Mathf.Sin(2 * Mathf.PI * freq * t) * Mathf.Exp(-t * decay);

        AudioClip Arpeggio(int[] semis, float step, float root, float length)
        {
            return Make("arp", length, (t, n) =>
            {
                float s = 0;
                for (int i = 0; i < semis.Length; i++)
                {
                    float tt = t - i * step;
                    if (tt < 0) continue;
                    float f = root * Mathf.Pow(2f, semis[i] / 12f);
                    // plucked-string flavour: harmonics decaying at different rates
                    s += (Tone(tt, f, 2.4f) + Tone(tt, f * 2f, 4f) * 0.4f + Tone(tt, f * 3f, 7f) * 0.18f) * (1f - Mathf.Exp(-tt * 600f));
                }
                return s * 0.20f;
            });
        }

        static AudioClip Make(string name, float seconds, Func<float, System.Random, float> gen)
        {
            int len = Mathf.CeilToInt(seconds * Rate);
            var data = new float[len];
            var noise = new System.Random(name.GetHashCode());
            float fadeOut = Mathf.Min(0.03f, seconds * 0.2f);
            for (int i = 0; i < len; i++)
            {
                float t = i / (float)Rate;
                float v = gen(t, noise);
                float left = seconds - t;
                if (left < fadeOut) v *= left / fadeOut;
                data[i] = Mathf.Clamp(v, -1f, 1f);
            }
            var clip = AudioClip.Create(name, len, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
