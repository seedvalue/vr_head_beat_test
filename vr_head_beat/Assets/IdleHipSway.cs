using UnityEngine;

public class IdleHipSway : MonoBehaviour
{
    [Header("Настройки бедра")]
    [Tooltip("Выберите бедро (например, LeftUpperLeg или RightUpperLeg)")]
    public Transform hipJoint;

    [Header("Параметры покачивания")]
    [Tooltip("Амплитуда покачивания позиции (в метрах)")]
    public Vector3 positionAmplitude = new Vector3(0.005f, 0.003f, 0.002f);

    [Tooltip("Амплитуда вращения (в градусах)")]
    public Vector3 rotationAmplitude = new Vector3(1.5f, 2.0f, 1.0f);

    [Tooltip("Скорость покачивания")]
    public float frequency = 1.0f;

    [Tooltip("Сдвиг фазы (если используете на двух бёдрах — поставьте 0.5 на втором)")]
    public float phaseOffset = 0f;

    [Tooltip("Включить ли покачивание позиции")]
    public bool enablePositionSway = true;

    [Tooltip("Включить ли вращение")]
    public bool enableRotationSway = true;

    private Vector3 initialLocalPosition;
    private Quaternion initialLocalRotation;

    private void Start()
    {
        if (hipJoint == null)
        {
            Debug.LogError("Hip joint не назначен! Отключаем компонент.", this);
            enabled = false;
            return;
        }

        // Сохраняем начальные значения
        initialLocalPosition = hipJoint.localPosition;
        initialLocalRotation = hipJoint.localRotation;
    }

    private void Update()
    {
        if (hipJoint == null) return;

        float time = Time.time * frequency + phaseOffset;

        // Покачивание позиции
        if (enablePositionSway)
        {
            Vector3 offsetPos = new Vector3(
                Mathf.Sin(time * 0.7f) * positionAmplitude.x,
                Mathf.Sin(time * 1.3f) * positionAmplitude.y,
                Mathf.Sin(time * 1.1f) * positionAmplitude.z
            );
            hipJoint.localPosition = initialLocalPosition + offsetPos;
        }

        // Покачивание вращения
        if (enableRotationSway)
        {
            Quaternion swayRot = Quaternion.Euler(
                Mathf.Sin(time * 1.2f) * rotationAmplitude.x,
                Mathf.Sin(time * 0.9f + 1f) * rotationAmplitude.y,
                Mathf.Sin(time * 1.4f + 2f) * rotationAmplitude.z
            );

            hipJoint.localRotation = initialLocalRotation * swayRot;
        }
    }
}