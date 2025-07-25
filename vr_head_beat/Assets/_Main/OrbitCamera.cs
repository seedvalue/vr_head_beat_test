using UnityEngine;
using UnityEngine.InputSystem;

public class OrbitCamera : MonoBehaviour
{
    [Header("Target Settings")]
    public Transform target;
    
    [Header("Distance Settings")]
    public float minDistance = 2f;
    public float maxDistance = 10f;
    [SerializeField] private float targetDistance = 5f;
    [SerializeField] private float currentDistance = 5f;
    
    [Header("Rotation Settings")]
    public float rotationSpeed = 5f;
    public float verticalMinAngle = -20f;
    public float verticalMaxAngle = 80f;
    
    [Header("Zoom Settings")]
    public float zoomSpeed = 20f;
    [Range(0.1f, 20f)] public float zoomSmoothness = 10f; // Плавность зума
    
    [Header("Rotation Smoothness")]
    [Range(0.1f, 20f)] public float rotationSmoothness = 15f; // Плавность вращения
    
    private Mouse mouse;
    private bool isRotating = false;
    
    private float targetRotationY = 0f;
    private float targetRotationX = 0f;
    private float currentRotationY = 0f;
    private float currentRotationX = 0f;
    
    private float lastScrollValue = 0f;
    private float scrollVelocity = 0f;
    
    void Start()
    {
        mouse = Mouse.current;
        
        if (target != null)
        {
            Vector3 angles = transform.eulerAngles;
            targetRotationY = currentRotationY = angles.y;
            targetRotationX = currentRotationX = angles.x;
            targetDistance = currentDistance = Vector3.Distance(transform.position, target.position);
        }
    }
    
    void Update()
    {
        if (target == null || mouse == null) return;
        
        // Проверяем нажатие правой кнопки мыши
        if (mouse.rightButton.wasPressedThisFrame)
        {
            StartRotation();
        }
        
        if (mouse.rightButton.wasReleasedThisFrame)
        {
            StopRotation();
        }
        
        // Обрабатываем вращение с плавностью
        if (isRotating && mouse.delta.ReadValue().magnitude > 0)
        {
            Vector2 delta = mouse.delta.ReadValue() * Time.deltaTime;
            targetRotationY += delta.x * rotationSpeed;
            targetRotationX -= delta.y * rotationSpeed;
            targetRotationX = Mathf.Clamp(targetRotationX, verticalMinAngle, verticalMaxAngle);
        }
        
        // Плавное вращение
        currentRotationY = Mathf.Lerp(currentRotationY, targetRotationY, rotationSmoothness * Time.deltaTime);
        currentRotationX = Mathf.Lerp(currentRotationX, targetRotationX, rotationSmoothness * Time.deltaTime);
        
        // Обрабатываем скролл с плавностью
        float scroll = mouse.scroll.ReadValue().y;
        if (Mathf.Abs(scroll) > 0.1f)
        {
            targetDistance -= scroll * zoomSpeed * Time.deltaTime;
            targetDistance = Mathf.Clamp(targetDistance, minDistance, maxDistance);
        }
        
        // Плавное изменение расстояния
        currentDistance = Mathf.Lerp(currentDistance, targetDistance, zoomSmoothness * Time.deltaTime);
    }
    
    void LateUpdate()
    {
        if (target == null) return;
        
        // Вычисляем позицию камеры с плавным вращением и зумом
        Vector3 direction = new Vector3(0, 0, -currentDistance);
        Quaternion rotation = Quaternion.Euler(currentRotationX, currentRotationY, 0);
        Vector3 position = target.position + rotation * direction;
        
        // Плавная интерполяция позиции
        transform.rotation = Quaternion.Lerp(transform.rotation, rotation, rotationSmoothness * Time.deltaTime);
        transform.position = Vector3.Lerp(transform.position, position, zoomSmoothness * Time.deltaTime);
    }
    
    private void StartRotation()
    {
        isRotating = true;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;
    }
    
    private void StopRotation()
    {
        isRotating = false;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
    }
    
    // Метод для мгновенной установки расстояния без плавности
    public void SetDistanceImmediate(float distance)
    {
        targetDistance = currentDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }
    
    // Метод для плавной установки расстояния
    public void SetDistance(float distance)
    {
        targetDistance = Mathf.Clamp(distance, minDistance, maxDistance);
    }
    
    // Метод для мгновенной установки углов вращения
    public void SetRotationImmediate(float xAngle, float yAngle)
    {
        targetRotationX = currentRotationX = xAngle;
        targetRotationY = currentRotationY = yAngle;
    }
    
    // Метод для плавной установки углов вращения
    public void SetRotation(float xAngle, float yAngle)
    {
        targetRotationX = Mathf.Clamp(xAngle, verticalMinAngle, verticalMaxAngle);
        targetRotationY = yAngle;
    }
    
    // Получить текущее расстояние
    public float GetCurrentDistance()
    {
        return currentDistance;
    }
    
    // Получить целевое расстояние
    public float GetTargetDistance()
    {
        return targetDistance;
    }
    
    // Получить текущие углы вращения
    public Vector2 GetCurrentRotation()
    {
        return new Vector2(currentRotationX, currentRotationY);
    }
}
