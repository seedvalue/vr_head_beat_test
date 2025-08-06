using UnityEngine;


public class DamageRagdoll : MonoBehaviour
{
    [Tooltip("Множитель силы импульса.")]
    public float impactMultiplier = 50f;

    private Rigidbody[] ragdollBodies;

    private void Awake()
    {
        // Собираем все Rigidbody Ragdoll-скелета
        ragdollBodies = GetComponentsInChildren<Rigidbody>();
    }

    /// <summary>
    /// Вызывать из PunchController.ApplyDamage напрямую.
    /// </summary>
    public void ApplyDamage(Vector3 hitPoint, Vector3 hitDirection, Vector2 uv, float force)
    {
        // Находим ближайший коллайдер Ragdoll
        Collider closest = null;
        float minDist = float.MaxValue;

        foreach (var rb in ragdollBodies)
        {
            if (!rb.TryGetComponent(out Collider c)) continue;

            float d = Vector3.Distance(c.ClosestPoint(hitPoint), hitPoint);
            if (d < minDist)
            {
                minDist = d;
                closest = c;
            }
        }

        // Прикладываем силу
        if (closest != null && closest.attachedRigidbody != null)
        {
            Vector3 impulse = hitDirection.normalized * force * impactMultiplier;
            closest.attachedRigidbody.AddForceAtPosition(impulse, hitPoint, ForceMode.Impulse);
        }
    }
}