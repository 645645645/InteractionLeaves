using UnityEngine;

namespace UnityEditor
{
    [CustomPropertyDrawer(typeof(_ReadOnlyInPlayModeAttribute))]
    public class _ReadOnlyInPlayModeDrawer:PropertyDrawer
    {
        public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
        {
            bool isReadOnly = Application.isPlaying;
        
            bool previousGUIState = GUI.enabled;
        
            GUI.enabled = !isReadOnly;
        
            EditorGUI.PropertyField(position, property, label, true);
        
            GUI.enabled = previousGUIState;
        }
    
        public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
        {
            return EditorGUI.GetPropertyHeight(property, label, true);
        }
    }
}