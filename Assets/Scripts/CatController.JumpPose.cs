using UnityEngine;

public sealed partial class CatController
{
    // Pose time follows the flight rather than a fixed animation duration.
    // The physical controller remains the sole owner of world displacement.
    private void UpdateRefinedJumpPose()
    {
        if (!hasRefinedJump) return;
        if (isJumping)
        {
            float elapsed = Time.time - jumpStartedAt;
            float desired;
            if (elapsed < .10f)
                desired = Mathf.Lerp(0f, .23f, elapsed / .10f);
            else if (body.linearVelocity.y > 0f)
                desired = Mathf.Lerp(.23f, .48f,
                    1f - Mathf.Clamp01(body.linearVelocity.y / Mathf.Max(.01f, jumpLaunchSpeed)));
            else
                desired = Mathf.Lerp(.48f, .79f, Mathf.Clamp01(-body.linearVelocity.y / 2.5f));
            jumpPosePhase = Mathf.Max(jumpPosePhase, desired);
        }
        else if (!float.IsNegativeInfinity(jumpLandingAt))
            jumpPosePhase = Mathf.Lerp(.80f, 1f, (Time.time - jumpLandingAt) / .18f);
        else
            jumpPosePhase = 1f;
        animator.SetFloat("JumpPhase", jumpPosePhase);
    }
}
