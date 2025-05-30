using UnityEngine;

namespace UnityEditor
{
    [CustomPropertyDrawer(typeof(_ExponentialSliderAttribute))]
    public class _ExponentialSliderDrawer : PropertyDrawer
    {
        private const float LabelWidth = 80f;
        private const float Epsilon = 1e-10f;

        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            _ExponentialSliderAttribute slider = attribute as _ExponentialSliderAttribute;

            if (property.propertyType == SerializedPropertyType.Float)
            {
                EditorGUI.BeginChangeCheck();

                float value = property.floatValue;

                // 验证最小值是否大于0（指数模式需要正数）
                if (slider.isExponential && slider.min <= 0)
                {
                    EditorGUI.LabelField(position, label.text, "Exponential slider requires min > 0");
                    return;
                }

                // 确保幂数大于0
                if (!slider.isExponential && slider.power <= 0)
                {
                    EditorGUI.LabelField(position, label.text, "Power must be greater than 0");
                    return;
                }

                // 确保最小值小于最大值
                if (slider.min >= slider.max)
                {
                    EditorGUI.LabelField(position, label.text, "Min value must be less than max value");
                    return;
                }

                // 限制值在有效范围内
                value = Mathf.Clamp(value, slider.min, slider.max);
                
                Rect labelRect = new Rect(position.x, position.y, EditorGUIUtility.labelWidth, position.height);
                Rect sliderRect = new Rect(position.x + EditorGUIUtility.labelWidth, position.y, position.width - EditorGUIUtility.labelWidth - LabelWidth, position.height);
                Rect valueLabelRect = new Rect(position.x + position.width - LabelWidth, position.y, LabelWidth, position.height);

                EditorGUI.LabelField(labelRect, label);
                // 将实际值转换为滑块值（0-1范围）
                float sliderValue;

                if (slider.isExponential)
                {
                    // 指数转换：log(min)到log(max)的线性插值
                    float minLog = Mathf.Log(slider.min);
                    float maxLog = Mathf.Log(slider.max);
                    float valueLog = Mathf.Log(Mathf.Max(value, Epsilon));
                    sliderValue = Mathf.InverseLerp(minLog, maxLog, valueLog);
                }
                else if (slider.isLinearPower)
                {
                    // 浮点数幂数线性转换
                    float normalizedValue = (value - slider.min) / (slider.max - slider.min);

                    if (slider.power > 0)
                    {
                        sliderValue = Mathf.Pow(normalizedValue, 1.0f / slider.power);
                    }
                    else
                    {
                        // 负幂数处理
                        sliderValue = 1.0f - Mathf.Pow(1.0f - normalizedValue, 1.0f / Mathf.Abs(slider.power));
                    }
                }
                else
                {
                    // 常规幂数转换
                    float normalizedValue = Mathf.InverseLerp(slider.min, slider.max, value);
                    sliderValue = Mathf.Pow(normalizedValue, 1.0f / slider.power);
                }

                sliderValue = EditorGUI.Slider(sliderRect, GUIContent.none, sliderValue, 0f, 1f);

                // 将滑块值转换回实际值
                if (slider.isExponential)
                {
                    // 指数转换回实际值
                    float minLog = Mathf.Log(slider.min);
                    float maxLog = Mathf.Log(slider.max);
                    float valueLog = Mathf.Lerp(minLog, maxLog, sliderValue);
                    value = Mathf.Exp(valueLog);
                }
                else if (slider.isLinearPower)
                {
                    // 浮点数幂数线性转换回实际值
                    float normalizedValue;

                    if (slider.power > 0)
                    {
                        normalizedValue = Mathf.Pow(sliderValue, slider.power);
                    }
                    else
                    {
                        // 负幂数处理
                        normalizedValue = 1.0f - Mathf.Pow(1.0f - sliderValue, Mathf.Abs(slider.power));
                    }

                    value = Mathf.Lerp(slider.min, slider.max, normalizedValue);
                }
                else
                {
                    // 常规幂数转换回实际值
                    float normalizedValue = Mathf.Pow(sliderValue, slider.power);
                    value = Mathf.Lerp(slider.min, slider.max, normalizedValue);
                }

                string formatString = slider.decimalPlaces > 0 ? $"F{slider.decimalPlaces}" : "G";
                string valueText = value.ToString(formatString);

                if (Mathf.Abs(value) < 0.001f && value != 0)
                {
                    valueText = value.ToString("0.####E0");
                }
                
                // EditorGUI.LabelField(valueLabelRect, valueText);            
                string newText = EditorGUI.TextField(valueLabelRect, valueText);
            
                if (float.TryParse(newText, out float parsedValue))
                {
                    value = Mathf.Clamp(parsedValue, slider.min, slider.max);
                }

                if (EditorGUI.EndChangeCheck())
                {
                    property.floatValue = value;
                }
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use ExponentialSlider with float.");
            }
        }
    }
}