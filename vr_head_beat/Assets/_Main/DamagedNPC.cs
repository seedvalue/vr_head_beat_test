using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamagedNPC : MonoBehaviour
{
    [SerializeField] private DamageTexture damageTexture;
    
    
    public void ApplyDamage(Vector2 uvCoordinate, float force = 1.0f)
    {
        Debug.Log($"DamagedNPC: ApplyDamage : uvCoordinate: {uvCoordinate}, force: {force}");
        damageTexture.DrawDamage(uvCoordinate, force);
        
        
        // Рассчитать размер кисти на основе силы, если нужно
       // float currentBrushSize = CalculateBrushSize(force); // Ваша логика из предыдущего ответа

        // Вызвать DrawToMaskBlitMaterial с рассчитанным размером
       // DrawToMaskBlitMaterial(uvCoordinate, currentBrushSize); // Обновите сигнатуру метода
    }
    
}
