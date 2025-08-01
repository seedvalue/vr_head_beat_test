using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DamageTexture : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private Renderer targetRenderer; // Материал на меше, с слоями повреждений
    
    // Для блита внутри GPU сетап из проекта не из сцены
    [SerializeField] Material blitMaterial; // материал с Hidden/BlitDraw шейдером
    
    [Header("Brush Painter")]
    [Tooltip("Ширина RenderTexture повреждений (например, 512, 1024, 2048)")]
    public int renderTextureWidth = 1024;
    [Tooltip("Высота RenderTexture повреждений (например, 512, 1024, 2048)")]
    public int renderTextureHeight = 1024;
    [Tooltip("Формат RenderTexture. ARGB32 подходит для большинства случаев.")]
    public RenderTextureFormat renderTextureFormat = RenderTextureFormat.ARGB32;
    
    public float brushSize = 0.1f;
    public Texture2D brushTexture2D;
    [Range(0F,1F)]
    public float brushStrength = 1.0f; // альфа при одном ударе
    
    [Header("UI test (optional)")]
    [SerializeField]
    private RawImage rawImageTestPainter;

    // RenderTexture для маски повреждений, теперь приватная
    private RenderTexture damageMask;
    
    /// <summary>
    /// Создает или пересоздает RenderTexture для маски повреждений.
    /// </summary>
    private void CreateDamageMask()
    {
        // Освобождаем предыдущую текстуру, если она была
        if (damageMask != null)
        {
            DestroyImmediate(damageMask); // Используем DestroyImmediate в редакторе/старте
            // или RenderTexture.ReleaseTemporary(damageMask) если она была временной
            // или damageMask.Release() если она была постоянной, но мы хотим пересоздать
        }

        // Создаем новую RenderTexture
        damageMask = new RenderTexture(renderTextureWidth, renderTextureHeight, 0, renderTextureFormat);
        // Важно: Устанавливаем имя для отладки и фильтр, если нужно
        damageMask.name = $"{gameObject.name}_DamageMask";
        damageMask.filterMode = FilterMode.Bilinear; // Или Point, в зависимости от желаемого эффекта
        damageMask.wrapMode = TextureWrapMode.Clamp;

        Debug.Log($"Created DamageMask RenderTexture: {damageMask.width}x{damageMask.height}, Format: {damageMask.format}");
    }

    /// <summary>
    /// Инициализирует маску повреждений: очищает её и назначает материалу.
    /// </summary>
    private void InitDamageMask()
    {
        Debug.Log("InitDamageMask");

        if (targetRenderer == null || targetRenderer.material == null)
        {
            Debug.LogWarning("Target renderer or its material is null!");
            return;
        }

        if (damageMask == null)
        {
            Debug.LogWarning("Damage mask is null! It should have been created in Start().");
            return;
        }
        
        Debug.Log($"RandomWrite support: {SystemInfo.copyTextureSupport}"); // Check support
        
        // Назначаем созданную RenderTexture материалу объекта
        targetRenderer.material.SetTexture("_DamageMask", damageMask);
        
        // Очищаем её, заполняя черным цветом (предполагается, что черный = нет повреждений)
        ClearRenderTexture(damageMask, Color.black);
    }
    
    /// <summary>
    /// Очищает RenderTexture заданным цветом.
    /// </summary>
    private void ClearRenderTexture(RenderTexture rt, Color clearColor)
    {
        Debug.Log($"Clear RenderTexture: {rt.name}");
        RenderTexture currentRT = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, clearColor);
        RenderTexture.active = currentRT; // Восстанавливаем предыдущий активный RT
    }

    /// <summary>
    /// Рисует повреждение в заданной UV-координате.
    /// </summary>
    public void DrawDamage(Vector2 uvCoordinate, float force = 1.0f)
    {
        Debug.Log($"DamageTexture : UV {uvCoordinate}, force: {force}");
        if (damageMask == null || blitMaterial == null)
        {
            Debug.LogWarning("Cannot draw damage: damageMask or blitMaterial is null.");
            return;
        }
        
        var calculatedForce = force * brushStrength;
        DrawToMaskBlitMaterial(uvCoordinate, calculatedForce);
    }
    
    /// <summary>
    /// Выполняет отрисовку кисти в RenderTexture с помощью Graphics.Blit.
    /// </summary>
    private void DrawToMaskBlitMaterial(Vector2 uv, float calculatedForce)
    {
        Debug.Log($"DrawToMask : {uv}");
        
        // Передаем параметры в материал для отрисовки
        blitMaterial.SetTexture("_BrushTex", brushTexture2D);
        blitMaterial.SetVector("_BrushUV", uv);
        blitMaterial.SetFloat("_BrushSize", brushSize);
        blitMaterial.SetFloat("_BrushStrength", calculatedForce);

        // Создаем временную RenderTexture для промежуточного результата
        var tmp = RenderTexture.GetTemporary(damageMask.width, damageMask.height, 0, damageMask.format);
        
        // Копируем текущее содержимое damageMask во временную текстуру
        Graphics.Blit(damageMask, tmp);
        
        // Рисуем новое повреждение из временной текстуры в основную damageMask, используя blitMaterial
        Graphics.Blit(tmp, damageMask, blitMaterial);
        
        // Для отладки: отображаем промежуточный результат (можно убрать)
        if (rawImageTestPainter != null)
        {
            rawImageTestPainter.texture = tmp;
        }
        
        // Освобождаем временную текстуру
        RenderTexture.ReleaseTemporary(tmp);
    }
    
    void Start()
    {
        // 1. Создаем RenderTexture
        CreateDamageMask();
        // 2. Инициализируем её (очищаем и назначаем материалу)
        InitDamageMask();
    }

    // Рекомендуется освобождать RenderTexture при уничтожении объекта, чтобы избежать утечек памяти
    private void OnDestroy()
    {
        if (damageMask != null)
        {
            Debug.Log($"Releasing DamageMask RenderTexture: {damageMask.name}");
            DestroyImmediate(damageMask); // В редакторе
            // Или damageMask.Release(); Destroy(damageMask); в игре, в зависимости от контекста
            // Или RenderTexture.ReleaseTemporary если она была временной (но она постоянная)
            damageMask = null;
        }
    }
}