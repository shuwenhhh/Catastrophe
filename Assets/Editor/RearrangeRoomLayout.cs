using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// CATASTROPHE! 房间布局重排 v2
/// 修复: 正确名字(含空格), 书架物品一起搬, 不碰灯
/// </summary>
public static class RearrangeRoomLayout
{
    [MenuItem("CATASTROPHE/Rearrange Room Layout v2")]
    public static void Run()
    {
        int moved = 0, missing = 0;

        // 后墙: 沙发 x=-2.0, z=+3.2, 朝 -Z
        if (TryMove("Tripo Sofa", new Vector3(-2.0f, 0f, 3.2f), 180f)) moved++; else missing++;

        // 后墙: 纸箱 (沙发旁边) x=-3.8, z=+2.6
        if (TryMove("Tripo Hide Cardboard", new Vector3(-3.8f, 0f, 2.6f), 0f)) moved++;
        else if (TryMove("Hide Cardboa", new Vector3(-3.8f, 0f, 2.6f), 0f)) moved++;
        else missing++;

        // 左墙: 书架 + 上面所有东西一起搬
        // 目标: x=-4.5, z=+0.5, 朝 +X
        if (MoveWithChildren("Tripo Shelving Unit", new Vector3(-4.5f, 0f, 0.5f), 90f,
            new string[] { "Shelf Mug", "Shelf Books", "Shelf Cup", "Shelf Plant", "Climbable Shelf Platform", "Shelf frame collision" }))
            moved++;
        else missing++;

        // 左墙: 门 (书架旁边) x=-4.5, z=-1.8
        if (TryMove("Door", new Vector3(-4.5f, 0f, -1.8f), 90f)) moved++; else missing++;

        // 前墙: 电视柜 (正对沙发) x=-2.0, z=-3.2
        if (TryMove("Tripo Tv Cabinet", new Vector3(-2.0f, 0f, -3.2f), 0f)) moved++; else missing++;

        // 前墙: 电视 (电视柜上) x=-2.0, z=-3.15
        if (TryMove("Tripo Television", new Vector3(-2.0f, 0.65f, -3.15f), 0f)) moved++; else missing++;

        // 后墙: 窗帘 (沙发右边) x=+1.5, z=+3.4
        if (TryMove("Tripo Curtain", new Vector3(1.5f, 0f, 3.4f), 180f)) moved++; else missing++;

        // 注意: 不碰 Window, 避免误伤灯光

        Debug.Log($"[RearrangeRoomLayout v2] 完成: 移动 {moved} 个, 未找到 {missing} 个。");
        EditorUtility.DisplayDialog("布局重排 v2 完成",
            $"移动了 {moved} 个物体，未找到 {missing} 个。\n请在 Scene 视图检查，确认后 Cmd+S 保存。",
            "好");
    }

    static bool TryMove(string exactName, Vector3 pos, float rotY)
    {
        var go = GameObject.Find(exactName);
        if (go == null)
        {
            // 模糊匹配
            go = FindByKeyword(exactName.Replace("Tripo ", "").Replace(" ", ""));
        }
        if (go == null)
        {
            Debug.LogWarning($"[RearrangeRoomLayout] 未找到 '{exactName}'，跳过。");
            return false;
        }
        Undo.RecordObject(go.transform, "Rearrange Room");
        go.transform.position = pos;
        go.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        Debug.Log($"[RearrangeRoomLayout] {go.name} -> {pos}, rotY={rotY}");
        return true;
    }

    /// <summary>
    /// 移动主物体，并把相关小物件按相同位移一起搬。
    /// </summary>
    static bool MoveWithChildren(string mainName, Vector3 newPos, float rotY, string[] childKeywords)
    {
        var main = GameObject.Find(mainName);
        if (main == null)
        {
            Debug.LogWarning($"[RearrangeRoomLayout] 未找到 '{mainName}'，跳过。");
            return false;
        }
        Vector3 delta = newPos - main.transform.position;
        Undo.RecordObject(main.transform, "Rearrange Room");
        main.transform.position = newPos;
        main.transform.rotation = Quaternion.Euler(0f, rotY, 0f);
        Debug.Log($"[RearrangeRoomLayout] {main.name} -> {newPos}, 位移 delta={delta}");

        // 相关小物件一起搬
        int childMoved = 0;
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (!go.scene.IsValid() || go == main) continue;
            foreach (var kw in childKeywords)
            {
                if (go.name.Contains(kw))
                {
                    Undo.RecordObject(go.transform, "Rearrange Room");
                    go.transform.position += delta;
                    childMoved++;
                    Debug.Log($"[RearrangeRoomLayout]   关联: {go.name} 跟随移动");
                    break;
                }
            }
        }
        Debug.Log($"[RearrangeRoomLayout] {main.name} 关联移动了 {childMoved} 个小物件。");
        return true;
    }

    static GameObject FindByKeyword(string keyword)
    {
        string kw = keyword.ToLower().Replace(" ", "");
        foreach (var go in Resources.FindObjectsOfTypeAll<GameObject>())
        {
            if (go.scene.IsValid() && go.name.ToLower().Replace(" ", "").Contains(kw))
                return go;
        }
        return null;
    }
}
