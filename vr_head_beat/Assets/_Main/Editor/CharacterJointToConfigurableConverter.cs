using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

/// <summary>
/// Инструмент для конвертации CharacterJoint в ConfigurableJoint с поддержкой targetRotation.
/// Работает в редакторе, создаёт копию объекта.
/// </summary>
public class CharacterJointToConfigurableConverter : EditorWindow
{
    private static GameObject sourceNPC;

    [MenuItem("Tools/Convert CharacterJoints to ConfigurableJoints")]
    public static void ShowWindow()
    {
        sourceNPC = Selection.activeGameObject;

        if (sourceNPC == null)
        {
            EditorUtility.DisplayDialog("Conversion Tool", "Please select an NPC with CharacterJoints.", "OK");
            return;
        }

        CharacterJoint[] joints = sourceNPC.GetComponentsInChildren<CharacterJoint>();
        if (joints.Length == 0)
        {
            EditorUtility.DisplayDialog("Conversion Tool", "Selected object has no CharacterJoints.", "OK");
            return;
        }

        Convert();
    }

    static void Convert()
    {
        // Создаём копию оригинального объекта
        GameObject clone = Object.Instantiate(sourceNPC);
        clone.name = sourceNPC.name + "_Converted";

        // Получаем все CharacterJoint'ы на копии
        CharacterJoint[] joints = clone.GetComponentsInChildren<CharacterJoint>();

        // Буфер для хранения данных ДО удаления компонентов
        var jointDataList = new List<JointConversionData>();

        // === ШАГ 1: СОБИРАЕМ ВСЕ ДАННЫЕ ===
        foreach (CharacterJoint joint in joints)
        {
            jointDataList.Add(new JointConversionData
            {
                Joint = joint,
                ConnectedBody = joint.connectedBody,
                Anchor = joint.anchor,
                Axis = joint.axis,
                SwingAxis = joint.swingAxis,
                LowTwistLimit = joint.lowTwistLimit,
                HighTwistLimit = joint.highTwistLimit,
                Swing1Limit = joint.swing1Limit,
                Swing2Limit = joint.swing2Limit,
                EnableProjection = joint.enableProjection,
                ProjectionDistance = joint.projectionDistance,
                ProjectionAngle = joint.projectionAngle
            });
        }

        // === ШАГ 2: КОНВЕРТИРУЕМ КАЖДЫЙ JOINT ===
        foreach (var data in jointDataList)
        {
            // Удаляем CharacterJoint
            Object.DestroyImmediate(data.Joint);

            // Добавляем ConfigurableJoint на тот же GameObject
            ConfigurableJoint confJoint = data.Joint.gameObject.AddComponent<ConfigurableJoint>();
            
            confJoint.connectedBody = data.ConnectedBody;
            confJoint.anchor = data.Anchor;
            confJoint.axis = data.Axis;

            // Вычисляем secondaryAxis
            Vector3 secondaryAxis = Vector3.Cross(data.Axis, data.SwingAxis);
            if (secondaryAxis.magnitude < 0.01f)
            {
                // Если оси почти параллельны — используем fallback
                secondaryAxis = GetPerpendicular(data.Axis);
            }
            else
            {
                secondaryAxis.Normalize();
            }

            // Корректируем направление (правая система координат)
            Vector3 cross = Vector3.Cross(data.Axis, secondaryAxis);
            if (Vector3.Dot(cross, data.SwingAxis) < 0)
                secondaryAxis = -secondaryAxis;

            confJoint.secondaryAxis = secondaryAxis;

            // === УСТАНАВЛИВАЕМ ОГРАНИЧЕНИЯ ===
            confJoint.lowAngularXLimit = data.LowTwistLimit;
            confJoint.highAngularXLimit = data.HighTwistLimit;
            confJoint.angularYLimit = data.Swing1Limit;
            confJoint.angularZLimit = data.Swing2Limit;

            confJoint.angularXMotion = ConfigurableJointMotion.Limited;
            confJoint.angularYMotion = ConfigurableJointMotion.Limited;
            confJoint.angularZMotion = ConfigurableJointMotion.Limited;

            // === НАСТРАИВАЕМ DRIVE ДЛЯ targetRotation ===
            JointDrive drive = new JointDrive
            {
                positionSpring = 500f,   // Жёсткость возврата
                positionDamper = 10f,    // Демпфирование
                maximumForce = 3000f     // Макс. сила
            };

            confJoint.rotationDriveMode = RotationDriveMode.Slerp;  // Включаем targetRotation
            confJoint.slerpDrive = drive;
            confJoint.targetRotation = Quaternion.identity;         // Целевая ориентация (локальная)
            confJoint.targetAngularVelocity = Vector3.zero;

            // === ПРОЕКЦИЯ (чтобы суставы не разлетались) ===
            confJoint.projectionMode = data.EnableProjection 
                ? JointProjectionMode.PositionAndRotation 
                : JointProjectionMode.None;
            confJoint.projectionDistance = data.ProjectionDistance;
            confJoint.projectionAngle = data.ProjectionAngle;
        }

        // === ЗАВЕРШЕНИЕ ===
        // Смещаем копию, чтобы не перекрывать оригинал
        clone.transform.position += Vector3.right * 2f;

        // Выделяем новую копию
        Selection.activeGameObject = clone;

        // Лог и сообщение
        Debug.Log($"✅ Converted {joints.Length} CharacterJoints to ConfigurableJoints. New object: {clone.name}");
        EditorUtility.DisplayDialog(
            "Success", 
            $"Converted {joints.Length} joints.\nNew object created: {clone.name}", 
            "OK"
        );
    }

    // Вспомогательный метод: возвращает вектор, перпендикулярный заданному
    static Vector3 GetPerpendicular(Vector3 v)
    {
        // Проверяем, не параллелен ли v оси Y
        return Mathf.Abs(Vector3.Dot(v, Vector3.up)) < 0.9f ? Vector3.up : Vector3.right;
    }

    // Структура для хранения данных перед удалением CharacterJoint
    struct JointConversionData
    {
        public CharacterJoint Joint;
        public Rigidbody ConnectedBody;
        public Vector3 Anchor;
        public Vector3 Axis;
        public Vector3 SwingAxis;
        public SoftJointLimit LowTwistLimit;
        public SoftJointLimit HighTwistLimit;
        public SoftJointLimit Swing1Limit;
        public SoftJointLimit Swing2Limit;
        public bool EnableProjection;
        public float ProjectionDistance;
        public float ProjectionAngle;
    }
}