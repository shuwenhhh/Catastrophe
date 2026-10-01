using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public sealed partial class CatController : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.15f;
    [SerializeField] private float jumpHeight = 1.05f;
    [SerializeField] private float coyoteTime = 0.12f;
    [SerializeField] private float jumpBufferTime = 0.15f;
    [SerializeField] private float fallGravityMultiplier = 2.1f;
    [SerializeField] private float pushForce = 4.5f;
    [SerializeField] private float pushReach = 0.65f;
    [SerializeField] private float frontVisualScale = 0.75f;
    [SerializeField] private float backVisualScale = 0.74f;
    [SerializeField] private float pushVisualScale = 0.67f;
    [SerializeField] private float groundedFootLocalY = -0.225f;

    private Rigidbody body;
    private Animator animator;
    private SpriteRenderer spriteRenderer;
    private Vector3 baseSpriteScale;
    private Vector3 baseSpriteLocalPosition;
    private bool hasBaseSpriteTransform;
    private Vector2 moveInput;
    private Vector2 visualDirection = Vector2.right;
    private Vector3 moveDirection;
    private bool pushRequested;
    private bool isPushing;
    private bool isJumping;
    private float jumpStartedAt;
    private float lastGroundedAt = float.NegativeInfinity;
    private float lastJumpPressedAt = float.NegativeInfinity;
    private int currentVisualState;
    private bool isMovingVisual;
    private GameObject activeHideSpot;

    public bool IsHidden { get; private set; }

    public void EnterHide(GameObject hideSpot)
    {
        activeHideSpot = hideSpot;
        IsHidden = activeHideSpot != null;
    }

    public void ExitHide()
    {
        activeHideSpot = null;
        IsHidden = false;
    }

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    private void Start()
    {
        CacheVisualComponents();
    }

    private void CacheVisualComponents()
    {
        animator = GetComponentInChildren<Animator>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
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
        if (animator == null || spriteRenderer == null)
            CacheVisualComponents();

        moveInput = ReadMovement();
        moveDirection = CameraRelativeDirection(moveInput);
        if (Keyboard.current != null && Keyboard.current.spaceKey.wasPressedThisFrame)
            lastJumpPressedAt = Time.time;
        pushRequested |= Keyboard.current != null &&
                         (Keyboard.current.eKey.wasPressedThisFrame || Keyboard.current.fKey.wasPressedThisFrame);

        if (animator != null)
        {
            Vector3 horizontalVelocity = body.linearVelocity;
            horizontalVelocity.y = 0f;
            float normalizedSpeed = moveSpeed > 0.001f
                ? Mathf.Clamp01(horizontalVelocity.magnitude / moveSpeed)
                : 0f;
            animator.SetFloat("Speed", normalizedSpeed, 0.1f, Time.deltaTime);
            UpdateVisualDirection();
            animator.SetFloat("DirectionX", visualDirection.x);
            animator.SetFloat("DirectionY", visualDirection.y);
            bool isGrounded = IsGrounded();
            animator.SetBool("Grounded", isGrounded);
            if (isJumping)
            {
                bool landed = isGrounded && Time.time - jumpStartedAt > 0.12f &&
                              body.linearVelocity.y <= 0.1f;
                if (landed)
                {
                    isJumping = false;
                    currentVisualState = 0;
                }
            }

            if (isJumping || isPushing)
                return;

            // Input is authoritative for the visual stop. Waiting for the next
            // physics tick can leave the last walk sprite (often a raised paw)
            // visible after the player has already released the key.
            isMovingVisual = moveInput.sqrMagnitude > 0.02f;
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
        bool grounded = IsGrounded();
        if (grounded)
            lastGroundedAt = Time.time;

        Vector3 velocity = body.linearVelocity;
        velocity.x = moveDirection.x * moveSpeed;
        velocity.z = moveDirection.z * moveSpeed;
        body.linearVelocity = velocity;

        bool bufferedJump = Time.time - lastJumpPressedAt <= jumpBufferTime;
        bool canUseGround = Time.time - lastGroundedAt <= coyoteTime;
        if (bufferedJump && canUseGround && !isJumping)
        {
            if (animator != null)
                PlayDirectionalState("Jump", true);
            isJumping = true;
            jumpStartedAt = Time.time;
            lastJumpPressedAt = float.NegativeInfinity;
            lastGroundedAt = float.NegativeInfinity;
            velocity = body.linearVelocity;
            velocity.y = Mathf.Sqrt(2f * Mathf.Abs(Physics.gravity.y) * jumpHeight);
            body.linearVelocity = velocity;
        }

        // A faster descent feels responsive while preserving enough ascent to
        // clear the chair and dining-table tops.
        if (body.linearVelocity.y < 0f)
            body.AddForce(Physics.gravity * (fallGravityMultiplier - 1f), ForceMode.Acceleration);

        if (moveDirection.sqrMagnitude > 0.02f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 0.22f);
        }
    }

    private void UpdateVisualDirection()
    {
        if (moveInput.sqrMagnitude <= 0.02f)
            return;

        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            visualDirection = new Vector2(Mathf.Sign(moveInput.x), 0f);
            if (spriteRenderer != null)
                // BillboardSprite presents this sheet mirrored to the camera, so
                // flip for screen-right movement and keep the source for screen-left.
                spriteRenderer.flipX = moveInput.x > 0f;
        }
        else
        {
            // Positive Y moves away from the camera; negative Y moves toward it.
            visualDirection = new Vector2(0f, Mathf.Sign(moveInput.y));
            if (spriteRenderer != null)
                spriteRenderer.flipX = false;
        }
    }

    private void UpdateAnimationState(bool isMoving)
    {
        PlayDirectionalState(isMoving ? "Walk" : "Idle", false);
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
        spriteRenderer.transform.localPosition = position;
    }

    private float SolidFootPixelY(string spriteName)
    {
        return spriteName switch
        {
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
        string directionName;
        if (visualDirection.y > 0.5f)
            directionName = "Back";
        else if (visualDirection.y < -0.5f)
            directionName = "Front";
        else
            directionName = "Side";

        string stateName = $"Base Layer.{actionName}_{directionName}";
        int desiredState = Animator.StringToHash(stateName);
        if (!restart && desiredState == currentVisualState)
            return;

        animator.CrossFadeInFixedTime(desiredState, restart ? 0.025f : 0.08f, 0, 0f);
        currentVisualState = desiredState;
    }

    private static Vector3 CameraRelativeDirection(Vector2 input)
    {
        Camera gameCamera = Camera.main;
        if (gameCamera == null)
            return new Vector3(input.x, 0f, input.y);

        Vector3 forward = gameCamera.transform.forward;
        forward.y = 0f;
        forward = forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward;

        Vector3 right = gameCamera.transform.right;
        right.y = 0f;
        right = right.sqrMagnitude > 0.001f ? right.normalized : Vector3.right;
        return Vector3.ClampMagnitude(right * input.x + forward * input.y, 1f);
    }

    private IEnumerator Push()
    {
        isPushing = true;
        if (animator != null)
        {
            ApplyVisualScale(pushVisualScale);
            int pushState = Animator.StringToHash("Base Layer.Push_Side");
            // This controller is intentionally driven directly from code (its
            // directional states have no transition graph). Play guarantees E
            // enters the push state immediately; a trigger alone had no route.
            animator.Play(pushState, 0, 0f);
            currentVisualState = pushState;
        }

        yield return new WaitForSeconds(0.24f);
        Vector3 origin = transform.position + Vector3.up * 0.20f;
        if (Physics.SphereCast(origin, 0.14f, transform.forward, out RaycastHit hit, pushReach,
                Physics.AllLayers, QueryTriggerInteraction.Ignore))
        {
            Rigidbody targetBody = hit.rigidbody;
            if (targetBody != null && targetBody != body)
                targetBody.AddForce((transform.forward + Vector3.up * 0.12f) * pushForce, ForceMode.Impulse);
        }

        yield return new WaitForSeconds(0.28f);
        isPushing = false;
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

        Touchscreen touchscreen = Touchscreen.current;
        if (touchscreen != null && touchscreen.primaryTouch.press.isPressed)
            movement += touchscreen.primaryTouch.delta.ReadValue() / 30f;

        Mouse mouse = Mouse.current;
        if (mouse != null && mouse.leftButton.isPressed)
            movement += mouse.delta.ReadValue() / 30f;

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
