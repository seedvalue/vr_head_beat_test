using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class RagdollAnimatorLinker : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Animator sourceAnimator; // Укажи в инспекторе (может быть на дочернем объекте)

    [Header("Settings")]
    [Range(0f, 1f)] [SerializeField] private float globalInfluence = 1f;
    [SerializeField] private bool copyRootMotion = true;

    [Header("Per-Bone Influence")]
    [SerializeField] private List<BoneInfluence> boneInfluences = new List<BoneInfluence>();

    [Header("Debug")]
    [SerializeField] private bool autoSetupOnStart = true;

    // Runtime
    private GameObject animatedNPC;
    private Animator animatedNPCAnimator;
    private Dictionary<Transform, Transform> boneMapping = new Dictionary<Transform, Transform>();
    private Dictionary<Transform, Rigidbody> ragdollRigidbodies = new Dictionary<Transform, Rigidbody>();
    private HumanPoseHandler? sourcePoseHandler;
    private HumanPose humanPose;

    [System.Serializable]
    public class BoneInfluence
    {
        public HumanBodyBones bone;
        [Range(0f, 1f)] public float influence = 1f;
    }

    private void Start()
    {
        if (autoSetupOnStart)
            Setup();
    }

    public void Setup()
    {
        // Найдём Animator, если не назначен
        if (sourceAnimator == null)
        {
            sourceAnimator = GetComponentInChildren<Animator>();
            if (sourceAnimator == null)
            {
                Debug.LogError("[RagdollAnimatorLinker] Не найден Animator в дочерних объектах.", this);
                return;
            }
        }

        if (!sourceAnimator.isHuman || sourceAnimator.avatar == null || !sourceAnimator.avatar.isHuman)
        {
            Debug.LogError("[RagdollAnimatorLinker] Animator должен быть Humanoid и иметь валидный Avatar.", this);
            return;
        }

        CreateAnimatedNPC();
        SetupRagdoll();
        SetupBoneMapping();
        SetupSourcePoseHandler();

        // ✅ Оригинал: рендер включён (ничего не делаем)
        // ❌ Выключаем аниматор на оригинале
        sourceAnimator.enabled = false;

        // ✅ Включаем рагдолл
        EnableRagdoll(true);

        Debug.Log($"[RagdollAnimatorLinker] Настроен. Создан AnimatedNPC: {animatedNPC.name}", this);
    }

    private void CreateAnimatedNPC()
    {
        animatedNPC = new GameObject($"AnimatedNPC_{GetInstanceID()}");
        animatedNPC.transform.SetPositionAndRotation(transform.position, transform.rotation);
        animatedNPC.transform.localScale = transform.localScale;

        animatedNPCAnimator = animatedNPC.AddComponent<Animator>();
        animatedNPCAnimator.avatar = sourceAnimator.avatar;
        animatedNPCAnimator.runtimeAnimatorController = sourceAnimator.runtimeAnimatorController;
        animatedNPCAnimator.applyRootMotion = false; // не двигаем позицию
        animatedNPCAnimator.enabled = true;

        // Отключаем все рендеры у копии
        var renderers = animatedNPC.GetComponentsInChildren<Renderer>();
        foreach (var r in renderers)
            r.enabled = false;

        Debug.Log("[RagdollAnimatorLinker] AnimatedNPC создан и настроен.", this);
    }

    private void SetupRagdoll()
    {
        ragdollRigidbodies.Clear();
        var rbs = GetComponentsInChildren<Rigidbody>();
        foreach (var rb in rbs)
        {
            ragdollRigidbodies[rb.transform] = rb;
        }

        if (ragdollRigidbodies.Count == 0)
            Debug.LogWarning("[RagdollAnimatorLinker] Не найдено Rigidbody на оригинале.", this);
    }

    private void SetupBoneMapping()
    {
        boneMapping.Clear();

        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var bone = (HumanBodyBones)i;
            var srcBone = sourceAnimator.GetBoneTransform(bone);
            var animBone = animatedNPCAnimator.GetBoneTransform(bone);

            if (srcBone != null && animBone != null)
            {
                boneMapping[animBone] = srcBone;
            }
        }

        if (boneMapping.Count == 0)
        {
            Debug.LogError("[RagdollAnimatorLinker] Не удалось сопоставить ни одной кости. Проверь Avatar (Configure...).", this);
        }
        else
        {
            Debug.Log($"[RagdollAnimatorLinker] Сопоставлено {boneMapping.Count} костей.", this);
        }
    }

    private void SetupSourcePoseHandler()
    {
        if (sourceAnimator.avatar != null && sourceAnimator.avatar.isHuman)
        {
            sourcePoseHandler = new HumanPoseHandler(sourceAnimator.avatar, sourceAnimator.transform);
            humanPose = new HumanPose();
            Debug.Log("[RagdollAnimatorLinker] HumanPoseHandler инициализирован.", this);
        }
        else
        {
            Debug.LogError("[RagdollAnimatorLinker] Не удалось инициализировать HumanPoseHandler: аватар недействителен.", this);
        }
    }

    private void EnableRagdoll(bool enable)
    {
        var rbs = GetComponentsInChildren<Rigidbody>();
        var joints = GetComponentsInChildren<Joint>();

        foreach (var rb in rbs)
            rb.isKinematic = !enable;

        foreach (var joint in joints)
            joint.enableCollision = enable;
    }

    private void LateUpdate()
    {
        // Копируем параметры аниматора с оригинала на AnimatedNPC
        SyncAnimatorParameters();
    }

    private void FixedUpdate()
    {
        if (animatedNPCAnimator == null || sourcePoseHandler == null || !sourceAnimator.isActiveAndEnabled)
            return;

        // Получаем позу из AnimatedNPC
        sourcePoseHandler.GetHumanPose(ref humanPose);

        foreach (var kv in boneMapping)
        {
            var animBone = kv.Key;      // Кость из AnimatedNPC
            var targetBone = kv.Value;  // Кость на оригинале
            var rb = ragdollRigidbodies.TryGetValue(targetBone, out var body) ? body : null;

            if (rb == null) continue;

            float influence = GetBoneInfluence(GetBoneFromTransform(targetBone)) * globalInfluence;
            if (influence <= 0f) continue;

            // Применяем вращение
            rb.MoveRotation(Quaternion.Slerp(rb.rotation, animBone.rotation, influence));

            // Применяем позицию только к Hips, если разрешено
            if (copyRootMotion && targetBone == sourceAnimator.GetBoneTransform(HumanBodyBones.Hips))
            {
                rb.MovePosition(Vector3.Lerp(rb.position, animBone.position, influence));
            }
        }
    }

    private float GetBoneInfluence(HumanBodyBones bone)
    {
        foreach (var bi in boneInfluences)
            if (bi.bone == bone)
                return bi.influence;
        return 1f;
    }

    private HumanBodyBones GetBoneFromTransform(Transform t)
    {
        for (int i = 0; i < (int)HumanBodyBones.LastBone; i++)
        {
            var bone = (HumanBodyBones)i;
            if (sourceAnimator.GetBoneTransform(bone) == t)
                return bone;
        }
        return HumanBodyBones.LastBone;
    }

    // ———————————————————— Синхронизация анимации ————————————————————

    private void SyncAnimatorParameters()
    {
        if (!sourceAnimator.isActiveAndEnabled || !animatedNPCAnimator) return;

        // Копируем параметры
        animatedNPCAnimator.speed = sourceAnimator.speed;

        // Float
        foreach (var param in sourceAnimator.parameters)
        {
            if (param.type == AnimatorControllerParameterType.Float)
                animatedNPCAnimator.SetFloat(param.name, sourceAnimator.GetFloat(param.name));
            else if (param.type == AnimatorControllerParameterType.Int)
                animatedNPCAnimator.SetInteger(param.name, sourceAnimator.GetInteger(param.name));
            else if (param.type == AnimatorControllerParameterType.Bool)
                animatedNPCAnimator.SetBool(param.name, sourceAnimator.GetBool(param.name));
            else if (param.type == AnimatorControllerParameterType.Trigger)
            {
                if (sourceAnimator.GetBool(param.name))
                {
                    animatedNPCAnimator.SetTrigger(param.name);
                    sourceAnimator.ResetTrigger(param.name); // чтобы не накапливалось
                }
            }
        }
    }

    private void OnDestroy()
    {
        if (animatedNPC != null)
            Destroy(animatedNPC);
    }

    // ———————————————————— Public API ————————————————————

    /// <summary> Получить аниматор, который управляет внутренней анимацией (для ручного контроля) </summary>
    public Animator GetAnimatedAnimator() => animatedNPCAnimator;

    /// <summary> Установить глобальное влияние анимации на рагдолл (0–1) </summary>
    public void SetGlobalInfluence(float value) => globalInfluence = Mathf.Clamp01(value);

    /// <summary> Установить влияние на конкретную кость </summary>
    public void SetBoneInfluence(HumanBodyBones bone, float value)
    {
        value = Mathf.Clamp01(value);
        for (int i = 0; i < boneInfluences.Count; i++)
        {
            if (boneInfluences[i].bone == bone)
            {
                boneInfluences[i].influence = value;
                return;
            }
        }
        boneInfluences.Add(new BoneInfluence { bone = bone, influence = value });
    }
}