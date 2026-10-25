using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public sealed partial class CatController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 0.50f;
    [SerializeField, Min(0.05f)] private float plantedPawTravelLocal = 0.20f;
    [SerializeField, Range(0.3f, 0.8f)] private float walkStanceFraction = 0.64f;
    [SerializeField, Min(0.1f)] private float walkCyclesPerSecond = 3.1f;
    [SerializeField] private float jumpHeight = 1.40f;
    [SerializeField] private float shortJumpHeight = 0.50f;
    [SerializeField] private float shortJumpReleaseWindow = 0.22f;
    [SerializeField] private float jumpMoveSpeed = 1.8f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float fallGravityMultiplier = 2.1f;
    [SerializeField] private float pushReach = 0.65f;
    [SerializeField] private float frontVisualScale = 0.75f;
    [SerializeField] private float backVisualScale = 0.74f;
    [SerializeField] private float pushVisualScale = 0.67f;
    [SerializeField] private float groundedFootLocalY = -0.225f;
    [SerializeField] private float footstepDistance = 0.40f;
    [SerializeField] private float cuteVisualFootOffsetY = -0.11f;
    [SerializeField, Min(0.1f)] private float walkCycleDistance = 0.70f;

    private Rigidbody body;
    private CatSolidCollision solidCollision;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private SkinnedMeshRenderer[] skinnedRenderers;
    private CuteCatVisual cuteVisual;
    private CatContactShadow contactShadow;
    private Vector3 baseSpriteScale;
    private Vector3 baseSpriteLocalPosition;
    private bool hasBaseSpriteTransform;
    private Vector2 moveInput;
    private Vector2 visualDirection = Vector2.right;
    private Vector3 lastFacingDirection = Vector3.right;
    private Vector3 moveDirection;
    private Vector2 previousMoveInput;
    private Quaternion movementView = Quaternion.identity;
    private bool pushRequested;
    private bool isPushing;
    private bool isJumping;
    private float jumpStartedAt;
    private float jumpTakeoffFootY;
    private float jumpLaunchSpeed;
    private float jumpLandingAt = float.NegativeInfinity;
    private float jumpPosePhase;
    private bool hasRefinedJump;
    private BoxCollider[] shelfPlatforms;
    private float lastGroundedAt = float.NegativeInfinity;
    private float lastJumpPressedAt = float.NegativeInfinity;
    private bool jumpHeld;
    private bool jumpCutPending;
    private bool jumpCutApplied;
    private int currentVisualState;
    private bool isMovingVisual;
    private GameObject activeHideSpot;
    private HideSpot nearbyHideSpot;
    private RigidbodyConstraints movementConstraints;
    private Vector3 lastFootstepPosition;
    private float footstepDistanceTravelled;
    private Vector3 lastGaitPosition;
    private float gaitPhase;
    private float measuredGroundSpeed;
    private float walkClipDuration = 2f;
    private bool wasGaitGrounded;

    public bool IsHidden { get; private set; }
    public bool CanHideNearby => nearbyHideSpot != null;
    public string SavedHideName => IsHidden && activeHideSpot ? activeHideSpot.name : string.Empty;

    // Read-only feedback from an accepted jump, not an independent UI timer.
    // Full means the early-release window is over; it does not add jump height.
    public bool JumpHoldFeedbackActive => GameFlowUI.GameplayInputEnabled &&
        isJumping && jumpHeld && !jumpCutApplied && !IsHidden;
    public float JumpHoldProgress => JumpHoldFeedbackActive
        ? Mathf.Clamp01((Time.time - jumpStartedAt) /
            Mathf.Max(.02f, shortJumpReleaseWindow + Time.fixedDeltaTime)) : 0f;

    public void EnterHide(GameObject hideSpot)
    {
        if (hideSpot == null || IsHidden)
            return;

        var anchor = hideSpot.GetComponent<HideSpot>()?.HideAnchor;
        if (anchor != null && solidCollision != null && !solidCollision.CanOccupy(anchor.position, anchor.rotation)) return;
        activeHideSpot = hideSpot;
        IsHidden = true;
        movementConstraints = body.constraints;
        body.linearVelocity = Vector3.zero;
        body.constraints = RigidbodyConstraints.FreezeAll;
        // Move the cat inside the hide spot (e.g. into the cardboard box)
        body.position = anchor != null ? anchor.position : hideSpot.transform.position + Vector3.up * 0.05f;
        if (anchor != null) body.rotation = anchor.rotation;
        transform.SetPositionAndRotation(body.position, body.rotation);
        SetHiddenVisual(false); // Hiding is physical occlusion, never an invisible cat.
    }

    public void ExitHide()
    {
        if (!IsHidden)
            return;

        // Step out in front of the hide spot
        if (activeHideSpot != null)
        {
            var anchor = activeHideSpot.GetComponent<HideSpot>()?.ExitAnchor;
            if (anchor != null && solidCollision != null && !solidCollision.CanOccupy(anchor.position, anchor.rotation)) return;
            Vector3 exitPos = activeHideSpot.transform.position + activeHideSpot.transform.forward * 0.7f;
            exitPos.y = activeHideSpot.transform.position.y + 0.05f;
            body.position = anchor != null ? anchor.position : exitPos;
            if (anchor != null) body.rotation = anchor.rotation;
            transform.SetPositionAndRotation(body.position, body.rotation);
        }
        activeHideSpot = null;
        IsHidden = false;
        body.constraints = movementConstraints;
        SetHiddenVisual(false);
    }

    public void SetNearbyHideSpot(HideSpot hideSpot)
    {
        nearbyHideSpot = hideSpot;
    }

    public void ClearNearbyHideSpot(HideSpot hideSpot)
    {
        if (nearbyHideSpot == hideSpot)
            nearbyHideSpot = null;
    }

    private void SetHiddenVisual(bool hidden)
    {
        if (spriteRenderer == null && cuteVisual == null)
            CacheVisualComponents();
        // Cat is physically inside the hide spot; hide the model instead of ghosting it.
        if (spriteRenderer != null)
            spriteRenderer.enabled = !hidden;
        if (skinnedRenderers != null)
            foreach (SkinnedMeshRenderer renderer in skinnedRenderers)
                renderer.enabled = !hidden;
        if (cuteVisual != null)
            cuteVisual.gameObject.SetActive(!hidden);
    }


    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        solidCollision = GetComponent<CatSolidCollision>();
        lastFootstepPosition = transform.position;
        lastGaitPosition = body.position;
    }

    private void Start()
    {
        CacheVisualComponents();
        if (animator != null && spriteRenderer == null)
        {
            // The physics speed is set by the distance a planted paw covers in
            // one cycle, not tuned independently from the animation.
            float worldStride = plantedPawTravelLocal / walkStanceFraction *
                                Mathf.Abs(animator.transform.lossyScale.x);
            moveSpeed = worldStride * walkCyclesPerSecond;
            if (animator.runtimeAnimatorController != null)
                foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                    if (clip.name.EndsWith("Cat_Walk")) { walkClipDuration = clip.length; break; }
        }
        GameObject shelf = GameObject.Find("Tripo Shelving Unit");
        shelfPlatforms = shelf == null ? System.Array.Empty<BoxCollider>() :
            shelf.GetComponentsInChildren<BoxCollider>(true)
                .Where(c => c.enabled && c.name.StartsWith("Climbable Shelf Platform"))
                .OrderBy(c => c.bounds.max.y).ToArray();
    }

    private void CacheVisualComponents()
    {
        animator = GetComponentInChildren<Animator>();
        hasRefinedJump = animator != null && animator.parameters.Any(p => p.name == "JumpPhase");
        // The contact shadow also uses a SpriteRenderer; it is not the cat visual.
        spriteRenderer = animator != null ? animator.GetComponentInChildren<SpriteRenderer>() : null;
        skinnedRenderers = GetComponentsInChildren<SkinnedMeshRenderer>(true);
        cuteVisual = GetComponent<CuteCatVisual>();
        contactShadow = GetComponent<CatContactShadow>();
        if (spriteRenderer != null && !hasBaseSpriteTransform)
        {
            baseSpriteScale = spriteRenderer.transform.localScale;
            baseSpriteLocalPosition = spriteRenderer.transform.localPosition;
            hasBaseSpriteTransform = true;
            ApplyVisualScale(1f);
        }
    }

    private void Update()
    {
        if (!GameFlowUI.GameplayInputEnabled)
        {
            moveInput = Vector2.zero;
            previousMoveInput = Vector2.zero;
            moveDirection = Vector3.zero;
            if (animator != null) animator.SetFloat("Speed", 0f);
            return;
        }
        if (animator == null)
            CacheVisualComponents();

        MobileCatControls mobile = MobileCatControls.Instance;
        moveInput = ReadMovement();
        // Use the player's live orbit direction: a held stick still points
        // up-screen while a second finger looks around.
        if (moveInput.sqrMagnitude > .02f)
        {
            Camera view = Camera.main;
            if(view != null)
            {
                var orbit=view.GetComponent<ThirdPersonCamera>();
                movementView=orbit&&orbit.enabled?orbit.PlanarRotation:view.transform.rotation;
            }
        }
        moveDirection = CameraRelativeDirection(moveInput, movementView);
        previousMoveInput = moveInput;
        if ((Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame) ||
            (mobile != null && mobile.ConsumeJumpPressed()))
        {
            lastJumpPressedAt = Time.time;
            jumpCutPending = false;
        }
        jumpHeld = (Keyboard.current != null && Keyboard.current.spaceKey.isPressed) ||
                   (mobile != null && mobile.JumpHeld);
        if ((Keyboard.current != null && Keyboard.current.spaceKey.wasReleasedThisFrame) ||
            (mobile != null && mobile.ConsumeJumpReleased()))
            jumpCutPending = true;
        bool interactionPressed = (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) ||
                                  (mobile != null && mobile.ConsumeActionPressed());
        if (interactionPressed && IsHidden)
            ExitHide();
        else if (interactionPressed && nearbyHideSpot != null)
            EnterHide(nearbyHideSpot.gameObject);
        else
            pushRequested |= Keyboard.current != null &&
                             (interactionPressed || Keyboard.current.fKey.wasPressedThisFrame);

        if (IsHidden)
        {
            moveInput = Vector2.zero;
            previousMoveInput = Vector2.zero;
            moveDirection = Vector3.zero;
        }

        if (animator != null)
        {
            Vector3 horizontalVelocity = body.linearVelocity;
            horizontalVelocity.y = 0f;
            float normalizedSpeed = moveSpeed > 0.001f
                ? Mathf.Clamp01(horizontalVelocity.magnitude / moveSpeed)
                : 0f;
            // No input: snap Speed to 0 so Walk -> Idle triggers immediately
            // instead of lingering on the last walk frame.
            if (moveInput.sqrMagnitude <= 0.02f)
                animator.SetFloat("Speed", 0f);
            else
                animator.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
            UpdateVisualDirection();
            if (spriteRenderer != null)
            {
                animator.SetFloat("DirectionX", visualDirection.x);
                animator.SetFloat("DirectionY", visualDirection.y);
            }
            bool isGrounded = IsGrounded();
            animator.SetBool("Grounded", isGrounded);
            if (spriteRenderer == null)
            {
                // Match one complete walk cycle to its planted-paw travel, even
                // when an analog stick or furniture contact slows the cat.
                float scale = animator.transform.lossyScale.x;
                float cycleDistance = plantedPawTravelLocal / walkStanceFraction * Mathf.Abs(scale);
                animator.SetFloat("WalkRate", horizontalVelocity.magnitude * walkClipDuration / Mathf.Max(.01f, cycleDistance));
            }
            if (isJumping)
            {
                bool landed = isGrounded && Time.time - jumpStartedAt > 0.12f &&
                              body.linearVelocity.y <= 0.1f;
                if (landed)
                {
                    isJumping = false;
                    jumpLandingAt = Time.time;
                    currentVisualState = 0;
                    AudioManager.I?.PlayLanding();
                    if (!hasRefinedJump) cuteVisual?.PlayLandingSquash();
                }
            }
            UpdateRefinedJumpPose();

            if (isJumping || isPushing)
                return;

            // Input is authoritative for the visual stop. Waiting for the next
            // physics tick can leave the last walk sprite (often a raised paw)
            // visible after the player has already released the key.
            isMovingVisual = moveInput.sqrMagnitude > 0.02f && measuredGroundSpeed > 0.04f && isGrounded;
            if (spriteRenderer != null)
                UpdateAnimationState(isMovingVisual);
        }

        if (pushRequested && !isPushing)
        {
            pushRequested = false;
            StartCoroutine(Push());
        }
    }

    private void LateUpdate()
    {
        // 3D model (CuteCatVisual): align feet to ground via tunable offset.
        if (cuteVisual != null && spriteRenderer == null)
        {
            Vector3 vp = cuteVisual.transform.localPosition;
            vp.y = cuteVisualFootOffsetY;
            cuteVisual.transform.localPosition = vp;
            return;
        }
        if (spriteRenderer == null)
            return;

        if (isPushing)
        {
            ApplyVisualScale(pushVisualScale);
            AlignGroundedFeet(pushVisualScale);
            return;
        }

        float directionScale = DirectionVisualScale();
        ApplyVisualScale(directionScale);
        if (!isJumping && IsGrounded())
            AlignGroundedFeet(directionScale);
    }

    private void FixedUpdate()
    {
        if (IsHidden || body.isKinematic) return;
        body.angularVelocity = Vector3.zero;
        bool grounded = IsGrounded();
        UpdateGait(grounded);
        if (grounded)
            lastGroundedAt = Time.time;

        Vector3 velocity = body.linearVelocity;
        float travelSpeed = isJumping ? jumpMoveSpeed : moveSpeed;
        // After clearing a thin shelf lip, cross it before the descending paws
        // catch the edge. This local air-control assist does not change walking
        // speed or the height of either jump.
        if(isJumping&&shelfPlatforms!=null)
            foreach(var platform in shelfPlatforms)
            {
                var b=platform.bounds;var p=new Vector3(body.position.x,b.center.y,body.position.z);
                if(Vector3.Distance(p,b.ClosestPoint(p))<.65f&&body.position.y-.225f>b.max.y+.02f)
                    travelSpeed=Mathf.Max(travelSpeed,3.6f);
            }
        Vector3 travel = Vector3.zero;
        if (moveDirection.sqrMagnitude > .02f)
        {
            Quaternion facing = Quaternion.LookRotation(moveDirection);
            Quaternion next = Quaternion.RotateTowards(body.rotation, facing, 540f * Time.fixedDeltaTime);
            if (solidCollision != null) next = solidCollision.TurnTowards(next);
            else body.MoveRotation(next);
            // A post can allow a small partial turn and then block the rest.
            // Never make translation depend on finishing that turn: otherwise
            // the cat stays pinned at cat-tree platform edges. The compound
            // collision sweep still prevents movement through furniture.
            travel = moveDirection * (Quaternion.Angle(next, facing) <= 8f
                ? travelSpeed : travelSpeed * .7f);
        }
        if (ShouldWaitOutsideShelf(travel)) travel = Vector3.zero;
        velocity.x = travel.x;
        velocity.z = travel.z;
        body.linearVelocity = solidCollision != null
            ? solidCollision.LimitHorizontalVelocity(velocity, Time.fixedDeltaTime) : velocity;

        UpdateFootsteps(grounded);

        bool bufferedJump = Time.time - lastJumpPressedAt <= jumpBufferTime;
        bool canUseGround = Time.time - lastGroundedAt <= coyoteTime;
        if (bufferedJump && canUseGround && !isJumping)
        {
            if (animator != null)
            {
                if (spriteRenderer != null)
                    PlayDirectionalState("Jump", true);
                else
                    animator.SetTrigger("Jump");
            }
            if (!hasRefinedJump) cuteVisual?.PlayJumpStretch();
            isJumping = true;
            jumpStartedAt = Time.time;
            jumpLandingAt = float.NegativeInfinity;
            jumpPosePhase = 0f;
            if (hasRefinedJump) animator.SetFloat("JumpPhase", 0f);
            jumpTakeoffFootY = body.position.y - .225f;
            jumpCutApplied = false;
            lastJumpPressedAt = float.NegativeInfinity;
            lastGroundedAt = float.NegativeInfinity;
            velocity = body.linearVelocity;
            float launchHeight = jumpHeight;
            if (shelfPlatforms != null && shelfPlatforms.Length > 1)
            {
                Bounds first = shelfPlatforms[0].bounds;
                Vector3 feet = new Vector3(body.position.x, first.center.y, body.position.z);
                if (Mathf.Abs(jumpTakeoffFootY - first.max.y) < .09f &&
                    first.Contains(feet))
                    launchHeight = Mathf.Min(launchHeight, 1.05f);
            }
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * launchHeight);
            jumpLaunchSpeed = velocity.y;
            body.linearVelocity = velocity;
        }

        // Launch immediately on press. Releasing early trims only the remaining
        // upward velocity, so a tap reaches nearby low furniture while holding
        // keeps the existing full-height jump for the upper route.
        if (isJumping && !jumpCutApplied && jumpCutPending)
        {
            velocity = body.linearVelocity;
            if (velocity.y > 0f && Time.time - jumpStartedAt <= shortJumpReleaseWindow)
            {
                // Cap the total rise, not the current velocity fraction. A tap
                // can arrive after 0-3 render frames; a fixed multiplier made
                // those taps wildly different and sometimes too low to climb.
                float risen = Mathf.Max(0f, body.position.y - .225f - jumpTakeoffFootY);
                float remaining = Mathf.Max(0f, shortJumpHeight - risen);
                velocity.y = Mathf.Min(velocity.y,
                    Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * remaining));
                body.linearVelocity = velocity;
            }
            jumpCutApplied = true;
        }
        if (!jumpHeld && isJumping && !jumpCutApplied && Time.time - jumpStartedAt > Time.fixedDeltaTime)
            jumpCutPending = true;
        if (!isJumping) jumpCutPending = false;

        // A faster descent feels responsive while preserving enough ascent to
        // clear the chair and dining-table tops.
        if (body.linearVelocity.y < 0f)
            body.AddForce(Physics.gravity * (fallGravityMultiplier - 1f), ForceMode.Acceleration);

    }

    // The first two boards have the same footprint. Walking into the lower
    // bay during a held jump places the head below board two and cancels the
    // ascent. Wait just outside its front edge until the paws clear it; a
    // released (short) jump is free to enter the lower board immediately.
    private bool ShouldWaitOutsideShelf(Vector3 travel)
    {
        if (!isJumping || travel.sqrMagnitude < .001f ||
            shelfPlatforms == null || shelfPlatforms.Length < 2) return false;
        int target = jumpTakeoffFootY < shelfPlatforms[0].bounds.max.y - .15f ? (jumpHeld&&!jumpCutPending&&!jumpCutApplied?1:0) :
            System.Array.FindIndex(shelfPlatforms, b => b.bounds.max.y > jumpTakeoffFootY + .15f);
        if (target < 0) return false;
        Bounds board = shelfPlatforms[target].bounds;
        float footY = body.position.y - .225f;
        // Do not enter the board footprint until the paws are fully above it.
        if (footY >= board.max.y + .04f || body.linearVelocity.y <= 0f) return false;
        Vector3 atBoardHeight = new Vector3(body.position.x, board.center.y, body.position.z);
        Vector3 toward = board.ClosestPoint(atBoardHeight) - atBoardHeight;
        toward.y = 0f;
        return toward.sqrMagnitude > .0001f && toward.sqrMagnitude < .55f * .55f &&
               Vector3.Dot(travel.normalized, toward.normalized) > .6f;
    }

    private void UpdateFootsteps(bool grounded)
    {
        Vector3 currentPosition = transform.position;
        Vector3 travelled = currentPosition - lastFootstepPosition;
        travelled.y = 0f;
        lastFootstepPosition = currentPosition;

        bool walking = grounded && !IsHidden && moveDirection.sqrMagnitude > 0.02f &&
                       new Vector2(body.linearVelocity.x, body.linearVelocity.z).sqrMagnitude > 0.02f;
        if (!walking)
        {
            footstepDistanceTravelled = 0f;
            return;
        }

        footstepDistanceTravelled += travelled.magnitude;
        if (footstepDistanceTravelled < footstepDistance)
            return;

        footstepDistanceTravelled %= Mathf.Max(0.05f, footstepDistance);
        AudioManager.I?.PlayFootstep();
    }

    private void UpdateGait(bool grounded)
    {
        Vector3 travelled = body.position - lastGaitPosition;
        lastGaitPosition = body.position;
        travelled.y = 0f;
        float distance = travelled.magnitude;
        // Measure the result of the previous physics step, not desired velocity:
        // pushing against furniture must not make the paws run on the spot.
        bool walking = grounded && wasGaitGrounded && !isJumping && !isPushing && !IsHidden &&
                       moveInput.sqrMagnitude > 0.02f && GameFlowUI.GameplayInputEnabled &&
                       distance < Mathf.Max(0.25f, moveSpeed * Time.fixedDeltaTime * 3f);
        measuredGroundSpeed = walking ? distance / Mathf.Max(.001f, Time.fixedDeltaTime) : 0f;
        if (walking && measuredGroundSpeed > .04f)
            gaitPhase = Mathf.Repeat(gaitPhase + distance / Mathf.Max(.1f, walkCycleDistance), 1f);
        else if (moveInput.sqrMagnitude <= .02f || !grounded)
            gaitPhase = 0f;
        wasGaitGrounded = grounded;
    }

    private void UpdateVisualDirection()
    {
        // Use actual travel when sliding along furniture, not the direction
        // pressed into an obstacle. Preserve world-facing direction at rest so
        // orbiting the camera selects the correct idle sheet as well.
        if (moveInput.sqrMagnitude > .02f)
        {
            Vector3 travel = Vector3.ProjectOnPlane(body.linearVelocity, Vector3.up);
            lastFacingDirection = travel.sqrMagnitude > .0016f
                ? travel.normalized : moveDirection.normalized;
        }
        Vector2 screenDirection = new Vector2(lastFacingDirection.x, lastFacingDirection.z);
        Camera camera = Camera.main;
        if (camera != null)
        {
            // Match BillboardSprite's camera-to-subject bearing, not the camera
            // optical axis: collision/clamping can move the cat off centre.
            Vector3 forward = Vector3.ProjectOnPlane(transform.position - camera.transform.position, Vector3.up);
            if (forward.sqrMagnitude < .0001f)
                forward = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
            forward.Normalize();
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            screenDirection = new Vector2(Vector3.Dot(lastFacingDirection, right), Vector3.Dot(lastFacingDirection, forward));
        }
        // Hysteresis prevents diagonal input from rapidly switching sheets/size.
        float sideBias = Mathf.Abs(visualDirection.x) > .5f ? 0.85f : 1.18f;
        if (Mathf.Abs(screenDirection.x) > Mathf.Abs(screenDirection.y) * sideBias)
        {
            visualDirection = new Vector2(Mathf.Sign(screenDirection.x), 0f);
            if (spriteRenderer != null)
                // BillboardSprite presents this sheet mirrored to the camera, so
                // flip for screen-right movement and keep the source for screen-left.
                spriteRenderer.flipX = screenDirection.x > 0f;
        }
        else
        {
            // Positive Y moves away from the camera; negative Y moves toward it.
            visualDirection = new Vector2(0f, Mathf.Sign(screenDirection.y));
            if (spriteRenderer != null)
                spriteRenderer.flipX = false;
        }
    }

    private void UpdateAnimationState(bool isMoving)
    {
        if (!isMoving)
        {
            animator.speed = 1f;
            PlayDirectionalState("Idle", false);
            return;
        }

        // Sprite frames cannot cross-fade like bones. Sample the walk at the
        // travelled distance, preserving phase across direction changes.
        int state = DirectionalStateHash("Walk");
        animator.speed = 0f;
        animator.Play(state, 0, gaitPhase);
        currentVisualState = state;
    }

    private float DirectionVisualScale()
    {
        if (visualDirection.y < -0.5f)
            return frontVisualScale;
        if (visualDirection.y > 0.5f)
            return backVisualScale;
        return 1f;
    }

    private void ApplyVisualScale(float factor)
    {
        if (!hasBaseSpriteTransform || spriteRenderer == null)
            return;

        Transform visual = spriteRenderer.transform;
        visual.localScale = baseSpriteScale * factor;
        visual.localPosition = baseSpriteLocalPosition;
    }

    private void AlignGroundedFeet(float factor)
    {
        if (!hasBaseSpriteTransform || spriteRenderer.sprite == null)
            return;

        // All authored sheets use a 724 px frame at 362 pixels per unit. The
        // values below are the bottom of the solid paw silhouette (15% alpha),
        // not faint antialiasing noise. Aligning that point to the capsule's
        // physical bottom removes hovering and prevents walk frames sinking.
        const float framePivotPixelY = 362f;
        const float pixelsPerUnit = 362f;
        float footPixelY = SolidFootPixelY(spriteRenderer.sprite.name);
        float spriteFootY = (framePivotPixelY - footPixelY) / pixelsPerUnit *
                            baseSpriteScale.y * factor;
        Vector3 position = baseSpriteLocalPosition;
        position.y = groundedFootLocalY - spriteFootY;
        if (contactShadow == null) contactShadow = GetComponent<CatContactShadow>();
        if (contactShadow != null)
        {
            Vector3 physicalFoot = transform.TransformPoint(new Vector3(0f, groundedFootLocalY, 0f));
            Vector3 visibleFoot = contactShadow.VisibleGroundPoint(physicalFoot);
            position.y += (visibleFoot.y - physicalFoot.y) / Mathf.Max(.001f, transform.lossyScale.y);
        }
        spriteRenderer.transform.localPosition = position;
    }

    private float SolidFootPixelY(string spriteName)
    {
        return spriteName switch
        {
            "Cat_Jump_Side_00" => 585f,
            "Cat_Jump_Front_00" => 651f,
            "Cat_Jump_Back_00" => 615f,
            "Cat_Walk_Side_00" => 556f,
            "Cat_Walk_Side_01" => 555f,
            "Cat_Walk_Side_02" => 555f,
            "Cat_Walk_Side_03" => 556f,
            "Cat_Walk_Side_04" => 556f,
            "Cat_Walk_Side_05" => 555f,
            "Cat_Walk_Front_00" => 622f,
            "Cat_Walk_Front_01" => 630f,
            "Cat_Walk_Front_02" => 619f,
            "Cat_Walk_Front_03" => 630f,
            "Cat_Walk_Front_04" => 622f,
            "Cat_Walk_Front_05" => 623f,
            "Cat_Walk_Back_00" => 623f,
            "Cat_Walk_Back_01" => 612f,
            "Cat_Walk_Back_02" => 617f,
            "Cat_Walk_Back_03" => 605f,
            "Cat_Walk_Back_04" => 626f,
            "Cat_Walk_Back_05" => 613f,
            "Cat_Push_Side_00" => 628f,
            "Cat_Push_Side_01" => 628f,
            "Cat_Push_Side_02" => 628f,
            "Cat_Push_Side_03" => 626f,
            _ => visualDirection.y < -0.5f ? 624f :
                visualDirection.y > 0.5f ? 616f : 556f
        };
    }

    private void PlayDirectionalState(string actionName, bool restart)
    {
        int desiredState = DirectionalStateHash(actionName);
        if (!restart && desiredState == currentVisualState)
            return;
        animator.speed = 1f;
        animator.Play(desiredState, 0, 0f);
        currentVisualState = desiredState;
    }

    private int DirectionalStateHash(string actionName)
    {
        string directionName;
        if (visualDirection.y > 0.5f)
            directionName = "Back";
        else if (visualDirection.y < -0.5f)
            directionName = "Front";
        else
            directionName = "Side";

        string stateName = $"Base Layer.{actionName}_{directionName}";
        return Animator.StringToHash(stateName);
    }

    public static Vector3 RoomAlignedDirection(Vector2 input)
    {
        // LivingRoom walls and floor use world X/Z. Camera orbit must never
        // rotate keyboard controls; two keys still allow normalized diagonals.
        return Vector3.ClampMagnitude(new Vector3(input.x, 0f, input.y), 1f);
    }

    public static Vector3 CameraRelativeDirection(Vector2 input, Quaternion view)
    {
        Vector3 right = Vector3.ProjectOnPlane(view * Vector3.right, Vector3.up).normalized;
        if(right.sqrMagnitude < .01f) right = Vector3.right;
        Vector3 forward = Vector3.Cross(right, Vector3.up).normalized;
        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    private IEnumerator Push()
    {
        isPushing = true;
        if (animator != null)
        {
            if (spriteRenderer != null)
            {
                ApplyVisualScale(pushVisualScale);
                int pushState = Animator.StringToHash("Base Layer.Push_Side");
                animator.speed = 1f;
                animator.Play(pushState, 0, 0f);
                currentVisualState = pushState;
            }
            else
            {
                animator.SetTrigger("Push");
            }
        }

        yield return new WaitForSeconds(0.24f);
        TryPushNearby();

        yield return new WaitForSeconds(0.28f);
        isPushing = false;
    }

    public bool TryPushNearby()
    {
        if (IsHidden) return false;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        float feetY = transform.position.y + groundedFootLocalY;
        Vector3 low = transform.position; low.y = feetY + .035f;
        Vector3 high = low + Vector3.up * .34f;
        Valuable nearest = null; float best = float.PositiveInfinity;
        foreach (Collider candidate in Physics.OverlapCapsule(low, high, pushReach, Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            if (candidate.attachedRigidbody == null || candidate.attachedRigidbody == body) continue;
            Valuable item = candidate.attachedRigidbody.GetComponent<Valuable>();
            if (item == null || candidate.bounds.min.y > feetY + .40f || candidate.bounds.max.y < feetY - .04f) continue;
            CabinetOnlyTelevision cabinetOnly = item.GetComponent<CabinetOnlyTelevision>();
            if (cabinetOnly != null && !cabinetOnly.CanPush(this)) continue;
            Vector3 origin = transform.position;
            origin.y = Mathf.Clamp(candidate.bounds.center.y, feetY + .035f, feetY + .34f);
            Vector3 contact = candidate.ClosestPoint(origin);
            Vector3 flat = Vector3.ProjectOnPlane(contact - origin, Vector3.up);
            float distance = flat.magnitude;
            if (distance > pushReach || distance < .001f || Vector3.Dot(flat / distance, forward) < .5f) continue;
            // Candidate selection ignores our own compound colliders, but never
            // reaches through a wall, tabletop, or another prop.
            bool blocked = false;
            foreach (RaycastHit hit in Physics.RaycastAll(origin, (contact-origin).normalized, Vector3.Distance(origin,contact), Physics.AllLayers, QueryTriggerInteraction.Ignore))
                if (!hit.transform.IsChildOf(transform) && hit.rigidbody != candidate.attachedRigidbody) { blocked=true; break; }
            if (!blocked && distance < best) { best=distance; nearest=item; }
        }
        if (nearest == null || !nearest.TryNudge(forward)) return false;
        AudioManager.I?.PlayPawPush();
        return true;
    }

    private static Vector2 ReadMovement()
    {
        Vector2 movement = Vector2.zero;
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) movement.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) movement.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) movement.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) movement.y += 1f;
        }

        if (MobileCatControls.Instance != null && MobileCatControls.Instance.Move.sqrMagnitude > .001f)
            movement = MobileCatControls.Instance.Move;

        return Vector2.ClampMagnitude(movement, 1f);
    }

    private bool IsGrounded()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule == null)
            return Physics.Raycast(transform.position, Vector3.down, 0.7f, Physics.AllLayers,
                QueryTriggerInteraction.Ignore);

        Vector3 center = transform.TransformPoint(capsule.center);
        float radius = capsule.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z);
        float halfHeight = Mathf.Max(radius, capsule.height * transform.lossyScale.y * 0.5f);
        // The capsule comes to rest exactly tangent to a surface. Give the foot
        // probe a small contact skin so a valid floor/table contact is not lost
        // to floating-point separation (the old probe stopped ~4 cm too high).
        Vector3 foot = center + Vector3.down * (halfHeight - radius + 0.025f);
        Collider[] hits = Physics.OverlapSphere(foot, radius * 0.98f, Physics.AllLayers,
            QueryTriggerInteraction.Ignore);
        foreach (Collider hit in hits)
            if (hit != capsule && !hit.transform.IsChildOf(transform))
                return true;
        return false;
    }
}
