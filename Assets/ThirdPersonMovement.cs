using UnityEngine;
using UnityEngine.InputSystem;

public class ThirdPersonMovement : MonoBehaviour
{
    private Transform cam;
    private Animator animator;
    private PlayerControls InputDetector;
    private CharacterController characterController;

    public float walkSpeed = 1.5f;
    public float runSpeed = 5;

    private int isWalkingHash;
    private int isRunningHash;
    private Vector2 currentMovementInput;
    private Vector3 currentMovement;
    private Vector3 currentRunMovement;
    private bool isMovementPressed;
    private bool isrunPressed;
    private float turnSmooth = 0.1f;

    private float gravity = -9.81f;
    private float terminalVelocity = -50f;
    private float trunSmotthVelocity;
    private Vector3 velocity;

    private void Awake()
    {
        cam = Camera.main.transform;
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        InputDetector = new PlayerControls();
        isWalkingHash = Animator.StringToHash("isWalking");
        isRunningHash = Animator.StringToHash("isRunning");
    }

    private void Start()
    {
        InputDetector.Gameplay.Movement.started += ctx => OnMovementPerformed(ctx);
        InputDetector.Gameplay.Movement.performed += ctx => OnMovementPerformed(ctx);
        InputDetector.Gameplay.Movement.canceled += ctx => OnMovementPerformed(ctx);
        InputDetector.Gameplay.Run.started += ctx => OnRun(ctx);
        InputDetector.Gameplay.Run.performed += ctx => OnRun(ctx);
        InputDetector.Gameplay.Run.canceled += ctx => OnRun(ctx);
    }

    private void OnRun(InputAction.CallbackContext ctx)
    {
        isrunPressed = ctx.ReadValueAsButton();
    }

    private void Update()
    {
        handleRotation();
        handleAnimation();
        handleMovement();
        handleGravity();

        // Apply the movement
        characterController.Move(velocity * Time.deltaTime);

        // Reset the movement
        velocity = Vector3.zero;
    }

    private void handleGravity()
    {
        if (characterController.isGrounded && velocity.y < 0)
        {
            velocity.y = -2f;
        }
        else
        {
            velocity.y += gravity;
        }
    }

    private void handleAnimation()
    {
        bool isWalking = animator.GetBool(isWalkingHash);
        bool isrunning = animator.GetBool(isRunningHash);

        if (isMovementPressed && !isWalking)
        {
            animator.SetBool(isWalkingHash, true);
        }
        if (!isMovementPressed && isWalking)
        {
            animator.SetBool(isWalkingHash, false);
        }
        if (isMovementPressed && isrunPressed && !isrunning)
        {
            animator.SetBool(isRunningHash, true);
        }
        if ((!isMovementPressed || !isrunPressed) && isrunning)
        {
            animator.SetBool(isRunningHash, false);
        }
    }

    private void handleRotation()
    {
        float targetAngle = Mathf.Atan2(currentMovementInput.x, currentMovementInput.y) * Mathf.Rad2Deg + cam.eulerAngles.y;
        float angle = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetAngle, ref trunSmotthVelocity, turnSmooth);
        if (isMovementPressed)
        {
            transform.rotation = Quaternion.Euler(0f, angle, 0f);
        }
    }

    private void handleMovement()
    {
        if (isMovementPressed)
        {
            float targetAngle = Mathf.Atan2(currentMovementInput.x, currentMovementInput.y) * Mathf.Rad2Deg + cam.eulerAngles.y;
            Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;

            if (isrunPressed)
            {
                velocity += moveDir.normalized * runSpeed;
            }
            else
            {
                velocity += moveDir.normalized * walkSpeed;
            }
        }
        /*
        float targetAngle = Mathf.Atan2(currentRunMovement.x, currentRunMovement.z) * Mathf.Rad2Deg + cam.eulerAngles.y;
        Vector3 moveDir = Quaternion.Euler(0f, targetAngle, 0f) * Vector3.forward;*/
    }

    private void OnMovementPerformed(InputAction.CallbackContext ctx)
    {
        currentMovementInput = ctx.ReadValue<Vector2>();
        currentMovement.x = currentMovementInput.x;
        currentMovement.z = currentMovementInput.y;
        isMovementPressed = currentMovementInput.x != 0 || currentMovementInput.y != 0;
    }

    private void OnEnable()
    {
        InputDetector.Enable();
    }

    private void OnDisable()
    {
        InputDetector.Disable();
    }
}