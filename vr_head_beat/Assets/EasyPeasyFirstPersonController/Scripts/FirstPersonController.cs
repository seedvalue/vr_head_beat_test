using System;
using UnityEngine;
using UnityEngine.InputSystem; // Новый Input System

namespace EasyPeasyFirstPersonController
{
    public partial class FirstPersonController : MonoBehaviour
    {
        [Header("Mouse Look Settings")]
        [Range(0, 100)] public float mouseSensitivity = 25f;
        [Range(0f, 200f)] private float snappiness = 100f;
        [Header("Movement Speeds")]
        [Range(0f, 20f)] public float walkSpeed = 10f;
        [Range(0f, 30f)] public float sprintSpeed = 15f;
        [Range(0f, 10f)] public float crouchSpeed = 6f;
        [Header("Crouching & Sliding")]
        public float crouchHeight = 1f;
        public float crouchCameraHeight = 0.5f;
        public float slideSpeed = 9f;
        public float slideDuration = 0.7f;
        public float slideFovBoost = 5f;
        public float slideTiltAngle = 5f;
        [Header("Jumping")]
        [Range(0f, 15f)] public float jumpSpeed = 3f;
        [Range(0f, 50f)] public float gravity = 9.81f;
        public bool coyoteTimeEnabled = true;
        public float coyoteTimeDuration = 0.25f;
        [Header("Camera FOV")]
        public float normalFov = 60f;
        public float sprintFov = 70f;
        public float fovChangeSpeed = 5f;
        [Header("Head Bobbing")]
        public float walkingBobbingSpeed = 14f;
        public float bobbingAmount = 0.05f;
        private float sprintBobMultiplier = 1.2f;
        private float recoilReturnSpeed = 8f;
        [Header("Abilities")]
        public bool canSlide = true;
        public bool canJump = true;
        public bool canSprint = true;
        public bool canCrouch = true;
        [Header("Ground Check")]
        public Transform groundCheck;
        public float groundDistance = 0.3f;
        public LayerMask groundMask;
        [Header("References")]
        public Transform playerCamera;
        public Transform cameraParent;

        // Private fields
        private float rotX, rotY;
        private float xVelocity, yVelocity;
        private CharacterController characterController;
        private Vector3 moveDirection = Vector3.zero;
        private bool isGrounded;
        private Vector2 moveInput; // Input value from new system
        private Vector2 lookInput; // Input value from new system
        public bool isSprinting;
        public bool isCrouching;
        public bool isSliding;
        private float slideTimer;
        private float postSlideCrouchTimer;
        private Vector3 slideDirection;
        private float originalHeight;
        private float originalCameraParentHeight;
        private float coyoteTimer;
        private Camera cam;
        private AudioSource slideAudioSource;
        private float bobTimer;
        private float defaultPosY;
        private Vector3 recoil = Vector3.zero;
        private bool isLook = true, isMove = true;
        private float currentCameraHeight;
        private float currentBobOffset;
        private float currentFov;
        private float fovVelocity;
        private float currentSlideSpeed;
        private float slideSpeedVelocity;
        private float currentTiltAngle;
        private float tiltVelocity;

        // Новые поля для нового Input System
        // ИСПРАВЛЕНО: Используем базовый класс InputActionAsset вместо конкретного
        private InputActionAsset inputActionAsset;
        private InputActionMap playerActionMap;
        
        // Конкретные действия
        private InputAction moveAction;
        private InputAction lookAction;
        private InputAction jumpAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private InputAction slideAction;

        public float CurrentCameraHeight => isCrouching || isSliding ? crouchCameraHeight : originalCameraParentHeight;

        private void Awake()
        {
            characterController = GetComponent<CharacterController>();
            cam = playerCamera.GetComponent<Camera>();
            originalHeight = characterController.height;
            originalCameraParentHeight = cameraParent.localPosition.y;
            defaultPosY = cameraParent.localPosition.y;
            slideAudioSource = gameObject.AddComponent<AudioSource>();
            slideAudioSource.playOnAwake = false;
            slideAudioSource.loop = false;
            currentCameraHeight = originalCameraParentHeight;
            currentBobOffset = 0f;
            currentFov = normalFov;
            currentSlideSpeed = 0f;
            currentTiltAngle = 0f;

            // ИСПРАВЛЕНО: Инициализация через компонент PlayerInput или напрямую
            // Попробуем найти компонент PlayerInput на этом объекте
            PlayerInput playerInput = GetComponent<PlayerInput>();
            if (playerInput != null && playerInput.actions != null)
            {
                inputActionAsset = playerInput.actions;
                // Убедитесь, что имя Action Map совпадает с тем, что в вашем .inputactions файле
                playerActionMap = inputActionAsset.FindActionMap("Player"); // Или "DesktopTest"
            }
            else
            {
                // Альтернатива: если компонент PlayerInput не используется
                // Загрузите ассет вручную (замените путь на ваш)
                // inputActionAsset = Resources.Load<InputActionAsset>("XRI_Input_Actions_Game");
                // playerActionMap = inputActionAsset.FindActionMap("Player");
                
                Debug.LogWarning("PlayerInput component not found or actions not assigned. Make sure to assign Input Actions in Inspector or load them manually.");
            }

            if (playerActionMap != null)
            {
                // Находим конкретные действия по их именам
                moveAction = playerActionMap.FindAction("Move");
                lookAction = playerActionMap.FindAction("Look");
                jumpAction = playerActionMap.FindAction("Jump");
                sprintAction = playerActionMap.FindAction("Sprint");
                crouchAction = playerActionMap.FindAction("Crouch");
                slideAction = playerActionMap.FindAction("Slide");

                // Подписываемся на события
                if (moveAction != null)
                {
                    moveAction.performed += OnMove;
                    moveAction.canceled += OnMove;
                }
                
                if (lookAction != null)
                {
                    lookAction.performed += OnLook;
                    lookAction.canceled += OnLook;
                }
            }
        }

