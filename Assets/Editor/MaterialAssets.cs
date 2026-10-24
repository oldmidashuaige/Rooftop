using UnityEditor;
using UnityEngine;

/// <summary>编辑器共享：确保程序化材质资产存在并返回。</summary>
public static class MaterialAssets
{
    public const string MatsFolder = "Assets/Materials";

    public static DemoMaterials Ensure()
    {
        EnsureFolder(MatsFolder);
        return new DemoMaterials
        {
            ground = EnsureMaterial(MatsFolder + "/Ground.mat", new Color(0.32f, 0.34f, 0.30f), 0.05f),
            wall = EnsureMaterial(MatsFolder + "/Wall.mat", new Color(0.30f, 0.30f, 0.28f), 0.1f),
            crate = EnsureMaterial(MatsFolder + "/Crate.mat", new Color(0.45f, 0.31f, 0.18f), 0.1f),
            player = EnsureMaterial(MatsFolder + "/Player.mat", new Color(0.20f, 0.35f, 0.65f), 0.1f),
            gun = EnsureMaterial(MatsFolder + "/Gun.mat", new Color(0.10f, 0.10f, 0.12f), 0.6f),
            zombie = EnsureMaterial(MatsFolder + "/Zombie.mat", new Color(0.30f, 0.48f, 0.24f), 0.1f),
            zombieHead = EnsureMaterial(MatsFolder + "/ZombieHead.mat", new Color(0.42f, 0.58f, 0.32f), 0.1f),
            tracer = EnsureMaterial(MatsFolder + "/Tracer.mat", new Color(1f, 0.9f, 0.4f), 0f),
            spark = EnsureMaterial(MatsFolder + "/Spark.mat", new Color(1f, 0.6f, 0.2f), 0f)
        };
    }

    public static Material EnsureMaterial(string path, Color color, float smoothness)
    {
        var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (mat != null) return mat;

        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");
        mat = new Material(shader);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", color);
        mat.color = color;
        if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", smoothness);
        AssetDatabase.CreateAsset(mat, path);
        return mat;
    }

    public static void EnsureFolder(string path)
    {
        string parent = "Assets";
        string relative = path.StartsWith("Assets/") ? path.Substring("Assets/".Length) : path;
        foreach (var seg in relative.Split('/'))
        {
            string candidate = parent + "/" + seg;
            if (!AssetDatabase.IsValidFolder(candidate))
                AssetDatabase.CreateFolder(parent, seg);
            parent = candidate;
        }
    }
}
