using UnityEngine;
using UnityEngine.InputSystem;
using FishNet.Object;

/// <summary>
/// 第一人称控制器：移动/跳跃/镜头 = 客户端（owner）本地计算，经 NetworkTransform(客户端权威) 同步。
/// 服务器：依据同步到的位置累计距离，到达步距广播脚步声（owner 播放；阶段二同源喂给仇恨系统）。
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : NetworkBehaviour
{
    public float walkSpeed = 4f;
    public float runSpeed = 7.5f;
    public float jumpHeight = 1.2f;
    public float gravity = -19.62f;
    public float mouseSensitivity = 0.08f;
    public float acceleration = 20f;
    public float deceleration = 30f;
    public float forwardFactor = 1f;
    public float backwardFactor = 0.55f;
    public float strafeFactor = 0.75f;
    public float walkStepDistance = 2.4f;
    public float runStepDistance = 3.6f;
    public Transform cameraTransform;

    public static bool AutoMove;

    public float CurrentSpeed => moveVelocity.magnitude;

    private CharacterController cc;
    private float localYaw;
    private float localPitch;
    private Vector3 velocity;
    private Vector3 moveVelocity;
    private bool inputEnabled;

    private bool hasLastPos;
    private Vector3 lastPos;
    private float stepAccum;

    private void Awake()
    {
        cc = GetComponent<CharacterController>();
        if (cameraTransform == null)
        {
            var t = transform.Find("Camera");
            if (t != null) cameraTransform = t;
        }
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        if (!IsOwner)
        {
            if (cameraTransform != null) cameraTransform.gameObject.SetActive(false);
            return;
        }

        inputEnabled = true;
        localYaw = transform.eulerAngles.y;

        var body = transform.Find("Body");
        if (body != null)
            foreach (var r in body.GetComponentsInChildren<Renderer>())
                r.enabled = false;

        Debug.Log("[Net] 客户端本地玩家已出生");
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    public override void OnStopClient()
    {
        base.OnStopClient();
        inputEnabled = false;
    }

    private void Update()
    {
        if (!IsOwner || !inputEnabled) return;

        if (Keyboard.current == null) return;

        if (Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            bool locked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.lockState = locked ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = locked;
        }

        if (Mouse.current != null && Cursor.lockState == CursorLockMode.Locked)
        {
            Vector2 delta = Mouse.current.delta.ReadValue() * mouseSensitivity;
            localYaw += delta.x;
            localPitch = Mathf.Clamp(localPitch - delta.y, -85f, 85f);
            transform.rotation = Quaternion.Euler(0f, localYaw, 0f);
            if (cameraTransform != null)
                cameraTransform.localRotation = Quaternion.Euler(localPitch, 0f, 0f);
        }

        if (cc.isGrounded && Keyboard.current.spaceKey.wasPressedThisFrame)
            velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
    }

    private void FixedUpdate()
    {
        if (IsOwner)
            ApplyMovementLocally(Time.fixedDeltaTime);

        if (IsServerStarted)
            UpdateServerFootsteps();
    }

    private void ApplyMovementLocally(float dt)
    {
        Vector2 input;
        bool run;

        if (AutoMove)
        {
            input = new Vector2(Mathf.Sin(Time.time * 1.2f), 1f);
            if (input.sqrMagnitude > 1f) input.Normalize();
            run = Time.time % 2f < 1f;
            if (Mathf.Sin(Time.time * 0.7f) > 0.98f && cc.isGrounded)
                velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
        }
        else
        {
            if (Keyboard.current == null) return;

            input = Vector2.zero;
            if (Keyboard.current.wKey.isPressed) input.y += 1f;
            if (Keyboard.current.sKey.isPressed) input.y -= 1f;
            if (Keyboard.current.dKey.isPressed) input.x += 1f;
            if (Keyboard.current.aKey.isPressed) input.x -= 1f;
            if (input.sqrMagnitude > 1f) input.Normalize();

            run = Keyboard.current.leftShiftKey.isPressed;
        }

        transform.rotation = Quaternion.Euler(0f, localYaw, 0f);

        float dirFactor = Mathf.Lerp(backwardFactor, forwardFactor, (input.y + 1f) * 0.5f);
        if (Mathf.Abs(input.x) > 0.01f) dirFactor *= strafeFactor;
        float speed = (run ? runSpeed : walkSpeed) * dirFactor;
        Vector3 desired = (transform.right * input.x + transform.forward * input.y) * speed;
        float rate = input.sqrMagnitude > 0f ? acceleration : deceleration;
        moveVelocity = Vector3.MoveTowards(moveVelocity, desired, rate * dt);

        if (cc.isGrounded && velocity.y < 0f) velocity.y = -2f;
        velocity.y += gravity * dt;
        cc.Move((moveVelocity + Vector3.up * velocity.y) * dt);
    }

    private void UpdateServerFootsteps()
    {
        if (cc == null) return;

        Vector3 p = transform.position;
        if (!hasLastPos)
        {
            hasLastPos = true;
            lastPos = p;
            return;
        }

        Vector2 prev = new Vector2(lastPos.x, lastPos.z);
        Vector2 cur = new Vector2(p.x, p.z);
        float dist = Vector2.Distance(prev, cur);
        lastPos = p;
        if (dist < 0.005f) return;

        float speed = dist / Time.fixedDeltaTime;
        bool run = speed > runSpeed * 0.8f;
        float threshold = run ? runStepDistance : walkStepDistance;
        stepAccum += dist;

        if (stepAccum >= threshold)
        {
            stepAccum = 0f;
            RpcPlayStep(run);
        }
    }

    [ObserversRpc(ExcludeServer = false)]
    private void RpcPlayStep(bool run)
    {
        if (!IsOwner) return;
        var clip = run ? SfxLibrary.RunStep : SfxLibrary.Step;
        if (clip == null) return;
        var src = SfxLibrary.EnsureSource(gameObject);
        src.PlayOneShot(clip, run ? 0.4f : 0.3f);
    }
}
