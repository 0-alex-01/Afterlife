using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(ClimbingTreeArranger))]
public class ClimbingTreeArrangerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);

        var arranger = (ClimbingTreeArranger)target;

        GUI.backgroundColor = new Color(0.5f, 0.85f, 0.5f);
        if (GUILayout.Button("Arrange Cubes", GUILayout.Height(36)))
        {
            Undo.SetCurrentGroupName("Arrange Climbing Cubes");
            arranger.Arrange();
        }
        GUI.backgroundColor = Color.white;

        EditorGUILayout.HelpBox(
            "Tipp: Random Seed ändern und nochmal klicken = neues Muster.\n" +
            "Strg+Z macht jede Anordnung rückgängig.",
            MessageType.Info);
    }
}
