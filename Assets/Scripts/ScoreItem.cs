using UnityEngine;

public sealed class ScoreItem : MonoBehaviour
{
    public int Points { get; set; }
    public string DisplayName { get; set; }

    private bool scored;

    private void Update()
    {
        if (scored || transform.position.y > 0.22f)
            return;

        scored = true;
        GameBootstrap.Instance?.AddScore(Points, DisplayName);
    }
}
