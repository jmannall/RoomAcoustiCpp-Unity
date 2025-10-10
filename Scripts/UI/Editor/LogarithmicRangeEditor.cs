using UnityEditor;
using UnityEngine;

[CustomPropertyDrawer(typeof(LogarithmicRangeAttribute))]
public class LogarithmicRangeDrawer : PropertyDrawer
{
    public override void OnGUI(Rect position, SerializedProperty property, GUIContent label)
    {
        var expAttr = (LogarithmicRangeAttribute)attribute;

        EditorGUI.BeginProperty(position, label, property);

        // Reserve space for label, slider, and float field
        position = EditorGUI.PrefixLabel(position, GUIUtility.GetControlID(FocusType.Passive), label);

        // Work out widths (match Unity slider style: big slider, small field)
        const float fieldWidth = 65f; // keep it compact like Unity's default
        float sliderWidth = position.width - fieldWidth + 10f;

        Rect sliderRect = new Rect(position.x, position.y, sliderWidth, EditorGUIUtility.singleLineHeight);
        Rect fieldRect = new Rect(position.x + sliderWidth - 10f, position.y, fieldWidth, EditorGUIUtility.singleLineHeight);

        // Draw slider for exponent
        property.floatValue = GUI.HorizontalSlider(sliderRect, property.floatValue, expAttr.min, expAttr.max);

        // Clamp and compute 10^x
        float exp = Mathf.Clamp(property.floatValue, expAttr.min, expAttr.max);
        float value = Mathf.Pow(10f, exp);

        bool hasChanged = GUI.changed;
        GUI.changed = false;

        if (expAttr.displayInt)
            value = EditorGUI.IntField(fieldRect, Mathf.RoundToInt(value));
        else
            value = EditorGUI.FloatField(fieldRect, Mathf.Round(value * 100f) / 100f);

        if (GUI.changed)
        {
            // If user changed the numerical field, update the exponent accordingly
            value = Mathf.Clamp(value, (int)Mathf.Pow(10f, expAttr.min), (int)Mathf.Pow(10f, expAttr.max));
            property.floatValue = Mathf.Log10(value);
            property.serializedObject.ApplyModifiedProperties();
            GUI.changed = true;
        }
        else
            GUI.changed = hasChanged;

        // Show computed value as a single-line float field (read-only)
        //using (new EditorGUI.DisabledScope(true))
        //{
        //    EditorGUI.IntField(fieldRect, value);
        //}

        EditorGUI.EndProperty();
    }

    public override float GetPropertyHeight(SerializedProperty property, GUIContent label)
    {
        // Two lines tall
        return EditorGUIUtility.singleLineHeight;
    }
}
