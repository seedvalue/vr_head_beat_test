using UnityEngine;
using UnityEngine.InputSystem;
// Убедитесь, что у вас есть ссылка на UnityEngine.UI, если Slider находится на другом объекте
// using UnityEngine.UI; 

public class PunchController : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("Слой, который будет использоваться для определения ударов")]
    public string targetLayerName = "DamagedNPC";
    
    [Header("References")]
    [Tooltip("Камера, из которой будет пускаться луч")]
    public Camera playerCamera;

    [Header("Force Settings")]
    [Tooltip("Текущая сила удара (0-1)")]
    [Range(0f, 1f)]
    public float normalizedForce = 0.5f; // Значение по умолчанию 0.5 (50%)
    [Tooltip("Шаг изменения силы за одно движение колеса мыши")]
    public float forceStep = 0.05f; // Меньший шаг для более плавного изменения

    [Header("UI")]
    [Tooltip("Ссылка на UI Slider для отображения силы")]
    public UnityEngine.UI.Slider forceSliderUI; // Ссылка на слайдер в инспекторе

    private int targetLayerMask;
    
    private void Start()
    {
        // Получаем индекс слоя и создаем маску для рейкаста
        int targetLayer = LayerMask.NameToLayer(targetLayerName);
        if (targetLayer == -1)
        {
            Debug.LogError($"Слой с именем \"{targetLayerName}\" не найден! Проверьте настройки слоев.");
            enabled = false; // Отключаем компонент, если слой не найден
            return;
        }
        
        // Создаем маску слоя (1 << targetLayer означает "только этот слой")
        targetLayerMask = 1 << targetLayer;
        
        // Если у вас нет ссылки на камеру, пробуем получить основную камеру
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
        
        if (playerCamera == null)
        {
            Debug.LogError("Камера не назначена и не найдена основная камера!");
            enabled = false;
            return;
        }

        // Инициализируем UI
        UpdateForceUI();
    }

    private void Update()
    {
        // Проверяем нажатие левой кнопки мыши
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPunch();
        }

        // Проверяем движение колеса мыши для изменения силы
        if (Mouse.current != null)
        {
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (scroll != 0)
            {
                ChangeForce(scroll);
            }
        }
    }

    /// <summary>
    /// Изменяет нормализованную силу удара в зависимости от движения колеса мыши.
    /// </summary>
    /// <param name="scrollDelta">Значение прокрутки колеса мыши (положительное - вверх, отрицательное - вниз).</param>
    private void ChangeForce(float scrollDelta)
    {
        // Определяем направление изменения силы
        float direction = Mathf.Sign(scrollDelta);
        // Изменяем нормализованную силу
        normalizedForce += direction * forceStep;
        // Ограничиваем силу в диапазоне 0-1
        normalizedForce = Mathf.Clamp01(normalizedForce);
        
        // Выводим новое значение силы в консоль
        Debug.Log($"Normalized Punch Force changed to: {normalizedForce:F2}");

        // Обновляем UI напрямую
        UpdateForceUI();
    }

    /// <summary>
    /// Обновляет значение на UI слайдере, если он назначен.
    /// </summary>
    private void UpdateForceUI()
    {
        // Если слайдер назначен в инспекторе, обновляем его значение напрямую
        if (forceSliderUI != null)
        {
            forceSliderUI.value = normalizedForce;
        }
    }

    /// <summary>
    /// Пытается выполнить "удар" - пускает луч из камеры и проверяет пересечение с объектами на слое DamagedNPC
    /// </summary>
    private void TryPunch()
    {
        // Получаем позицию курсора
        Vector2 mousePosition = Mouse.current.position.ReadValue();
        // Создаем луч из камеры через позицию курсора
        var ray = playerCamera.ScreenPointToRay(mousePosition);
        
        // Выполняем рейкаст только по объектам на слое DamagedNPC
        if (Physics.Raycast(ray, out RaycastHit hit, Mathf.Infinity, targetLayerMask))
        {
            // Получаем UV-координаты из информации о пересечении
            var hitUV = hit.textureCoord;
            // Выводим координаты UV и силу в консоль
            Debug.Log($"Попадание! UV координаты: {hitUV}, Нормализованная сила удара: {normalizedForce:F2}");
            Debug.Log($"Объект: {hit.collider.name}, Точка попадания: {hit.point}");
            
            var transRoot = hit.transform.root;
            // Предполагая, что компонент называется DamageTexture
            if (transRoot.TryGetComponent<DamagedNPC>(out var component)) 
            {
                // Передаем рассчитанные UV и текущую нормализованную силу
                component.ApplyDamage(hitUV, normalizedForce);
            }
            else
            {
                Debug.LogError($"PunchController : TryPunch : DamageTexture component not found on {transRoot.gameObject.name}");
            }
        }
        else
        {
            // Необязательно, но может быть полезно для отладки
            // Debug.Log("Промах - луч не пересек объект на слое DamagedNPC");
        }
    }
}