using UnityEngine;
using UnityEditor;
using System.IO;

/// <summary>
/// CATASTROPHE! 一键应用房间布局
/// 读取 Assets/Editor/RoomLayoutConfig.txt，按配置移动家具。
/// 使用: CATASTROPHE -> Apply Room Layout
/// 以后调布局只改 txt 文件，不用改代码。
/// </summary>
public static class ApplyRoomLayout
{
    [MenuItem("CATASTROPHE/Apply Room Layout")]
    public static void Run()
    {
        string cfgPath = Path.Combine(Application.dataPath, "Editor/RoomLayoutConfig.txt");
        if (!File.Exists(cfgPath))
        {
            EditorUtility.DisplayDialog("找不到配置", "没找到 Assets/Editor/RoomLayoutConfig.txt", "好");
            return;
        }

        int moved = 0, missing = 0;
        foreach (var rawLine in File.ReadAllLines(cfgPath))
        {
            string line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith("#")) continue;

            // 格式: 物体名 | x | z | 朝向 | y(可选)
            var parts = line.Split('|');
            if (parts.Length < 4)
            {
                Debug.LogWarning($"[ApplyRoomLayout] 格式不对，跳过: {line}");
                continue;
            }
            string name = parts[0].Trim();
            if (!float.TryParse(parts[1].Trim(), out float x) ||
                !float.TryParse(parts[2].Trim(), out float z) ||
                !float.TryParse(parts[3].Trim(), out float rotY))
            {
                Debug.LogWarning($"[ApplyRoomLayout] 数字解析失败，跳过: {line}");
                continue;
            }
            float y = 0f;
            if (parts.Length >= 5) float.TryParse(parts[4].Trim(), out y);

            var go = GameObject.Find(name);
            if (go == null)
            {
                Debug.LogWarning($"[ApplyRoomLayout] 未找到 '{name}'，跳过。");
                missing++;
                continue;
            }
            Undo.RecordObject(go.transform, "Apply Room Layout");
            go.transform.position = new Vector3(x, y, z);
            go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
            Debug.Log($"[ApplyRoomLayout] {name} -> ({x}, {y}, {z}) rotY={rotY}");
            moved++;
        }

        Debug.Log($"[ApplyRoomLayout] 完成: 移动 {moved} 个, 未找到 {missing} 个。");
        EditorUtility.DisplayDialog("布局应用完成",
            $"移动了 {moved} 个，未找到 {missing} 个。\n检查 Scene，满意后 Cmd+S 保存。",
            "好");
    }
}
