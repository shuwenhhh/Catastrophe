using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager I;

    public int chaosScore = 0;
    // Preserve the existing destruction-feedback conversion: one point = ¥100.
    public long DamageAmount => (long)Mathf.Max(0, chaosScore) * 100L;
    public bool IsFailed { get; private set; }
    public float ElapsedTime => runElapsed;
    public string Grade => ElapsedTime < 90f ? "S" : ElapsedTime < 180f ? "A" : "B";
    public int HighScore => PlayerPrefs.GetInt("Catastrophe.HighScore", 0);

    public readonly struct ChaosEntry
    {
        public readonly string itemName;
        public readonly int points;

        public ChaosEntry(string itemName, int points)
        {
            this.itemName = itemName;
            this.points = points;
        }
    }

    private readonly List<ChaosEntry> receipt = new List<ChaosEntry>();
    private float runElapsed;
    private bool runStarted;
    public IReadOnlyList<ChaosEntry> Receipt => receipt;

    public event Action<int> OnChaosChanged;
    public event Action OnLevelCompleted;
    public event Action OnLevelFailedEvent;

    private void Awake()
    {
        if (I != null && I != this)
        {
            Destroy(gameObject);
            return;
        }

        I = this;
    }

    public void AddChaos(int basePoints)
    {
        AddChaos(basePoints, "Chaos");
    }

    public void BeginRun()
    {
        chaosScore = 0;
        receipt.Clear();
        runElapsed = 0f;
        runStarted = true;
        IsFailed = false;
        OnChaosChanged?.Invoke(chaosScore);
    }

    public void RestoreRun(int score, float elapsed, List<GameSaveStore.ReceiptItem> entries)
    {
        chaosScore = Mathf.Max(0, score);
        runElapsed = Mathf.Max(0, elapsed);
        runStarted = true;
        IsFailed = false;
        receipt.Clear();
        foreach (var entry in entries) receipt.Add(new ChaosEntry(entry.name, entry.points));
        OnChaosChanged?.Invoke(chaosScore);
    }

    private void Update()
    {
        if (runStarted && GameFlowUI.GameplayInputEnabled)
            runElapsed += Time.deltaTime;
    }

    public void AddChaos(int basePoints, string itemName)
    {
        if (basePoints <= 0)
            return;

        chaosScore += basePoints;
        receipt.Add(new ChaosEntry(string.IsNullOrWhiteSpace(itemName) ? "Item" : itemName, basePoints));
        OnChaosChanged?.Invoke(chaosScore);
    }

    public void OnCaught()
    {
        if (IsFailed)
            return;

        IsFailed = true;
        SaveHighScore();
        OnLevelFailed();
    }

    public void OnLevelComplete()
    {
        SaveHighScore();
        OnLevelCompleted?.Invoke();
    }

    private void SaveHighScore()
    {
        if (chaosScore <= HighScore) return;
        PlayerPrefs.SetInt("Catastrophe.HighScore", chaosScore);
        PlayerPrefs.Save();
    }

    public void OnLevelFailed()
    {
        OnLevelFailedEvent?.Invoke();
    }

    private void OnDestroy()
    {
        if (I == this)
            I = null;
    }
}
