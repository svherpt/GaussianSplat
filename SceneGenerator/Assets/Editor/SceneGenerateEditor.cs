using UnityEngine;
using UnityEditor;

[CustomEditor(typeof(GenerateImages))]
public class SceneGenerateEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        GenerateImages exporter = (GenerateImages)target;

        if (GUILayout.Button("Generate Cameras"))
        {
            exporter.Generate();
        }
    }
}
