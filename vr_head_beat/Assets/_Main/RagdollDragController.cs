using UnityEngine;


public class RagdollDragController : MonoBehaviour
{
    [Header("Параметры всех костей")]
    [Range(0, 10)] public float drag          = 1.0f;
    [Range(0, 10)] public float angularDrag   = 1.0f;

    private Rigidbody[] bodies;

    private void Awake()
    {
        // Собираем все Rigidbody Ragdoll
        bodies = GetComponentsInChildren<Rigidbody>();
        Apply();                // первоначальная установка
    }

    private void OnValidate()
    {
        // Работает и в Play-режиме: изменил ползунок → сразу применилось
        if (bodies != null) Apply();
    }

    /// <summary>
    /// Применить drag и angularDrag ко всем костям.
    /// </summary>
    [ContextMenu("Apply Now")]
    public void Apply()
    {
        foreach (var rb in bodies)
        {
            rb.drag         = drag;
            rb.angularDrag  = angularDrag;
        }
    }
}