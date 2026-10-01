using System;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class GameManager : MonoBehaviour
{
    public static GameManager I;

    public int chaosScore = 0;

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
        if (basePoints <= 0)
            return;

        chaosScore += basePoints;
        OnChaosChanged?.Invoke(chaosScore);
    }

    public void OnCaught()
    {
        OnLevelFailed();
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    public void OnLevelComplete()
    {
        OnLevelCompleted?.Invoke();
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
