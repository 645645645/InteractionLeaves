using UnityEngine;

public class CharacterControll : MonoBehaviour
{
    [Header("角色移动设置")]
    public float movementSpeed = 5.0f;
    public float rotationSpeed = 10.0f;
    public float jumpForce = 8.0f;
    public float gravity = 20.0f;
    
    [Header("相机设置")]
    public Transform cameraTransform;
    public float cameraDistance = 5.0f;
    public float minCameraDistance = 2.0f;
    public float maxCameraDistance = 10.0f;
    public float zoomSensitivity = 1.0f;
    public float cameraHeight = 1.5f;
    public bool useCustomFocusPoint = false;
    public Transform focusPointTransform;
    public float focusPointHeight = 1.0f;
    public float cameraSensitivityY = 30.0f;
    public float cameraSensitivityX = 30.0f;
    public float minVerticalAngle = -40.0f;
    public float maxVerticalAngle = 80.0f;
    public float cameraSmoothTime = 0.2f;
    
    [Header("移动端设置")]
    public bool enableMobileControls = true;
    [Range(0f, 0.5f)] public float joystickAreaWidth = 0.4f;
    [Range(0f, 0.5f)] public float cameraAreaWidth = 0.6f;
    [Range(0f, 0.5f)] public float joystickDeadzone = 0.1f;
    
    [Header("动画设置")]
    public float animationBlendSpeed = 0.2f;
    public float rotationThreshold = 0.1f;
    
    private UnityEngine.CharacterController characterController;
    private Animator characterAnimator;
    private float verticalRotation;
    private float horizontalRotation;
    private Vector3 moveDirection = Vector3.zero;
    private Vector3 cameraVelocity = Vector3.zero;
    private Vector3 currentFocusPoint;
    private float targetCameraDistance;
    
    // 动画参数ID
    private int speedParamID;
    private int directionParamID;
    private int jumpParamID;
    // private int isGroundedParamID;
    
    // 移动平滑变量
    private Vector2 smoothJoystickInput;
    private Vector3 smoothMovementDirection;
    private float inputSmoothTime = 0.1f;
    private Vector2 inputVelocity = Vector2.zero;
    private Vector3 movementVelocity = Vector3.zero;
    
    // 移动端控制变量
    private Vector2 joystickInput;
    private bool isJoystickBeingDragged;
    private bool isCameraRotationBeingDragged;
    private int joystickTouchId = -1;
    private int cameraRotationTouchId = -1;
    private Vector2 lastCameraTouchPosition;
    private Vector2 joystickStartPosition;
    private float initialPinchDistance = -1;
    private bool isJumping = false;

    void Start()
    {
        characterController = GetComponentInChildren<CharacterController>();
        characterAnimator = GetComponentInChildren<Animator>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        // 缓存动画参数ID
        speedParamID = Animator.StringToHash("Speed");
        directionParamID = Animator.StringToHash("Direction");
        jumpParamID = Animator.StringToHash("Jump");
        // isGroundedParamID = Animator.StringToHash("IsGrounded");
        
        // 初始化焦点位置和相机距离
        UpdateFocusPoint();
        targetCameraDistance = cameraDistance;
        
        // 初始化相机旋转角度
        horizontalRotation = transform.eulerAngles.y;
        verticalRotation = cameraTransform.eulerAngles.x;
    }

    void Update()
    {
        UpdateFocusPoint();
        
        if (enableMobileControls && Application.isMobilePlatform)
        {
            HandleMobileInput();
            HandleMobileZoom();
        }
        else
        {
            HandlePCInput();
            HandlePCZoom();
        }
        
        HandleCharacterMovement();
        UpdateAnimation();
    }

    private void LateUpdate()
    {
        UpdateCameraPosition();
    }

    void UpdateFocusPoint()
    {
        if (useCustomFocusPoint && focusPointTransform != null)
        {
            currentFocusPoint = focusPointTransform.position;
        }
        else
        {
            currentFocusPoint = transform.position + Vector3.up * focusPointHeight;
        }
    }

    void HandlePCInput()
    {
        float mouseX = Input.GetAxis("Mouse X");
        float mouseY = Input.GetAxis("Mouse Y");
        HandleCameraRotation(mouseX, mouseY);
        
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");
        joystickInput = new Vector2(horizontal, vertical);
        
        if (Input.GetButton("Jump") )
        {
            if(characterController.isGrounded && !isJumping){
                moveDirection.y = jumpForce;
                isJumping = true;
                characterAnimator.SetTrigger(jumpParamID);
            }
        }
    }

