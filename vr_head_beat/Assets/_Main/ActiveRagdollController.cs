using UnityEngine;
using System.Collections.Generic;
using System.Linq; // Для OrderBy

[RequireComponent(typeof(Animator))]
public class ActiveRagdollController : MonoBehaviour
{
    [Header("Ragdoll Settings")] [Tooltip("Ссылка на корневой объект Ragdoll (обычно это этот же объект)")]
    public Transform ragdollRoot;

    [Tooltip("Множитель силы удара")] public float hitForceMultiplier = 1.0f;

    [Tooltip("Минимальная сила удара (когда normalizedForce = 0)")]
    public float minHitForce = 50.0f; // Увеличил для более заметного эффекта

    [Tooltip("Максимальная сила удара (когда normalizedForce = 1)")]
    public float maxHitForce = 500.0f; // Увеличил для более заметного эффекта

    [Tooltip("Время, в течение которого influence распространяется и затухает (в секундах)")]
    public float influenceActiveTime = 1.0f;

    [Tooltip("Скорость, с которой influence возвращается к 0")]
    public float influenceRecoverySpeed = 2.0f;

    [Tooltip("Как быстро influence распространяется на соседние кости")]
    public float influenceSpreadSpeed = 5.0f;

    [Tooltip("Максимальное расстояние, на которое influence может распространиться")]
    public float maxInfluenceSpreadDistance = 2.0f;

    [Header("Body Parts")] [Tooltip("Список костей Ragdoll с их настройками")]
    public List<RagdollBone> ragdollBones = new List<RagdollBone>();

    private Animator animator;
    private bool isAnyInfluenceActive = false;
    private float influenceTimer = 0f;

    // Для поиска соседей костей
    private Dictionary<RagdollBone, List<RagdollBone>> boneNeighbors;

    [System.Serializable]
    public class RagdollBone
    {
        [HideInInspector] public string name;
        public Transform boneTransform;
        public Rigidbody boneRigidbody;
        [Range(0f, 1f)] public float influence = 0f; // 0 = 100% анимация, 1 = 100% физика
        [HideInInspector] public Vector3 lastAnimatedPosition;
        [HideInInspector] public Quaternion lastAnimatedRotation;
        [HideInInspector] public float targetInfluence = 0f; // К какому значению стремится influence
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        animator.updateMode = AnimatorUpdateMode.AnimatePhysics;
        //animator.enabled = false; // полностью выключить


        if (ragdollRoot == null) ragdollRoot = transform;

        // Инициализация Ragdoll костей
        InitializeRagdollBones();

        // Находим соседей для каждой кости
        FindBoneNeighbors();

        // Убедимся, что Ragdoll изначально "выключен" (физика не контролирует кости)
        ResetAllInfluences();
    }

    private void OnEnable()
    {
        // Убедимся, что Animator включен
        //   if (animator != null)
        //     animator.enabled = true;
    }

    private void LateUpdate()
    {
        // 1. Сначала сохраняем "чистые" позиции из аниматора ДО смешивания или изменения influence
        foreach (var bone in ragdollBones)
        {
            if (bone.boneTransform != null)
            {
                // Сохраняем оригинальную позицию из аниматора до применения Blend
                bone.lastAnimatedPosition = bone.boneTransform.position;
                bone.lastAnimatedRotation = bone.boneTransform.rotation;
            }
        }

        // 2. Затем обновляем influence
        UpdateInfluences();

        // 3. После этого выполняем смешивание
        BlendAnimationAndPhysics();

        // Логирование influence (по желанию)
        // foreach (var bone in ragdollBones)
        // {
        //     Debug.Log($"Bone: {bone.name}, influence: {bone.influence:F2}");
        // }
    }
    

