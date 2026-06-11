using System.Collections;
using UnityEngine;

public enum PaperForm { Paper, Ball, Plane }

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerController : MonoBehaviour
{
    [Header("Sprites (assigned by LevelBuilder)")]
    public Sprite[] paperIdle;
    public Sprite[] paperWalk;
    public Sprite[] paperFold;
    public Sprite[] ballFrames;
    public Sprite[] planeFrames;

    [Header("Paper")]
    public float runSpeed = 8f;
    public float runAccel = 70f;
    public float airAccel = 45f;
    public float jumpSpeed = 15.5f;
    public float paperGravity = 2.5f;
    public float coyoteTime = 0.12f;
    public float jumpBufferTime = 0.12f;

    [Header("Ball")]
    public float ballGravity = 2f;
    public float ballForce = 30f;
    public float ballSelfSpeed = 8f;   // fastest the player can drive the ball unaided
    public float ballMaxSpeed = 24f;   // hard cap (ramp/gravity fed)

    [Header("Plane")]
    public float planeGravity = 0.4f;
    public float planeBoost = 12f;
    public float planeTopSpeed = 16f;
    public float planeSteer = 8f;
    public float planeMaxFall = 2.6f;   // gentle sink at full speed
    public float planeStallFall = 9f;   // fall rate when too slow

    [Header("State")]
    public bool doubleJumpUnlocked;
    public PaperForm form = PaperForm.Paper;

    public Rigidbody2D Body { get; private set; }
    public bool InputLocked { get; set; }

    BoxCollider2D paperCol;
    CircleCollider2D ballCol;
    BoxCollider2D planeCol;
    SpriteRenderer sr;
    Transform visual;

    const int GroundMask = 1 << 0; // world geometry lives on Default; player on Ignore Raycast

    bool grounded;
    float lastGroundedTime = -99f;
    float jumpPressedTime = -99f;
    bool airJumpAvailable;
    int facing = 1;
    float animClock;
    Coroutine squashRoutine;

    void Awake()
    {
        Body = GetComponent<Rigidbody2D>();
        visual = transform.Find("Visual");
        sr = visual.GetComponent<SpriteRenderer>();

        paperCol = null; ballCol = null; planeCol = null;
        foreach (var box in GetComponents<BoxCollider2D>())
        {
            if (box.size.x > box.size.y) planeCol = box; else paperCol = box;
        }
        ballCol = GetComponent<CircleCollider2D>();

        // The ball is a near-frictionless slider: gravity accelerates it down
        // ramps with no fuss, and with no surface grip it can never crawl up
        // a vertical wall. The rolling look is faked on the Visual transform.
        var ballMat = new PhysicsMaterial2D("ball") { friction = 0.02f, bounciness = 0.05f };
        ballCol.sharedMaterial = ballMat;
        var paperMat = new PhysicsMaterial2D("paper") { friction = 0f, bounciness = 0f };
        paperCol.sharedMaterial = paperMat; // no wall-sticking
        planeCol.sharedMaterial = paperMat;
        Body.freezeRotation = true;

        ApplyForm(PaperForm.Paper, instant: true);
    }

    void Update()
    {
        if (InputLocked) return;

        if (Input.GetKeyDown(KeyCode.Space)) jumpPressedTime = Time.time;
        if (Input.GetKeyUp(KeyCode.Space) && form == PaperForm.Paper && Body.linearVelocity.y > 0)
            Body.linearVelocity = new Vector2(Body.linearVelocity.x, Body.linearVelocity.y * 0.5f);

        if (Input.GetKeyDown(KeyCode.X) && form != PaperForm.Ball) TrySetForm(PaperForm.Ball);
        if (Input.GetKeyDown(KeyCode.Z) && form != PaperForm.Paper) TrySetForm(PaperForm.Paper);
        if (Input.GetKeyDown(KeyCode.C) && form != PaperForm.Plane && !grounded) TrySetForm(PaperForm.Plane);

        Animate();
    }

    void FixedUpdate()
    {
        grounded = CheckGrounded();
        if (grounded)
        {
            lastGroundedTime = Time.time;
            airJumpAvailable = true;
        }

        if (InputLocked) return;

        float input = 0f;
        if (Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow)) input -= 1f;
        if (Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow)) input += 1f;
        if (input != 0) facing = input > 0 ? 1 : -1;

        switch (form)
        {
            case PaperForm.Paper: TickPaper(input); break;
            case PaperForm.Ball: TickBall(input); break;
            case PaperForm.Plane: TickPlane(input); break;
        }
    }

    void TickPaper(float input)
    {
        var v = Body.linearVelocity;
        float accel = grounded ? runAccel : airAccel;
        v.x = Mathf.MoveTowards(v.x, input * runSpeed, accel * Time.fixedDeltaTime);

        bool wantsJump = Time.time - jumpPressedTime < jumpBufferTime;
        bool canCoyote = Time.time - lastGroundedTime < coyoteTime;
        if (wantsJump && canCoyote)
        {
            v.y = jumpSpeed;
            jumpPressedTime = -99f;
            lastGroundedTime = -99f;
            Squash(0.7f, 1.25f);
        }
        else if (wantsJump && !grounded && doubleJumpUnlocked && airJumpAvailable)
        {
            v.y = jumpSpeed * 0.95f;
            airJumpAvailable = false;
            jumpPressedTime = -99f;
            Squash(0.75f, 1.2f);
        }
        Body.linearVelocity = v;
    }

    void TickBall(float input)
    {
        var v = Body.linearVelocity;
        // Player effort only works below ballSelfSpeed (or when braking);
        // anything faster has to be earned by rolling downhill.
        bool braking = input != 0 && Mathf.Sign(input) != Mathf.Sign(v.x);
        if (braking || Mathf.Abs(v.x) < ballSelfSpeed)
            Body.AddForce(new Vector2(input * ballForce, 0f));
        v = Body.linearVelocity;
        if (Mathf.Abs(v.x) > ballMaxSpeed) v.x = Mathf.Sign(v.x) * ballMaxSpeed;
        Body.linearVelocity = v;
        // balls cannot jump — that's the whole point
    }

    void TickPlane(float input)
    {
        var v = Body.linearVelocity;
        if (input != 0)
            v.x = Mathf.MoveTowards(v.x, input * planeTopSpeed, planeSteer * Time.fixedDeltaTime);

        // lift scales with airspeed: fast = gentle sink, slow = stall
        float speed01 = Mathf.Clamp01(Mathf.Abs(v.x) / (planeBoost * 0.85f));
        float maxFall = Mathf.Lerp(planeStallFall, planeMaxFall, speed01);
        if (v.y < -maxFall) v.y = Mathf.MoveTowards(v.y, -maxFall, 30f * Time.fixedDeltaTime);
        Body.linearVelocity = v;

        if (grounded) TrySetForm(PaperForm.Paper); // landing auto-unfolds
    }

    public bool TrySetForm(PaperForm next)
    {
        if (next == form) return true;

        if (next == PaperForm.Paper && !RoomToUnfold())
        {
            Squash(1.15f, 0.85f); // bump: no room to unfold here
            return false;
        }
        ApplyForm(next, instant: false);
        return true;
    }

    void ApplyForm(PaperForm next, bool instant)
    {
        // lift slightly when growing a downward extent so we don't clip the floor
        if (!instant && next == PaperForm.Paper && form == PaperForm.Ball && grounded)
            transform.position += Vector3.up * (paperCol.size.y / 2f - ballCol.radius + 0.03f);

        form = next;
        paperCol.enabled = next == PaperForm.Paper;
        ballCol.enabled = next == PaperForm.Ball;
        planeCol.enabled = next == PaperForm.Plane;

        if (next == PaperForm.Ball)
        {
            Body.gravityScale = ballGravity;
            Body.linearDamping = 0.35f; // frictionless ball still coasts to a stop
        }
        else
        {
            Body.gravityScale = next == PaperForm.Plane ? planeGravity : paperGravity;
            Body.linearDamping = 0f;
        }

        if (next == PaperForm.Plane)
        {
            var v = Body.linearVelocity;
            int dir = v.x != 0 ? (int)Mathf.Sign(v.x) : facing;
            v.x = Mathf.Max(Mathf.Abs(v.x), planeBoost) * dir; // momentum boost per the design notes
            if (v.y < 0) v.y *= 0.4f;
            Body.linearVelocity = v;
            facing = dir;
        }

        visual.localRotation = Quaternion.identity;
        if (!instant) Squash(1.25f, 0.75f);
    }

    bool RoomToUnfold()
    {
        Vector2 center = transform.position;
        if (form == PaperForm.Ball && grounded)
            center.y += paperCol.size.y / 2f - ballCol.radius + 0.03f;
        return Physics2D.OverlapBox(center + paperCol.offset, paperCol.size * 0.95f, 0f, GroundMask) == null;
    }

    bool CheckGrounded()
    {
        Bounds b = ActiveCollider().bounds;
        Vector2 probe = new Vector2(b.center.x, b.min.y - 0.05f);
        Vector2 size = new Vector2(b.size.x * 0.85f, 0.1f);
        return Physics2D.OverlapBox(probe, size, 0f, GroundMask) != null;
    }

    Collider2D ActiveCollider()
    {
        if (ballCol.enabled) return ballCol;
        if (planeCol.enabled) return planeCol;
        return paperCol;
    }

    void Animate()
    {
        animClock += Time.deltaTime;
        sr.flipX = facing < 0;

        switch (form)
        {
            case PaperForm.Paper:
                if (!grounded)
                    sr.sprite = paperWalk[1];
                else if (Mathf.Abs(Body.linearVelocity.x) > 0.5f)
                    sr.sprite = paperWalk[(int)(animClock * 8f) % paperWalk.Length];
                else
                    sr.sprite = paperIdle[(int)(animClock * 1.6f) % paperIdle.Length];
                break;
            case PaperForm.Ball:
                sr.sprite = ballFrames[0];
                // fake the roll: spin the visual to match ground speed
                visual.Rotate(0f, 0f, -Body.linearVelocity.x / 0.34f * Mathf.Rad2Deg * Time.deltaTime);
                break;
            case PaperForm.Plane:
                var v = Body.linearVelocity;
                sr.sprite = planeFrames[v.y < -3f ? 1 : 0];
                float tilt = Mathf.Clamp(Mathf.Atan2(v.y, Mathf.Abs(v.x)) * Mathf.Rad2Deg, -35f, 25f);
                visual.localRotation = Quaternion.Euler(0, 0, facing > 0 ? tilt : -tilt);
                break;
        }
    }

    void Squash(float sx, float sy)
    {
        if (squashRoutine != null) StopCoroutine(squashRoutine);
        squashRoutine = StartCoroutine(SquashRoutine(sx, sy));
    }

    IEnumerator SquashRoutine(float sx, float sy)
    {
        var target = new Vector3(sx, sy, 1f);
        visual.localScale = target;
        float t = 0f;
        while (t < 0.18f)
        {
            t += Time.deltaTime;
            visual.localScale = Vector3.Lerp(target, Vector3.one, t / 0.18f);
            yield return null;
        }
        visual.localScale = Vector3.one;
    }

    public IEnumerator DeathCrumple()
    {
        InputLocked = true;
        Body.simulated = false;
        float t = 0f;
        while (t < 0.45f)
        {
            t += Time.deltaTime;
            float k = 1f - t / 0.45f;
            visual.localScale = new Vector3(k, k, 1f);
            visual.localRotation = Quaternion.Euler(0, 0, t * 720f);
            yield return null;
        }
    }

    public void ResetAt(Vector3 position)
    {
        transform.position = position;
        transform.rotation = Quaternion.identity;
        Body.simulated = true;
        Body.linearVelocity = Vector2.zero;
        Body.angularVelocity = 0f;
        ApplyForm(PaperForm.Paper, instant: true);
        visual.localScale = Vector3.one;
        visual.localRotation = Quaternion.identity;
        InputLocked = false;
    }
}
