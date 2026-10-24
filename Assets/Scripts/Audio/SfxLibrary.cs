using UnityEngine;

/// <summary>
/// 音效库：从 Resources/Sfx 加载烘焙好的 WAV（由编辑器工具 GenerateSfx 生成）。
/// 全部音效为程序化合成，后期可整体替换为真实素材（同名文件替换即可）。
/// </summary>
public static class SfxLibrary
{
    public const int Freq = 44100;

    private static AudioClip shotClip;
    private static AudioClip hitClip;
    private static AudioClip stepClip;
    private static AudioClip runStepClip;
    private static AudioClip jumpClip;
    private static AudioClip landClip;
    private static AudioClip hurtClip;

    public static AudioClip Shot => shotClip ?? (shotClip = Load("Sfx/shot"));
    public static AudioClip Hit => hitClip ?? (hitClip = Load("Sfx/hit"));
    public static AudioClip Step => stepClip ?? (stepClip = Load("Sfx/step"));
    public static AudioClip RunStep => runStepClip ?? (runStepClip = Load("Sfx/runstep"));
    public static AudioClip Jump => jumpClip ?? (jumpClip = Load("Sfx/jump"));
    public static AudioClip Land => landClip ?? (landClip = Load("Sfx/land"));
    public static AudioClip Hurt => hurtClip ?? (hurtClip = Load("Sfx/hurt"));

    public static AudioSource EnsureSource(GameObject go)
    {
        var src = go.GetComponent<AudioSource>();
        if (src == null) src = go.AddComponent<AudioSource>();
        src.playOnAwake = false;
        src.spatialBlend = 0f;
        return src;
    }

    private static AudioClip Load(string res)
    {
        var clip = Resources.Load<AudioClip>(res);
        if (clip == null)
            Debug.LogWarning("[Sfx] 缺少音效资源: Resources/" + res + "（请运行 Tools/Generate Sfx）");
        return clip;
    }
}