    private void FixedUpdate()
    {
        // Сбрасываем скорости у костей, которые полностью под контролем анимации
        // Это помогает избежать "дрift" из-за физики, когда influence = 0
        foreach (var bone in ragdollBones)
        {
            if (bone.boneRigidbody != null && bone.influence <= 0.01f)
            {
                bone.boneRigidbody.velocity = Vector3.zero;
                bone.boneRigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Инициализация костей Ragdoll
    /// </summary>
    private void InitializeRagdollBones()
    {
        // Если список пуст, попытаемся автоматически найти кости
        if (ragdollBones.Count == 0)
        {
            FindRagdollBones();
        }

        foreach (var bone in ragdollBones)
        {
            if (bone.boneRigidbody != null)
            {
                // Сделаем Rigidbody НЕ кинематическим, но будем контролировать их через influence
                bone.boneRigidbody.isKinematic = false;
                bone.boneRigidbody.useGravity = true; // Гравитация всегда включена
                // Начальные скорости нулевые
                bone.boneRigidbody.velocity = Vector3.zero;
                bone.boneRigidbody.angularVelocity = Vector3.zero;
            }
        }
    }

    /// <summary>
    /// Автоматический поиск костей Ragdoll
    /// </summary>
    private void FindRagdollBones()
    {
        // Получаем все Rigidbody в дочерних объектах
        Rigidbody[] rigidbodies = ragdollRoot.GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in rigidbodies)
        {
            RagdollBone bone = new RagdollBone
            {
                name = rb.name,
                boneTransform = rb.transform,
                boneRigidbody = rb
            };
            ragdollBones.Add(bone);
        }

        Debug.Log($"Найдено {ragdollBones.Count} костей Ragdoll");
    }

    /// <summary>
    /// Находит соседние кости для каждой кости (для распространения influence)
    /// </summary>
    private void FindBoneNeighbors()
    {
        boneNeighbors = new Dictionary<RagdollBone, List<RagdollBone>>();

        foreach (var bone in ragdollBones)
        {
            List<RagdollBone> neighbors = new List<RagdollBone>();

            foreach (var otherBone in ragdollBones)
            {
                if (bone != otherBone)
                {
                    float distance = Vector3.Distance(
                        bone.boneTransform.position,
                        otherBone.boneTransform.position
                    );

                    // Если расстояние между костями меньше максимального расстояния распространения
                    if (distance <= maxInfluenceSpreadDistance)
                    {
                        neighbors.Add(otherBone);
                    }
                }
            }

            // Сортируем соседей по расстоянию (ближайшие первыми)
            neighbors = neighbors.OrderBy(b =>
                Vector3.Distance(bone.boneTransform.position, b.boneTransform.position)
            ).ToList();

            boneNeighbors[bone] = neighbors;
        }
    }

    /// <summary>
    /// Сбрасывает influence для всех костей
    /// </summary>
    private void ResetAllInfluences()
    {
        foreach (var bone in ragdollBones)
        {
            bone.influence = 0f;
            bone.targetInfluence = 0f;
        }

        isAnyInfluenceActive = false;
        influenceTimer = 0f;
    }

    /// <summary>
    /// Обновляет influence для всех костей
    /// </summary>
    private void UpdateInfluences()
    {
        Debug.Log($"UpdateInfluences: timer={influenceTimer}, active={isAnyInfluenceActive}");
        var anyActive = false;

        // Если influence активен и время вышло — сбрасываем targetInfluence
        if (isAnyInfluenceActive && influenceTimer >= influenceActiveTime)
        {
            foreach (var bone in ragdollBones)
            {
                bone.targetInfluence = 0f;
            }
        }

        // Обновляем influence для каждой кости
        foreach (var bone in ragdollBones)
        {
            bone.influence = Mathf.MoveTowards(
                bone.influence,
                bone.targetInfluence,
                Time.deltaTime * influenceRecoverySpeed
            );

            if (bone.influence > 0.01f)
            {
                anyActive = true;
            }
        }

        isAnyInfluenceActive = anyActive;

        if (isAnyInfluenceActive)
        {
            influenceTimer += Time.deltaTime;
        }
        else
        {
            influenceTimer = 0f;
        }
    }


    
    /// <summary>
    /// Смешивает анимацию и физику для каждой кости
    /// </summary>
    private void BlendAnimationAndPhysics()
    {
        foreach (var bone in ragdollBones)
        {
            if (bone.boneTransform == null || bone.boneRigidbody == null) continue;
        
            // Позиции уже сохранены в LateUpdate до этого метода

            // Полностью физика
            if (bone.influence >= 0.99f)
            {
                // Ничего не делаем — пусть физика управляет через PhysX
                // Rigidbody не isKinematic, поэтому Transform будет обновлен PhysX
                continue;
            }

            // Полностью анимация
            if (bone.influence <= 0.01f)
            {
                // Устанавливаем Transform в позицию из аниматора (сохраненную ранее)
                bone.boneTransform.position = bone.lastAnimatedPosition;
                bone.boneTransform.rotation = bone.lastAnimatedRotation;
                // НЕ устанавливаем позицию Rigidbody здесь. Это может конфликтовать с физикой.
                // Сброс скорости уже происходит в FixedUpdate.
                continue;
            }

            // Смешиваем
            bone.boneTransform.position = Vector3.Lerp(
                bone.lastAnimatedPosition,      // Позиция из аниматора (сохраненная в начале LateUpdate)
                bone.boneRigidbody.position,    // Позиция из физики (управляемая Rigidbody)
                bone.influence
            );
            bone.boneTransform.rotation = Quaternion.Lerp(
                bone.lastAnimatedRotation,      // Вращение из аниматора (сохраненное в начале LateUpdate)
                bone.boneRigidbody.rotation,    // Вращение из физики (управляемое Rigidbody)
                bone.influence
            );
        }
    }

    /// <summary>
    /// Применение силы удара к Ragdoll
    /// </summary>
    /// <param name="hitPoint">Точка удара</param>
    /// <param name="normalizedForce">Нормализованная сила удара (от 0 до 1)</param>
    /// <param name="hitDirection">Направление удара</param>
    public void ApplyHitForce(Vector3 hitPoint, float normalizedForce, Vector3 hitDirection)
    {
        // Рассчитываем абсолютную силу удара
        float hitForce = Mathf.Lerp(minHitForce, maxHitForce, normalizedForce);

        // Находим ближайшую кость к точке удара
        RagdollBone nearestBone = FindNearestBone(hitPoint);
        if (nearestBone != null && nearestBone.boneRigidbody != null)
        {
            // Применяем силу к ближайшей кости
            Vector3 force = hitDirection * hitForce * hitForceMultiplier;
            nearestBone.boneRigidbody.AddForceAtPosition(force, hitPoint, ForceMode.Impulse);
            
            Debug.Log($"FORCE: {force.magnitude} N  |  mass: {nearestBone.boneRigidbody.mass}  |  isKinematic: {nearestBone.boneRigidbody.isKinematic}");
            

            // Устанавливаем influence для ближайшей кости
            nearestBone.targetInfluence = 1.0f; // Полный контроль физики
            Debug.Log($"ApplyHitForce : Bone: {nearestBone.name}, influence: {nearestBone.influence}");
            // Распространяем influence на соседние кости
            SpreadInfluence(nearestBone, hitForce);

            Debug.Log(
                $"ApplyHitForce : Сила применена к кости: {nearestBone.name}, норм. сила: {normalizedForce:F2}, абс. сила: {hitForce:F2}, вектор силы: {force}");
        }
    }

    /// <summary>
    /// Распространяет influence на соседние кости
    /// </summary>
    /// <param name="hitBone">Кость, по которой нанесли удар</param>
    /// <param name="hitForce">Сила удара</param>
    private void SpreadInfluence(RagdollBone hitBone, float hitForce)
    {
        Debug.Log($"SpreadInfluence : SpreadInfluence : started from {hitBone.name}, hitForce={hitForce}");
        // Создаем очередь для обхода соседей
        Queue<RagdollBone> queue = new Queue<RagdollBone>();
        Dictionary<RagdollBone, float> distances = new Dictionary<RagdollBone, float>();

        // Начинаем с кости, по которой ударили
        queue.Enqueue(hitBone);
        distances[hitBone] = 0f;

        while (queue.Count > 0)
        {
            RagdollBone currentBone = queue.Dequeue();
            float currentDistance = distances[currentBone];

            // Если мы вышли за пределы максимального расстояния, прекращаем
            if (currentDistance > maxInfluenceSpreadDistance)
                continue;

            // Рассчитываем influence для текущей кости на основе расстояния
            float distanceFactor = 1.0f - (currentDistance / maxInfluenceSpreadDistance);
            // Учитываем силу удара
            float forceFactor = Mathf.InverseLerp(minHitForce, maxHitForce, hitForce);
            float finalInfluence = distanceFactor * forceFactor;

            // Устанавливаем targetInfluence, если он больше текущего
            if (finalInfluence > currentBone.targetInfluence)
            {
                currentBone.targetInfluence = finalInfluence;
            }

            // Добавляем соседей в очередь
            if (boneNeighbors.TryGetValue(currentBone, out var boneNeighbor))
            {
                foreach (var neighbor in boneNeighbor)
                {
                    float distanceToNeighbor = Vector3.Distance(
                        currentBone.boneTransform.position,
                        neighbor.boneTransform.position
                    );

                    var newDistance = currentDistance + distanceToNeighbor;

                    // Если мы еще не посещали этого соседа или нашли более короткий путь
                    if (!distances.ContainsKey(neighbor) || newDistance < distances[neighbor])
                    {
                        // Проверяем, не вышли ли мы за пределы максимального расстояния
                        if (newDistance <= maxInfluenceSpreadDistance)
                        {
                            distances[neighbor] = newDistance;
                            queue.Enqueue(neighbor);
                        }
                    }
                }
            }
        }

        // Активируем систему influence
        isAnyInfluenceActive = true;
        influenceTimer = 0f;
        Debug.Log($"SpreadInfluence : Bone: {hitBone.name}, influence: {hitBone.influence}");

        foreach (var bone in ragdollBones)
        {
            Debug.Log($"SpreadInfluence : After spread: {bone.name} targetInfluence = {bone.targetInfluence}");
        }
    }

    /// <summary>
    /// Нахождение ближайшей кости к точке
    /// </summary>
    private RagdollBone FindNearestBone(Vector3 point)
    {
        RagdollBone nearestBone = null;
        float minDistance = Mathf.Infinity;

        foreach (var bone in ragdollBones)
        {
            if (bone.boneTransform != null)
            {
                float distance = Vector3.Distance(bone.boneTransform.position, point);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    nearestBone = bone;
                }
            }
        }

        return nearestBone;
    }

    /// <summary>
    /// Получение текущего состояния influence
    /// </summary>
    public bool IsInfluenceActive()
    {
        return isAnyInfluenceActive;
    }

    /// <summary>
    /// Немедленная деактивация всех influence
    /// </summary>
    public void ForceResetInfluence()
    {
        ResetAllInfluences();
    }
}