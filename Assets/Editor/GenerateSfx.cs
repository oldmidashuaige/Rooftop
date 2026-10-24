using System;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 编辑器工具：把程序化合成音效烘焙成 WAV 资源到 Resources/Sfx。
/// 之后可整体替换为真实素材（同名文件覆盖即可）。
/// </summary>
public static class GenerateSfx
{
    private const int Freq = 44100;
    private const string Dir = "Assets/Resources/Sfx";

    [MenuItem("Tools/Generate Sfx")]
    public static void Generate()
    {
        Directory.CreateDirectory(Dir);
        AssetDatabase.Refresh();

        Write("shot", ShotData());
        Write("hit", HitData());
        Write("step", StepData());
        Write("runstep", RunStepData());
        Write("jump", JumpData());
        Write("land", LandData());
        Write("hurt", HurtData());

        AssetDatabase.Refresh();
        Debug.Log("[Sfx] 已生成 " + Freq + "Hz WAV 音效到 Resources/Sfx");
    }

    private static void Write(string name, float[] data)
    {
        string path = Dir + "/" + name + ".wav";
        byte[] bytes = EncodeWav(data);
        File.WriteAllBytes(path, bytes);
        Debug.Log("[Sfx] " + name + ".wav  " + (bytes.Length / 1024) + " KB");
    }

    private static byte[] EncodeWav(float[] data)
    {
        int sampleCount = data.Length;
        int dataSize = sampleCount * 2;
        var ms = new MemoryStream(44 + dataSize);
        var w = new BinaryWriter(ms);

        w.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
        w.Write(36 + dataSize);
        w.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));
        w.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
        w.Write(16);                 // fmt chunk size
        w.Write((short)1);           // PCM
        w.Write((short)1);           // mono
        w.Write(Freq);
        w.Write(Freq * 2);           // byte rate
        w.Write((short)2);           // block align
        w.Write((short)16);          // bits per sample
        w.Write(System.Text.Encoding.ASCII.GetBytes("data"));
        w.Write(dataSize);

        for (int i = 0; i < sampleCount; i++)
        {
            short s = (short)Mathf.Clamp(data[i] * 32767f, -32768f, 32767f);
            w.Write(s);
        }
        w.Flush();
        return ms.ToArray();
    }

    private static float[] ShotData() => Synth(0.14f, (rng, i, t) =>
    {
        float env = Mathf.Exp(-t * 34f);
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.8f;
        float thump = Mathf.Sin(2f * Mathf.PI * 130f * t) * Mathf.Exp(-t * 26f) * 1.3f;
        return (noise + thump) * env;
    });

    private static float[] HitData() => Synth(0.07f, (rng, i, t) =>
    {
        float env = Mathf.Exp(-t * 60f);
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * 0.7f;
        float tick = Mathf.Sin(2f * Mathf.PI * 800f * t) * Mathf.Exp(-t * 55f) * 0.5f;
        return (noise + tick) * env;
    });

    private static float[] StepData() => Synth(0.09f, (rng, i, t) =>
    {
        float env = Mathf.Exp(-t * 45f);
        float baseF = i % 2 == 0 ? 95f : 70f;
        return Mathf.Sin(2f * Mathf.PI * baseF * t) * env * 0.9f;
    });

    private static float[] RunStepData() => Synth(0.10f, (rng, i, t) =>
    {
        float env = Mathf.Exp(-t * 40f);
        float baseF = i % 2 == 0 ? 120f : 85f;
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 50f) * 0.3f;
        return Mathf.Sin(2f * Mathf.PI * baseF * t) * env * 0.9f + noise;
    });

    private static float[] JumpData() => Synth(0.08f, (rng, i, t) =>
    {
        float env = Mathf.Exp(-t * 70f);
        float metal = Mathf.Sin(2f * Mathf.PI * 1900f * t) * env * 0.55f;
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * env * 0.18f;
        return metal + noise;
    });

    private static float[] LandData() => Synth(0.20f, (rng, i, t) =>
    {
        float env = Mathf.Exp(-t * 26f);
        float thump = Mathf.Sin(2f * Mathf.PI * 85f * t) * env;
        float thump2 = Mathf.Sin(2f * Mathf.PI * 45f * t) * Mathf.Exp(-t * 14f) * 0.6f;
        return (thump + thump2) * 0.9f;
    });

    private static float[] HurtData() => Synth(0.28f, (rng, i, t) =>
    {
        float env = Mathf.Exp(-t * 8f);
        float f = Mathf.Lerp(220f, 110f, t / 0.28f);
        float tone = Mathf.Sin(2f * Mathf.PI * f * t) * env * 0.8f;
        float noise = (float)(rng.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 13f) * 0.4f;
        return (tone + noise) * 0.8f;
    });

    private static float[] Synth(float seconds, Func<System.Random, int, float, float> sample)
    {
        var rng = new System.Random(nameof(GenerateSfx).GetHashCode() | 1);
        int n = Mathf.CeilToInt(seconds * Freq);
        var data = new float[n];
        for (int i = 0; i < n; i++)
            data[i] = Mathf.Clamp(sample(rng, i, (float)i / Freq), -1f, 1f);
        return data;
    }
}
