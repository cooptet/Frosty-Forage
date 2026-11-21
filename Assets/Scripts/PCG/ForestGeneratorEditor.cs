#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

//for testing
[CustomEditor(typeof(ForestGenerator))]
public class ForestGeneratorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var gen = (ForestGenerator)target;

        EditorGUILayout.Space();
        if (GUILayout.Button("Generate")) gen.Generate();
        if (GUILayout.Button("Generate (new seed)"))
        {
            Undo.RecordObject(gen, "New Seed");
            gen.seed = Random.Range(0, 999999);
            EditorUtility.SetDirty(gen);
            gen.Generate();
        }
        if (GUILayout.Button("Clear")) gen.Clear();
    }
}
#endif