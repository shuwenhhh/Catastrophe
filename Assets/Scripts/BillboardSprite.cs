using UnityEngine;

public sealed class BillboardSprite : MonoBehaviour
{
    private void LateUpdate()
    {
        Camera gameCamera = Camera.main;
        if (gameCamera == null)
            return;

        Vector3 toCamera = gameCamera.transform.position - transform.position;
        toCamera.y = 0f;
        if (toCamera.sqrMagnitude > 0.001f)
            transform.rotation = Quaternion.LookRotation(toCamera.normalized, Vector3.up);
    }
}