        private void OnEnable()
        {
            playerActionMap?.Enable();
            Cursor.lockState = CursorLockMode.Locked;
        }

        private void OnDisable()
        {
            playerActionMap?.Disable();
        }

        // Callback методы для нового Input System
        private void OnMove(InputAction.CallbackContext context)
        {
            moveInput = context.ReadValue<Vector2>();
        }

        private void OnLook(InputAction.CallbackContext context)
        {
            lookInput = context.ReadValue<Vector2>();
        }

        private void Update()
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);
            if (isGrounded && moveDirection.y < 0)
            {
                moveDirection.y = -2f;
                coyoteTimer = coyoteTimeEnabled ? coyoteTimeDuration : 0f;
            }
            else if (coyoteTimeEnabled)
            {
                coyoteTimer -= Time.deltaTime;
            }
            if (isLook)
            {
                // Используем значения из нового Input System
                float mouseX = lookInput.x * 10 * mouseSensitivity * Time.deltaTime;
                float mouseY = lookInput.y * 10 * mouseSensitivity * Time.deltaTime;
                rotX += mouseX;
                rotY -= mouseY;
                rotY = Mathf.Clamp(rotY, -90f, 90f);
                xVelocity = Mathf.Lerp(xVelocity, rotX, snappiness * Time.deltaTime);
                yVelocity = Mathf.Lerp(yVelocity, rotY, snappiness * Time.deltaTime);
                float targetTiltAngle = isSliding ? slideTiltAngle : 0f;
                currentTiltAngle = Mathf.SmoothDamp(currentTiltAngle, targetTiltAngle, ref tiltVelocity, 0.2f);
                playerCamera.transform.localRotation = Quaternion.Euler(yVelocity - currentTiltAngle, 0f, 0f);
                transform.rotation = Quaternion.Euler(0f, xVelocity, 0f);
            }
            HandleHeadBob();
            
            // Получаем состояние кнопок из нового Input System
            bool wantsToCrouch = canCrouch && (crouchAction?.IsPressed() ?? false) && !isSliding;
            
            Vector3 point1 = transform.position + characterController.center - Vector3.up * (characterController.height * 0.5f);
            Vector3 point2 = point1 + Vector3.up * characterController.height * 0.6f;
            float capsuleRadius = characterController.radius * 0.95f;
            float castDistance = isSliding ? originalHeight + 0.2f : originalHeight - crouchHeight + 0.2f;
            bool hasCeiling = Physics.CapsuleCast(point1, point2, capsuleRadius, Vector3.up, castDistance, groundMask);

            if (isSliding)
            {
                postSlideCrouchTimer = 0.3f;
            }

            if (postSlideCrouchTimer > 0)
            {
                postSlideCrouchTimer -= Time.deltaTime;
                isCrouching = canCrouch;
            }
            else
            {
                isCrouching = canCrouch && (wantsToCrouch || (hasCeiling && !isSliding));
            }

            // Проверка на слайд с использованием нового Input System
            if (canSlide && isSprinting && (slideAction?.WasPressedThisFrame() ?? false) && isGrounded)
            {
                isSliding = true;
                slideTimer = slideDuration;
                slideDirection = moveInput.magnitude > 0.1f ? (transform.right * moveInput.x + transform.forward * moveInput.y).normalized : transform.forward;
                currentSlideSpeed = sprintSpeed;
            }

            float slideProgress = slideTimer / slideDuration;
            if (isSliding)
            {
                slideTimer -= Time.deltaTime;
                if (slideTimer <= 0f || !isGrounded)
                {
                    isSliding = false;
                }
                float targetSlideSpeed = slideSpeed * Mathf.Lerp(0.7f, 1f, slideProgress);
                currentSlideSpeed = Mathf.SmoothDamp(currentSlideSpeed, targetSlideSpeed, ref slideSpeedVelocity, 0.2f);
                characterController.Move(slideDirection * currentSlideSpeed * Time.deltaTime);
            }

            float targetHeight = isCrouching || isSliding ? crouchHeight : originalHeight;
            characterController.height = Mathf.Lerp(characterController.height, targetHeight, Time.deltaTime * 10f);
            characterController.center = new Vector3(0f, characterController.height * 0.5f, 0f);

