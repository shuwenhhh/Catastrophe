using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

// A single local resume slot plus a bounded history. Uses explicit gameplay
// state: restoring a fallen prop must not award its points a second time.
public static class GameSaveStore
{
    public const string SaveKey = "Catastrophe.Resume.v1";
    public const string HistoryKey = "Catastrophe.History.v1";
    [Serializable] public class ReceiptItem { public string name; public int points; }
    [Serializable] public class PropState
    {
        public string key; public Vector3 position, velocity, angularVelocity;
        public Quaternion rotation; public bool destroyed, armed, kinematic; public float support;
    }
    [Serializable] public class Snapshot
    {
        public int version = 1, score, target;
        public string scene, date, hideName;
        public float elapsed, noise, alertRemaining, yaw, pitch;
        // Optional fields preserve v1 compatibility; old alerts migrate to 30s.
        public bool hasPatrolState, patrolInspecting;
        public float patrolRemaining;
        public bool hasNoiseSearchState;
        public int noiseSearchPhase;
        public Vector3 position, velocity; public Quaternion rotation;
        public List<PropState> props = new();
        public List<ReceiptItem> receipt = new();
        public List<string> cups = new();
    }
    [Serializable] public class Record { public string date, result; public int score; public float seconds; }
    [Serializable] public class History { public List<Record> records = new(); }

    public static string ObjectKey(Transform part)
    {
        string key = part.name + "[" + part.GetSiblingIndex() + "]";
        for (var t = part.parent; t; t = t.parent) key = t.name + "[" + t.GetSiblingIndex() + "]/" + key;
        return key;
    }
    public static Snapshot Read()
    {
        try
        {
            var data = JsonUtility.FromJson<Snapshot>(PlayerPrefs.GetString(SaveKey, ""));
            return data != null && data.version == 1 && data.scene == SceneManager.GetActiveScene().path &&
                data.props != null && data.receipt != null && data.cups != null ? data : null;
        }
        catch (Exception) { return null; }
    }
    public static History ReadHistory()
    {
        try { var h = JsonUtility.FromJson<History>(PlayerPrefs.GetString(HistoryKey, "")); return h?.records != null ? h : new History(); }
        catch (Exception) { return new History(); }
    }
    public static bool Capture()
    {
        var cat = UnityEngine.Object.FindAnyObjectByType<CatController>();
        if (!cat || !GameManager.I || GameManager.I.IsFailed || TargetManager.I?.IsLevelComplete == true) return false;
        var body = cat.GetComponent<Rigidbody>();
        var camera = UnityEngine.Object.FindAnyObjectByType<ThirdPersonCamera>();
        var data = new Snapshot {
            scene = SceneManager.GetActiveScene().path, date = DateTime.Now.ToString("yyyy.MM.dd HH:mm"),
            score = GameManager.I.chaosScore, elapsed = GameManager.I.ElapsedTime,
            target = TargetManager.I ? TargetManager.I.SavedTargetIndex : 0,
            cups = TargetManager.I ? TargetManager.I.SavedCups : new List<string>(),
            noise = NoiseManager.I ? NoiseManager.I.noise : 0f,
            alertRemaining = -1f,
            hasPatrolState = OwnerAlert.I && OwnerAlert.I.IsRunning,
            patrolInspecting = OwnerAlert.I && OwnerAlert.I.IsInspecting,
            patrolRemaining = OwnerAlert.I ? OwnerAlert.I.SecondsRemaining : OwnerAlert.PatrolInterval,
            hasNoiseSearchState = true,
            noiseSearchPhase = OwnerAlert.I ? (int)OwnerAlert.I.CurrentPhase : 0,
            position = body.position, rotation = body.rotation, velocity = body.linearVelocity,
            hideName = cat.SavedHideName, yaw = camera ? camera.OrbitYaw : 0f, pitch = camera ? camera.OrbitPitch : 32f
        };
        foreach (var prop in UnityEngine.Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None)) data.props.Add(prop.CaptureSave());
        foreach (var item in GameManager.I.Receipt) data.receipt.Add(new ReceiptItem { name = item.itemName, points = item.points });
        PlayerPrefs.SetString(SaveKey, JsonUtility.ToJson(data)); PlayerPrefs.Save();
        return true;
    }
    public static bool Restore()
    {
        var data = Read();
        var cat = UnityEngine.Object.FindAnyObjectByType<CatController>();
        if (data == null || !cat || !GameManager.I) return false;
        var props = new Dictionary<string, Valuable>();
        foreach (var prop in UnityEngine.Object.FindObjectsByType<Valuable>(FindObjectsSortMode.None)) props[ObjectKey(prop.transform)] = prop;
        // Refuse incompatible scene content before changing anything.
        foreach (var saved in data.props) if (!props.ContainsKey(saved.key)) return false;
        foreach (var saved in data.props) props[saved.key].RestoreSave(saved);
        if (cat.IsHidden) cat.ExitHide();
        var body = cat.GetComponent<Rigidbody>();
        body.position = data.position; body.rotation = data.rotation; body.linearVelocity = data.velocity;
        cat.transform.SetPositionAndRotation(data.position, data.rotation);
        GameManager.I.RestoreRun(data.score, data.elapsed, data.receipt);
        TargetManager.I?.RestoreProgress(data.target, data.cups);
        NoiseManager.I?.RestoreNoise(data.noise);
        Physics.SyncTransforms();
        if (!string.IsNullOrEmpty(data.hideName)) cat.EnterHide(GameObject.Find(data.hideName));
        UnityEngine.Object.FindAnyObjectByType<ThirdPersonCamera>()?.RestoreView(data.yaw, data.pitch);
        if (OwnerAlert.I)
        {
            if (data.hasNoiseSearchState)
                OwnerAlert.I.RestoreState((OwnerAlert.Phase)data.noiseSearchPhase, data.patrolRemaining);
            else if (data.hasPatrolState && data.patrolInspecting)
                OwnerAlert.I.RestoreState(OwnerAlert.Phase.Inspecting, data.patrolRemaining);
            else OwnerAlert.I.RestoreState(OwnerAlert.Phase.Idle, 0f);
        }
        return true;
    }
    public static void Archive(string result, int score, float seconds)
    {
        var history = ReadHistory();
        history.records.Insert(0, new Record { date = DateTime.Now.ToString("yyyy.MM.dd HH:mm"), result = result, score = score, seconds = seconds });
        if (history.records.Count > 10) history.records.RemoveRange(10, history.records.Count - 10);
        PlayerPrefs.SetString(HistoryKey, JsonUtility.ToJson(history)); PlayerPrefs.Save();
    }
    public static void ClearResume() { PlayerPrefs.DeleteKey(SaveKey); PlayerPrefs.Save(); }
}
