using UnityEngine;

public sealed class OwnerAlert : MonoBehaviour
{
    public float countdown = 5f;

    public void TriggerAlert()
    {
        // Session C owns the alert presentation and countdown behavior.
    }
}
