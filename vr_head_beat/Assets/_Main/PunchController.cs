using UnityEngine;
using UnityEngine.InputSystem;

public class PunchController : MonoBehaviour
{
    [Header("Raycast Settings")]
    [Tooltip("Слой, который будет использоваться для определения ударов")]
    public string targetLayerName = "DamagedNPC";
    
    [Header("References")]
    [Tooltip("Камера, из которой будет пускаться луч")]
    public Camera playerCamera;

    
    [SerializeField] float force = 1.0f;
    
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
    }

    private void Update()
    {
        // Проверяем нажатие левой кнопки мыши
        if (Mouse.current != null && Mouse.current.leftButton.wasPressedThisFrame)
        {
            TryPunch();
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
            // Выводим координаты UV в консоль
            Debug.Log($"Попадание! UV координаты: {hitUV}");
            Debug.Log($"Объект: {hit.collider.name}, Точка попадания: {hit.point}");
            var transRoot = hit.transform.root;
            if (transRoot.TryGetComponent<DamagedNPC>(out var component))
            {
                component.ApplyDamage(hitUV, force);
            }
            else
            {
                Debug.LogError($"PunchController : TryPunch : trans.TryGetComponent<DamagedNPC> NULL : {transRoot.gameObject.name}");
            }
        }
        else
        {
            // Необязательно, но может быть полезно для отладки
            // Debug.Log("Промах - луч не пересек объект на слое DamagedNPC");
        }
    }
}