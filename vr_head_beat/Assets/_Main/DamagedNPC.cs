using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamagedNPC : MonoBehaviour
{
    [SerializeField] private DamageTexture damageTexture;
    [SerializeField] private DamageRagdoll damageRagdoll;
    [SerializeField] private DamageSound damageSound;
    
    public void ApplyDamage(Vector3 hitPoint, Vector3 hitDirection, Vector2 uvCoordinate, float force = 1.0f)
    {
        Debug.Log($"DamagedNPC: ApplyDamage : uvCoordinate: {uvCoordinate}, force: {force}"); 
        
        if(damageTexture)damageTexture.DrawDamage(uvCoordinate, force);
        else Debug.LogError("damageTexture NULL");
        
        if(damageRagdoll)damageRagdoll.ApplyDamage(hitPoint, hitDirection, uvCoordinate, force);
        else Debug.LogError("damageRagdoll NULL");

        if(damageSound)damageSound.ApplyDamage(force);
        else Debug.LogError("damageSound NULL");
        
        
        
        // Рассчитать размер кисти на основе силы, если нужно
       // float currentBrushSize = CalculateBrushSize(force); // Ваша логика из предыдущего ответа

        // Вызвать DrawToMaskBlitMaterial с рассчитанным размером
       // DrawToMaskBlitMaterial(uvCoordinate, currentBrushSize); // Обновите сигнатуру метода
    }
    
}