            float targetFov = isSprinting ? sprintFov : (isSliding ? sprintFov + (slideFovBoost * Mathf.Lerp(0f, 1f, 1f - slideProgress)) : normalFov);
            currentFov = Mathf.SmoothDamp(currentFov, targetFov, ref fovVelocity, 1f / fovChangeSpeed);
            cam.fieldOfView = currentFov;
            HandleMovement();
        }

        private void HandleHeadBob()
        {
            Vector3 horizontalVelocity = new Vector3(characterController.velocity.x, 0f, characterController.velocity.z);
            bool isMovingEnough = horizontalVelocity.magnitude > 0.1f;
            float targetBobOffset = isMovingEnough ? Mathf.Sin(bobTimer) * bobbingAmount : 0f;
            currentBobOffset = Mathf.Lerp(currentBobOffset, targetBobOffset, Time.deltaTime * walkingBobbingSpeed);

            if (!isGrounded || isSliding || isCrouching)
            {
                bobTimer = 0f;
                float targetCameraHeight = isCrouching || isSliding ? crouchCameraHeight : originalCameraParentHeight;
                currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, Time.deltaTime * 10f);
                cameraParent.localPosition = new Vector3(
                    cameraParent.localPosition.x,
                    currentCameraHeight + currentBobOffset,
                    cameraParent.localPosition.z);
                recoil = Vector3.zero;
                cameraParent.localRotation = Quaternion.RotateTowards(cameraParent.localRotation, Quaternion.Euler(recoil), recoilReturnSpeed * Time.deltaTime);
                return;
            }

            if (isMovingEnough)
            {
                float bobSpeed = walkingBobbingSpeed * (isSprinting ? sprintBobMultiplier : 1f);
                bobTimer += Time.deltaTime * bobSpeed;
                float targetCameraHeight = isCrouching || isSliding ? crouchCameraHeight : originalCameraParentHeight;
                currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, Time.deltaTime * 10f);
                cameraParent.localPosition = new Vector3(
                    cameraParent.localPosition.x,
                    currentCameraHeight + currentBobOffset,
                    cameraParent.localPosition.z);
                recoil.z = moveInput.x * -2f;
            }
            else
            {
                bobTimer = 0f;
                float targetCameraHeight = isCrouching || isSliding ? crouchCameraHeight : originalCameraParentHeight;
                currentCameraHeight = Mathf.Lerp(currentCameraHeight, targetCameraHeight, Time.deltaTime * 10f);
                cameraParent.localPosition = new Vector3(
                    cameraParent.localPosition.x,
                    currentCameraHeight + currentBobOffset,
                    cameraParent.localPosition.z);
                recoil = Vector3.zero;
            }
            cameraParent.localRotation = Quaternion.RotateTowards(cameraParent.localRotation, Quaternion.Euler(recoil), recoilReturnSpeed * Time.deltaTime);
        }

        private void HandleMovement()
        {
            // moveInput уже получаем из OnMove callback
            
            // Проверка на спринт с использованием нового Input System
            isSprinting = canSprint && (sprintAction?.IsPressed() ?? false) && moveInput.y > 0.1f && isGrounded && !isCrouching && !isSliding;
            
            float currentSpeed = isCrouching ? crouchSpeed : (isSprinting ? sprintSpeed : walkSpeed);
            if (!isMove) currentSpeed = 0f;

            Vector3 direction = new Vector3(moveInput.x, 0f, moveInput.y);
            Vector3 moveVector = transform.TransformDirection(direction) * currentSpeed;
            moveVector = Vector3.ClampMagnitude(moveVector, currentSpeed);

            if (isGrounded || coyoteTimer > 0f)
            {
                // Проверка на прыжок с использованием нового Input System
                if (canJump && (jumpAction?.WasPressedThisFrame() ?? false) && !isSliding)
                {
                    moveDirection.y = jumpSpeed;
                }
                else if (moveDirection.y < 0)
                {
                    moveDirection.y = -2f;
                }
            }
            else
            {
                moveDirection.y -= gravity * Time.deltaTime;
            }

            if (!isSliding)
            {
                moveDirection = new Vector3(moveVector.x, moveDirection.y, moveVector.z);
                characterController.Move(moveDirection * Time.deltaTime);
            }
        }

        public void SetControl(bool newState)
        {
            SetLookControl(newState);
            SetMoveControl(newState);
        }

        public void SetLookControl(bool newState)
        {
            isLook = newState;
        }

        public void SetMoveControl(bool newState)
        {
            isMove = newState;
        }

        public void SetCursorVisibility(bool newVisibility)
        {
            Cursor.lockState = newVisibility ? CursorLockMode.None : CursorLockMode.Locked;
            Cursor.visible = newVisibility;
        }

        // Очистка подписок при уничтожении
        private void OnDestroy()
        {
            if (moveAction != null)
            {
                moveAction.performed -= OnMove;
                moveAction.canceled -= OnMove;
            }
            
            if (lookAction != null)
            {
                lookAction.performed -= OnLook;
                lookAction.canceled -= OnLook;
            }
        }
    }
}