    void HandlePCZoom()
    {
        float scrollInput = Input.GetAxis("Mouse ScrollWheel");
        if (scrollInput != 0)
        {
            targetCameraDistance -= scrollInput * zoomSensitivity;
            targetCameraDistance = Mathf.Clamp(targetCameraDistance, minCameraDistance, maxCameraDistance);
        }
    }

    void HandleMobileInput()
    {
        if (Input.touchCount >= 2)
        {
            isJoystickBeingDragged = false;
            isCameraRotationBeingDragged = false;
            return;
        }
        
        for (int i = 0; i < Input.touchCount; i++)
        {
            Touch touch = Input.GetTouch(i);
            
            switch (touch.phase)
            {
                case TouchPhase.Began:
                    HandleTouchBegan(touch);
                    break;
                    
                case TouchPhase.Moved:
                    HandleTouchMoved(touch);
                    break;
                    
                case TouchPhase.Ended:
                case TouchPhase.Canceled:
                    HandleTouchEnded(touch);
                    break;
            }
        }
        
        if (isCameraRotationBeingDragged && lastCameraTouchPosition != Vector2.zero)
        {
            Vector2 delta = (Vector2)Input.GetTouch(cameraRotationTouchId).position - lastCameraTouchPosition;
            HandleCameraRotation(delta.x * 0.1f, delta.y * 0.1f);
            lastCameraTouchPosition = Input.GetTouch(cameraRotationTouchId).position;
        }
    }

    void HandleMobileZoom()
    {
        if (Input.touchCount != 2)
        {
            initialPinchDistance = -1;
            return;
        }
        
        Touch touch1 = Input.GetTouch(0);
        Touch touch2 = Input.GetTouch(1);
        
        Vector2 touch1PrevPos = touch1.position - touch1.deltaPosition;
        Vector2 touch2PrevPos = touch2.position - touch2.deltaPosition;
        
        float prevTouchDelta = (touch1PrevPos - touch2PrevPos).magnitude;
        float touchDelta = (touch1.position - touch2.position).magnitude;
        
        float deltaMagnitudeDiff = prevTouchDelta - touchDelta;
        
        if (initialPinchDistance < 0)
        {
            initialPinchDistance = touchDelta;
            return;
        }
        
        targetCameraDistance += deltaMagnitudeDiff * 0.01f * zoomSensitivity;
        targetCameraDistance = Mathf.Clamp(targetCameraDistance, minCameraDistance, maxCameraDistance);
    }

    void HandleTouchBegan(Touch touch)
    {
        float screenWidth = Screen.width;
        float touchX = touch.position.x;
        
        if (touchX < screenWidth * joystickAreaWidth && joystickTouchId == -1)
        {
            joystickTouchId = touch.fingerId;
            isJoystickBeingDragged = true;
            joystickStartPosition = touch.position;
        }
        else if (touchX > screenWidth * (1 - cameraAreaWidth) && cameraRotationTouchId == -1)
        {
            cameraRotationTouchId = touch.fingerId;
            isCameraRotationBeingDragged = true;
            lastCameraTouchPosition = touch.position;
        }
        else if (joystickTouchId == -1 && cameraRotationTouchId == -1 && characterController.isGrounded && !isJumping)
        {
            moveDirection.y = jumpForce;
            isJumping = true;
            characterAnimator.SetTrigger(jumpParamID);
        }
    }

    void HandleTouchMoved(Touch touch)
    {
        if (touch.fingerId == joystickTouchId && isJoystickBeingDragged)
        {
            Vector2 touchDelta = touch.position - joystickStartPosition;
            
            float magnitude = touchDelta.magnitude / (Screen.width * joystickAreaWidth * 0.5f);
            if (magnitude > joystickDeadzone)
            {
                magnitude = Mathf.Clamp01((magnitude - joystickDeadzone) / (1 - joystickDeadzone));
                joystickInput = touchDelta.normalized * magnitude;
            }
            else
            {
                joystickInput = Vector2.zero;
            }
        }
    }

