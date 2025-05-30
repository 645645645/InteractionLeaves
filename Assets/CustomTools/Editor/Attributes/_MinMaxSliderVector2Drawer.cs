using UnityEngine;

namespace UnityEditor
{
    [CustomPropertyDrawer(typeof(_MinMaxSliderVector2Attribute))]
    public class _MinMaxSliderVector2Drawer : PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            if (property.propertyType == SerializedPropertyType.Vector2)
            {
                var minMaxAttribute = (_MinMaxSliderVector2Attribute)attribute;
                Vector2 vector = property.vector2Value;

                EditorGUI.BeginChangeCheck();

                position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

                Rect leftInput = new Rect(position.x, position.y, position.width * 0.2f, position.height);
                Rect slider = new Rect(position.x + position.width * 0.2f, position.y, position.width * 0.6f, position.height);
                Rect rightInput = new Rect(position.x + position.width * 0.8f, position.y, position.width * 0.2f, position.height);

                vector.x = EditorGUI.FloatField(leftInput, vector.x);
                EditorGUI.MinMaxSlider(slider, ref vector.x, ref vector.y, minMaxAttribute.minLimit, minMaxAttribute.maxLimit);
                vector.y = EditorGUI.FloatField(rightInput, vector.y);

                if (EditorGUI.EndChangeCheck())
                {
                    vector.x = Mathf.Clamp(vector.x, minMaxAttribute.minLimit, vector.y);
                    vector.y = Mathf.Clamp(vector.y, vector.x, minMaxAttribute.maxLimit);
                    property.vector2Value = vector;
                }
            }
            else
            {
                EditorGUI.LabelField(position, label.text, "Use only with Vector2.");
            }
        }
    }  
}