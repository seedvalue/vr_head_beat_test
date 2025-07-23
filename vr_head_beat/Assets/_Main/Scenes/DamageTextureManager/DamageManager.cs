using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DamageManager : MonoBehaviour
{
    /// <summary>
    /// Маска повреждения, RenderTexture - скрипт применяет ее после редактирования Paint шейдера
    /// отправдляет в Бленд шейдер, который применяет слои в завимости от плотности белого цвета.
    /// </summary>
    
    [Header("Слой")]
    [Tooltip("Имя слоя, по которому можно рисовать")]
    public string targetLayerName = "DamagedNPC";
    private int targetLayer; 
    
    public RenderTexture damageMask;

    [Header("Brush Painter")]
    public float brushSize = 0.1f;
    public Material blitMaterial; // материал с Hidden/BlitDraw шейдером
    public Material painterMaterial;
    public Texture2D brushTexture2D;
    [Range(0F,1F)]
    public float brushStrength = 1.0f; //альфа при одном ударе
    
   
    
    [SerializeField] private Renderer targetRenderer; //Материал на меше, с слоями повреждений
    
    [Header("Target Mesh")]
   
    public Camera sceneCamera; //Для лучей на фигуре

    

    [Header("Input Action")]
    public InputActionReference clickAction; //Для теста кликов по модельке в Едиторе мышкой
    
    [Header("Ui test:")]
    [SerializeField]
    private RawImage rawImageTestPainter;
    
    
    private void InitDamageMask()
    {
        Debug.Log("InitDamageMask");
        if (targetRenderer == null || targetRenderer.material == null)
        {
            Debug.LogWarning("Target renderer is null! || targetRenderer.material.");
            return;
        }

        if (damageMask == null)
        {
            Debug.LogWarning("Damage mask is null!");
            return;
        }
        Debug.Log($"RandomWrite support: {SystemInfo.copyTextureSupport}"); //Check support
        targetRenderer.material.SetTexture("_DamageMask", damageMask);
        ClearRenderTexture(damageMask, Color.black);
    }
    
    private void ClearRenderTexture(RenderTexture rt, Color clearColor)
    {
        Debug.Log("Clear RenderTexture");
        RenderTexture current = RenderTexture.active;
        RenderTexture.active = rt;
        GL.Clear(true, true, clearColor);
        RenderTexture.active = current;
    }
    
    
    void DrawToMaskBlitMaterial(Vector2 uv)
    {
        Debug.Log($"DrawToMask : {uv}");
        blitMaterial.SetTexture("_BrushTex", brushTexture2D);
        blitMaterial.SetVector("_BrushUV", uv);
        blitMaterial.SetFloat("_BrushSize", brushSize);
        blitMaterial.SetFloat("_BrushStrength", brushStrength);

        var tmp = RenderTexture.GetTemporary(damageMask.width, damageMask.height, 0, damageMask.format);
        Graphics.Blit(damageMask, tmp);
        Graphics.Blit(tmp, damageMask, blitMaterial);
        rawImageTestPainter.texture = tmp;
        RenderTexture.ReleaseTemporary(tmp);
    }
    
    /*
     //TODO применить расчитать силу но в методе повыше, а не там где рисуется. в рисовалку передать готовый радиус кисти
       
        [Header("Force Settings")]
    public float minForce = 1f;
    public float maxForce = 10f;
    
       var normalizedForce = Mathf.InverseLerp(minForce, maxForce, force);
        
        paintMaterial.SetFloat("_Radius", brushSize / damageMask.width);
     
     
     */
    
    
    
    void PaintDamageAtMouse()
    {
        Debug.Log("PaintDamageAtMouse");
        // Получаем позицию курсора/касания
        var screenPos = Mouse.current.position.ReadValue();
        Ray ray = sceneCamera.ScreenPointToRay(screenPos);
        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            // Проверяем слой, а не конкретный Transform
            if (hit.collider.gameObject.layer != targetLayer)  return;
            Vector2 uv = hit.textureCoord;
            DrawToMaskBlitMaterial(uv);
        }
    }
    
    
    void Start()
    {
        InitDamageMask();
        targetLayer = LayerMask.NameToLayer(targetLayerName);
        if (targetLayer == -1) Debug.LogError($"Слой \"{targetLayerName}\" не найден!");
    }

    void Update()
    {
        if (clickAction.action.WasPressedThisFrame())
        {
            Debug.Log("click action pressed");
            PaintDamageAtMouse();
        }
    }
    
    void OnEnable()
    {
        clickAction?.action.Enable();
    }

    void OnDisable()
    {
        clickAction?.action.Disable();
    }
}