    void HandleTouchEnded(Touch touch)
    {
        if (touch.fingerId == joystickTouchId)
        {
            joystickTouchId = -1;
            isJoystickBeingDragged = false;
            joystickInput = Vector2.zero;
        }
        else if (touch.fingerId == cameraRotationTouchId)
        {
            cameraRotationTouchId = -1;
            isCameraRotationBeingDragged = false;
            lastCameraTouchPosition = Vector2.zero;
        }
    }

    void HandleCameraRotation(float horizontal, float vertical)
    {
        // horizontalRotation += horizontal * cameraSensitivityX * Time.deltaTime;
        // verticalRotation -= vertical * cameraSensitivityY * Time.deltaTime;
        horizontalRotation = Mathf.LerpAngle(horizontalRotation, horizontalRotation + cameraSensitivityX * horizontal, Time.deltaTime * 16);
        verticalRotation = Mathf.LerpAngle(verticalRotation, verticalRotation - cameraSensitivityY * vertical, Time.deltaTime * 16);
        verticalRotation = Mathf.Clamp(verticalRotation, minVerticalAngle, maxVerticalAngle);
    }

    void HandleCharacterMovement()
    {
        smoothJoystickInput = Vector2.SmoothDamp(
            smoothJoystickInput, 
            joystickInput, 
            ref inputVelocity, 
            inputSmoothTime
        );
        
        Vector3 targetMovement = new Vector3(smoothJoystickInput.x, 0, smoothJoystickInput.y);
        
        // 仅在有输入时更新角色朝向
        if (targetMovement.magnitude > rotationThreshold)
        {
            // 计算基于输入的角色目标旋转
            Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up);
            Vector3 movementDirection = cameraForward * targetMovement.z + cameraTransform.right * targetMovement.x;
            
            if (movementDirection != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(movementDirection, Vector3.up);
                
                // 平滑旋转角色
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, 
                    targetRotation, 
                    rotationSpeed * Time.deltaTime
                );
            }
        }
        
        // 基于相机方向的移动（修复重复定义问题）
        Vector3 cameraForwardDirection = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 horizontalMovement  = cameraForwardDirection * smoothJoystickInput.y + cameraTransform.right * smoothJoystickInput.x;
        
        targetMovement = horizontalMovement  * movementSpeed;
        
        smoothMovementDirection = Vector3.SmoothDamp(
            smoothMovementDirection, 
            targetMovement, 
            ref movementVelocity, 
            inputSmoothTime
        );
        
        if (characterController.isGrounded)
        {
            if (isJumping && moveDirection.y <= 0)
            {
                isJumping = false;
            }
            // moveDirection.y = 0;
        }
        else
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }
        
        moveDirection.x = smoothMovementDirection.x;
        moveDirection.z = smoothMovementDirection.z;
        
        characterController.Move(moveDirection * Time.deltaTime);
    }

    void UpdateAnimation()
    {
        // 计算移动速度（相对于角色前方的速度）
        Vector3 localVelocity = transform.InverseTransformDirection(characterController.velocity);
        float speed = localVelocity.z * 0.2f;
        float direction = localVelocity.x * 0.5f;
        
        // 将direction限制在[-1,1]范围内
        direction = Mathf.Clamp(direction, -1f, 1f);
        
        // 应用动画混合
        characterAnimator.SetFloat(speedParamID, speed, animationBlendSpeed, Time.deltaTime);
        characterAnimator.SetFloat(directionParamID, direction, animationBlendSpeed, Time.deltaTime);
        // characterAnimator.SetBool(isGroundedParamID, characterController.isGrounded);
    }

    void UpdateCameraPosition()
    {
        cameraDistance = Mathf.Lerp(cameraDistance, targetCameraDistance, Time.deltaTime * 5.0f);
        
        // 使用独立的相机旋转角度
        Quaternion rotation = Quaternion.Euler(verticalRotation, horizontalRotation, 0);
        Vector3 targetPosition = currentFocusPoint + rotation * (-Vector3.forward * cameraDistance) + Vector3.up * cameraHeight;

        cameraTransform.position = Vector3.SmoothDamp(
            cameraTransform.position, 
            targetPosition, 
            ref cameraVelocity, 
            cameraSmoothTime
        );
        
        cameraTransform.LookAt(currentFocusPoint);
    }
}    