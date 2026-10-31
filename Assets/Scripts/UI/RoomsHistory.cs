using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>历史房间记录：持久化到 persistentDataPath/rooms_history.json。</summary>
[Serializable]
public class RoomRecord
{
    public string name = "";
    public string ip = "";
    public string port = "";
    public string time = "";
}

public static class RoomsHistory
{
    private static readonly List<RoomRecord> records = new List<RoomRecord>();
    private static bool loaded;

    [Serializable]
    private class Wrapper { public List<RoomRecord> records; }

    private static string FilePath => System.IO.Path.Combine(Application.persistentDataPath, "rooms_history.json");

    public static IReadOnlyList<RoomRecord> Records
    {
        get { Load(); return records; }
    }

    public static void Add(string name, string ip, string port)
    {
        Load();
        records.Insert(0, new RoomRecord
        {
            name = string.IsNullOrEmpty(name) ? ip : name,
            ip = ip,
            port = port,
            time = DateTime.Now.ToString("MM-dd HH:mm")
        });
        if (records.Count > 30) records.RemoveAt(records.Count - 1);
        Save();
    }

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        try
        {
            if (File.Exists(FilePath))
            {
                var wrap = JsonUtility.FromJson<Wrapper>(File.ReadAllText(FilePath));
                if (wrap != null && wrap.records != null)
                {
                    records.Clear();
                    records.AddRange(wrap.records);
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogWarning("[History] 读取历史房间失败: " + e.Message);
        }
    }

    public static void Save()
    {
        try
        {
            File.WriteAllText(FilePath, JsonUtility.ToJson(new Wrapper { records = records }));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[History] 保存历史房间失败: " + e.Message);
        }
    }
}
