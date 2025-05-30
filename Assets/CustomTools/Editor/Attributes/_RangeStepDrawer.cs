using UnityEngine;

namespace UnityEditor
{
    /// <summary>
/// 属性绘制器
/// </summary>
[CustomPropertyDrawer(typeof(_RangeStepAttribute))]
public class _RangeStepDrawer : PropertyDrawer
{
    private const float NumberWidth = 60f; // 数字输入框宽度
    
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var rangeAttribute = (_RangeStepAttribute)attribute;

        if (property.propertyType == SerializedPropertyType.Float)
        {
            float value = property.floatValue;
            EditorGUI.BeginChangeCheck();
            value = EditorGUI.Slider(position, label, value, rangeAttribute.min, rangeAttribute.max);
            if (EditorGUI.EndChangeCheck())
            {
                // 根据 space 调整值
                value = Mathf.Round(value / rangeAttribute.step) * rangeAttribute.step;
                value = Mathf.Clamp(value, rangeAttribute.min, rangeAttribute.max);
                property.floatValue = value;
            }
        }
        else if (property.propertyType == SerializedPropertyType.Integer)
        {
            int value = property.intValue;
            EditorGUI.BeginChangeCheck();
            value = EditorGUI.IntSlider(position, label, value, (int)rangeAttribute.min, (int)rangeAttribute.max);
            if (EditorGUI.EndChangeCheck())
            {
                // 根据 space 调整值
                value = Mathf.RoundToInt(value / rangeAttribute.step) * (int)rangeAttribute.step;
                value = Mathf.Clamp(value, (int)rangeAttribute.min, (int)rangeAttribute.max);
                property.intValue = value;
            }
        }
        else
        {
            EditorGUI.LabelField(position, label.text, "Use only with float or int values.");
        }
    }
}
}