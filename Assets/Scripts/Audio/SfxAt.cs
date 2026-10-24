using UnityEngine;

/// <summary>客户端：在世界坐标播一个 3D 定位音效（临时 AudioSource）。</summary>
public static class SfxAt
{
    public static void Play3D(Vector3 pos, AudioClip clip, float volume)
    {
        if (clip == null) return;
        var go = new GameObject("Sfx3D");
        go.transform.position = pos;
        var src = go.AddComponent<AudioSource>();
        src.spatialBlend = 1f;
        src.rolloffMode = AudioRolloffMode.Linear;
        src.minDistance = 3f;
        src.maxDistance = 50f;
        src.PlayOneShot(clip, volume);
        Object.Destroy(go, clip.length + 0.5f);
    }
}
