using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

#if UNITY_EDITOR
namespace DuzeraTools.GradientAtlas
{
[CustomEditor(typeof(GradientAtlasSO))]
public class GradientAtlasSOEditor : Editor
{
    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        GradientAtlasSO atlas = (GradientAtlasSO)target;

        EditorGUILayout.PropertyField(serializedObject.FindProperty("_atlasResolution"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_gradientWidth"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_gradientHeight"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("_autoUpdate"));

        serializedObject.ApplyModifiedProperties();

        int columns = atlas.Columns;
        int rows = atlas.Rows;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField($"Grid: {columns} x {rows}  ({atlas.Count} gradient slot(s))", EditorStyles.boldLabel);

        serializedObject.Update();

        SerializedProperty gradients = serializedObject.FindProperty("_gradients");
        if (gradients.arraySize != atlas.Count)
            gradients.arraySize = atlas.Count;

        const float cellSpacing = 4f;
        float availableWidth = EditorGUIUtility.currentViewWidth - 40;
        float cellWidth = Mathf.Clamp((availableWidth / columns) - cellSpacing, 20f, 36f);
        float cellHeight = Mathf.Clamp(cellWidth * atlas.GradientHeight / atlas.GradientWidth, 32f, 56f);

        for (int row = 0; row < rows; row++)
        {
            EditorGUILayout.BeginHorizontal();
            for (int col = 0; col < columns; col++)
            {
                int index = (row * columns) + col;

                Rect cellRect = GUILayoutUtility.GetRect(cellWidth, cellHeight, GUILayout.Width(cellWidth));

                Matrix4x4 previousMatrix = GUI.matrix;
                GUIUtility.RotateAroundPivot(-90f, cellRect.center);

                Rect fieldRect = new(
                    cellRect.center.x - (cellRect.height * 0.5f),
                    cellRect.center.y - (cellRect.width * 0.5f),
                    cellRect.height,
                    cellRect.width);

                EditorGUI.PropertyField(fieldRect, gradients.GetArrayElementAtIndex(index), GUIContent.none);

                GUI.matrix = previousMatrix;
            }
            EditorGUILayout.EndHorizontal();
        }

        serializedObject.ApplyModifiedProperties();

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        Texture2D texture = atlas.Texture;
        if (texture != null)
        {
            EditorGUILayout.LabelField("Atlas Texture", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
                EditorGUILayout.ObjectField("PNG", texture, typeof(Texture2D), false);
        }

        EditorGUILayout.LabelField("", GUI.skin.horizontalSlider);

        if (GUILayout.Button("Generate/Update Atlas"))
            atlas.UpdateAtlas();

        if (GUILayout.Button("Read From PNG"))
            atlas.ReadFromPng();
    }
}
}
#endif
