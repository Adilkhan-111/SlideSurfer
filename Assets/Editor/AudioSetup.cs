using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Sound for the game: synthesises the sound effects into WAV files (no recordings needed), finds the music's
/// loop points (skipping the silence an MP3 has at its ends), sets the import options for mobile and puts an
/// AudioManager into the scene. Run: Tools > SlideSurfer > Setup Audio. Running it again regenerates the effects.
/// </summary>
public static class AudioSetup
{
    private const string MusicDir = "Assets/Audio/Music";
    private const string SfxDir = "Assets/Audio/SFX";
    private const string ScenePath = "Assets/Scenes/MainGame.unity";
    private const int Rate = 44100;

    private static readonly System.Random rng = new System.Random(12345);

    [MenuItem("Tools/SlideSurfer/Setup Audio")]
    public static void SetupAll()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorUtility.DisplayDialog("Audio", "Stop Play mode first.", "OK");
            return;
        }
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Scene scene = SceneManager.GetActiveScene();
        if (scene.path != ScenePath) scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

        EnsureFolder(SfxDir);
        Write("Coin", Coin());
        Write("Jump", Jump());
        Write("Land", Splash(0.45f, 3500f, 0.7f));
        Write("Crash", Crash());
        Write("GameOver", GameOverJingle());
        Write("SharkWarning", SharkWarning());
        Write("SharkSplash", Splash(0.9f, 6000f, 1f));
        Write("Click", Click());
        Write("WaterLoop", WaterLoop(4f));
        AssetDatabase.Refresh();
        foreach (string name in new[] { "Coin", "Jump", "Land", "Crash", "GameOver", "SharkWarning", "SharkSplash", "Click", "WaterLoop" })
            ImportSfx(SfxDir + "/" + name + ".wav");

        AudioClip music = FindMusic();
        Vector2 loop = music != null ? FindLoopPoints(music) : Vector2.zero;
        if (music != null) ImportMusic(AssetDatabase.GetAssetPath(music));

        // the manager in the scene
        AudioManager manager = UnityEngine.Object.FindAnyObjectByType<AudioManager>();
        if (manager == null)
        {
            var go = new GameObject("AudioManager");
            Undo.RegisterCreatedObjectUndo(go, "Audio manager");
            manager = go.AddComponent<AudioManager>();
        }
        var so = new SerializedObject(manager);
        so.FindProperty("music").objectReferenceValue = music;
        so.FindProperty("loopStart").floatValue = loop.x;
        so.FindProperty("loopEnd").floatValue = loop.y;
        so.FindProperty("gameSettings").objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameSettings>("Assets/GameSettings.asset");
        Set(so, "coin", "Coin"); Set(so, "jump", "Jump"); Set(so, "land", "Land"); Set(so, "crash", "Crash");
        Set(so, "gameOver", "GameOver"); Set(so, "sharkWarning", "SharkWarning"); Set(so, "sharkSplash", "SharkSplash");
        Set(so, "click", "Click"); Set(so, "water", "WaterLoop");
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log("AudioSetup: done. Music loop " + loop.x.ToString("F2") + "s .. " + loop.y.ToString("F2") + "s");
    }

    private static void Set(SerializedObject so, string field, string clip)
        => so.FindProperty(field).objectReferenceValue = AssetDatabase.LoadAssetAtPath<AudioClip>(SfxDir + "/" + clip + ".wav");

    // ---------------------------------------------------------------- music

    private static AudioClip FindMusic()
    {
        foreach (string guid in AssetDatabase.FindAssets("t:AudioClip", new[] { MusicDir }))
            return AssetDatabase.LoadAssetAtPath<AudioClip>(AssetDatabase.GUIDToAssetPath(guid));
        Debug.LogWarning("AudioSetup: no music in " + MusicDir);
        return null;
    }

    // first and last moment the music is actually audible
    private static Vector2 FindLoopPoints(AudioClip clip)
    {
        string path = AssetDatabase.GetAssetPath(clip);
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        // GetData needs the samples in memory: decompress for the analysis, then switch to streaming
        var s = importer.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;
        importer.defaultSampleSettings = s;
        importer.loadInBackground = false;   // with background loading GetData returns silence
        importer.SaveAndReimport();
        clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        clip.LoadAudioData();

        var data = new float[clip.samples * clip.channels];
        clip.GetData(data, 0);
        const float threshold = 0.01f;
        int first = 0, last = data.Length - 1;
        while (first < data.Length && Mathf.Abs(data[first]) < threshold) first++;
        while (last > first && Mathf.Abs(data[last]) < threshold) last--;
        float start = (float)(first / clip.channels) / clip.frequency;
        float end = (float)(last / clip.channels) / clip.frequency;
        return new Vector2(start, end);
    }

    private static void ImportMusic(string path)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var s = importer.defaultSampleSettings;
        s.loadType = AudioClipLoadType.Streaming;     // a 1.5 minute track would cost ~35 MB decompressed on a phone
        s.compressionFormat = AudioCompressionFormat.Vorbis;
        s.quality = 0.7f;
        importer.defaultSampleSettings = s;
        importer.loadInBackground = true;
        importer.SaveAndReimport();
    }

    private static void ImportSfx(string path)
    {
        var importer = (AudioImporter)AssetImporter.GetAtPath(path);
        var s = importer.defaultSampleSettings;
        s.loadType = AudioClipLoadType.DecompressOnLoad;   // short and frequent: keep them ready to play
        s.compressionFormat = AudioCompressionFormat.ADPCM;
        importer.defaultSampleSettings = s;
        importer.forceToMono = true;
        importer.SaveAndReimport();
    }

    // ---------------------------------------------------------------- the effects

    // bright two-note chime, like picking up a coin in a mobile runner
    private static float[] Coin()
    {
        float[] b = Buffer(0.32f);
        Tone(b, 0f, 0.32f, 1318.5f, 0.5f, 0.10f, Bell);
        Tone(b, 0.065f, 0.32f, 1975.5f, 0.6f, 0.14f, Bell);
        return b;
    }

    // a quick water pop and an upward whoosh
    private static float[] Jump()
    {
        float[] b = Buffer(0.45f);
        Chirp(b, 0f, 0.09f, 280f, 950f, 0.5f);
        var f = new Svf();
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate;
            float cutoff = Mathf.Lerp(500f, 3200f, t / 0.45f);
            float env = Mathf.Sin(Mathf.Clamp01(t / 0.45f) * Mathf.PI);
            b[i] += f.Band(Noise(), cutoff, 0.35f) * env * 0.9f;
        }
        return b;
    }

    // a splash: noise that darkens as it fades, a low thump and some droplets
    private static float[] Splash(float length, float brightness, float size)
    {
        float[] b = Buffer(length);
        float lp = 0f;
        for (int i = 0; i < b.Length; i++)
        {
            float t = (float)i / Rate;
            float cutoff = Mathf.Lerp(brightness, 500f, Mathf.Clamp01(t / (length * 0.8f)));
            lp = OnePole(lp, Noise(), cutoff);
            float env = Mathf.Min(1f, t / 0.008f) * Mathf.Exp(-t / (length * 0.28f));
            b[i] += lp * env * 1.6f * size;
        }
        Tone(b, 0f, 0.15f, 85f, 0.6f * size, 0.06f, Sine);
        int drops = Mathf.RoundToInt(6 * size);
        for (int d = 0; d < drops; d++)
        {
            float at = 0.08f + (float)rng.NextDouble() * (length * 0.6f);
            float f0 = 1400f + (float)rng.NextDouble() * 1600f;
            Chirp(b, at, 0.05f, f0, f0 * 0.55f, 0.18f * size);
        }
        return b;
    }

    // hitting something: a heavy thump, a wooden knock and a splash tail
    private static float[] Crash()
    {
        float[] b = Buffer(0.9f);
        Chirp(b, 0f, 0.35f, 170f, 48f, 1f);
        Tone(b, 0.01f, 0.2f, 230f, 0.35f, 0.04f, Square);
        float[] s = Splash(0.8f, 4500f, 0.8f);
        for (int i = 0; i < s.Length && i + 2000 < b.Length; i++) b[i + 2000] += s[i] * 0.8f;
        return b;
    }

    // four soft descending marimba notes
    private static float[] GameOverJingle()
    {
        float[] b = Buffer(1.6f);
        float[] notes = { 783.99f, 659.25f, 523.25f, 261.63f };
        for (int i = 0; i < notes.Length; i++)
            Tone(b, i * 0.2f, i == notes.Length - 1 ? 0.9f : 0.4f, notes[i], 0.55f, i == notes.Length - 1 ? 0.35f : 0.16f, Marimba);
        return b;
    }

    // the classic "da-dum" warning: two low, growly notes a semitone apart
    private static float[] SharkWarning()
    {
        float[] b = Buffer(0.95f);
        Tone(b, 0f, 0.38f, 82.41f, 0.9f, 0.22f, Growl);
        Tone(b, 0.48f, 0.45f, 87.31f, 1f, 0.25f, Growl);
        float lp = 0f;
        for (int i = 0; i < b.Length; i++) b[i] = lp = OnePole(lp, b[i], 900f);
        return b;
    }

    private static float[] Click()
    {
        float[] b = Buffer(0.06f);
        Tone(b, 0f, 0.06f, 1800f, 0.5f, 0.012f, Sine);
        for (int i = 0; i < 200; i++) b[i] += Noise() * 0.15f * (1f - i / 200f);
        return b;
    }

    // rushing water that loops without a seam: the noise is filtered round the loop twice
    // and its swell repeats a whole number of times per loop
    private static float[] WaterLoop(float seconds)
    {
        int n = Mathf.RoundToInt(seconds * Rate);
        var white = new float[n];
        for (int i = 0; i < n; i++) white[i] = Noise();
        var b = new float[n];
        float lp = 0f, lp2 = 0f;
        for (int pass = 0; pass < 2; pass++)
            for (int i = 0; i < n; i++)
            {
                lp = OnePole(lp, white[i], 1100f);
                lp2 = OnePole(lp2, lp, 2200f);
                b[i] = lp2;
            }
        for (int i = 0; i < n; i++)
        {
            float t = (float)i / n;
            float swell = 0.75f + 0.15f * Mathf.Sin(t * Mathf.PI * 2f * 3f) + 0.1f * Mathf.Sin(t * Mathf.PI * 2f * 7f + 1f);
            b[i] *= swell * 3f;
        }
        return b;
    }

    // ---------------------------------------------------------------- synthesis helpers

    private delegate float Wave(float phase);
    private static float Sine(float p) => Mathf.Sin(p);
    private static float Square(float p) => Mathf.Sign(Mathf.Sin(p)) * 0.6f + Mathf.Sin(p) * 0.4f;
    private static float Bell(float p) => Mathf.Sin(p) + 0.35f * Mathf.Sin(p * 2.76f) + 0.15f * Mathf.Sin(p * 5.4f);
    private static float Marimba(float p) => Mathf.Sin(p) + 0.2f * Mathf.Sin(p * 4f) + 0.08f * Mathf.Sin(p * 9.9f);
    private static float Growl(float p)
    {
        float s = 0f;
        for (int h = 1; h <= 7; h++) s += Mathf.Sin(p * h) / h;
        return s * 0.6f;
    }

    private static float[] Buffer(float seconds) => new float[Mathf.CeilToInt(seconds * Rate)];
    private static float Noise() => (float)(rng.NextDouble() * 2.0 - 1.0);
    private static float OnePole(float y, float x, float cutoff) => y + (1f - Mathf.Exp(-2f * Mathf.PI * cutoff / Rate)) * (x - y);

    // a note with a 3 ms attack and an exponential decay
    private static void Tone(float[] b, float at, float length, float freq, float gain, float decay, Wave wave)
    {
        int start = Mathf.RoundToInt(at * Rate), count = Mathf.RoundToInt(length * Rate);
        for (int i = 0; i < count && start + i < b.Length; i++)
        {
            float t = (float)i / Rate;
            float env = Mathf.Min(1f, t / 0.003f) * Mathf.Exp(-t / decay);
            b[start + i] += wave(2f * Mathf.PI * freq * t) * env * gain;
        }
    }

    // a sine sliding from one pitch to another, fading out
    private static void Chirp(float[] b, float at, float length, float f0, float f1, float gain)
    {
        int start = Mathf.RoundToInt(at * Rate), count = Mathf.RoundToInt(length * Rate);
        double phase = 0;
        for (int i = 0; i < count && start + i < b.Length; i++)
        {
            float k = (float)i / count;
            phase += 2.0 * Math.PI * Mathf.Lerp(f0, f1, k) / Rate;
            float env = Mathf.Min(1f, i / (0.004f * Rate)) * (1f - k) * (1f - k);
            b[start + i] += (float)Math.Sin(phase) * env * gain;
        }
    }

    // state variable filter, for the swept band-pass whoosh
    private class Svf
    {
        private float low, band;
        public float Band(float x, float cutoff, float damping)
        {
            float f = 2f * Mathf.Sin(Mathf.PI * Mathf.Min(cutoff, Rate / 6f) / Rate);
            low += f * band;
            float high = x - low - damping * band;
            band += f * high;
            return band;
        }
    }

    // ---------------------------------------------------------------- WAV

    private static void Write(string name, float[] samples)
    {
        float peak = 0f;
        foreach (float s in samples) peak = Mathf.Max(peak, Mathf.Abs(s));
        float norm = peak > 0f ? 0.89f / peak : 1f;   // about -1 dB
        // tiny fades at both ends so nothing clicks (except the loop, which must stay continuous)
        bool loop = name.EndsWith("Loop");
        int fade = Mathf.Min(64, samples.Length / 4);

        string path = SfxDir + "/" + name + ".wav";
        using (var w = new BinaryWriter(File.Create(path)))
        {
            int bytes = samples.Length * 2;
            w.Write(new[] { 'R', 'I', 'F', 'F' }); w.Write(36 + bytes);
            w.Write(new[] { 'W', 'A', 'V', 'E' });
            w.Write(new[] { 'f', 'm', 't', ' ' }); w.Write(16); w.Write((short)1); w.Write((short)1);
            w.Write(Rate); w.Write(Rate * 2); w.Write((short)2); w.Write((short)16);
            w.Write(new[] { 'd', 'a', 't', 'a' }); w.Write(bytes);
            for (int i = 0; i < samples.Length; i++)
            {
                float s = samples[i] * norm;
                if (!loop)
                {
                    if (i < fade) s *= (float)i / fade;
                    if (i >= samples.Length - fade) s *= (float)(samples.Length - 1 - i) / fade;
                }
                w.Write((short)Mathf.Clamp(Mathf.RoundToInt(s * 32767f), -32768, 32767));
            }
        }
    }

    private static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        EnsureFolder(parent);
        AssetDatabase.CreateFolder(parent, Path.GetFileName(path));
    }
}
