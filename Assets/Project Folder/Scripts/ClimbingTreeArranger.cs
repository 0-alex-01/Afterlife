using System.Collections.Generic;
using UnityEngine;

// Ordnet alle Kinder dieser GameObject (z.B. ClimbingCubes) spiralförmig
// um einen Baumstamm an - mit zufälligem Klettermuster nach oben.
// Anwendung: Skript auf das "Climbing"-GameObject legen, Baum + Werte einstellen,
// dann im Inspector am Komponenten-Header (3-Punkte-Menü oder Rechtsklick) ->
// "Arrange Cubes" wählen.
public class ClimbingTreeArranger : MonoBehaviour
{
    [Header("Baum")]
    [Tooltip("Wurzel/Pivot-Position des Baums (z.B. 'Big Oak Tree')")]
    public Transform tree;
    [Tooltip("Radius vom Stamm-Zentrum, an dem die Cubes ankleben")]
    public float trunkRadius = 1.4f;

    [Header("Vertikaler Bereich (relativ zum Baum-Pivot)")]
    public float startHeight = 1.0f;
    public float topHeight = 15.0f;
    [Tooltip("Zufällige Höhen-Streuung pro Cube")]
    public float verticalJitter = 0.25f;

    [Header("Klettermuster")]
    [Tooltip("Grad-Schritt pro Cube um den Stamm (für eine Spirale)")]
    public float baseAngleStep = 70f;
    [Tooltip("Zufällige Winkel-Streuung pro Cube")]
    public float angleJitter = 25f;
    [Tooltip("Seed für Reproduzierbarkeit - ändern für anderes Muster")]
    public int randomSeed = 42;

    [Header("Optionen")]
    [Tooltip("Cube-Vorderseite nach außen drehen (kleben am Stamm)")]
    public bool faceOutward = true;
    [Tooltip("Kinder mit diesem Namensbestandteil überspringen (z.B. Caps)")]
    public string skipNameContains = "Cap";

    [Header("Zusätzliche Parents (deren Kinder mitarrangiert werden)")]
    [Tooltip("z.B. RockClimbPoints_XRI - deren Kinder werden mit in die Spirale aufgenommen")]
    public Transform[] additionalParents;

    private void Reset()
    {
        // Beim Hinzufügen: versuche, "Big Oak Tree" als Geschwister-Objekt zu finden
        if (tree == null && transform.parent != null)
        {
            var t = transform.parent.Find("Big Oak Tree");
            if (t != null) tree = t;
        }
    }

    [ContextMenu("Arrange Cubes")]
    public void Arrange()
    {
        var cubes = new List<Transform>();
        CollectChildren(transform, cubes);
        if (additionalParents != null)
        {
            foreach (var p in additionalParents)
                if (p != null) CollectChildren(p, cubes);
        }

        if (cubes.Count == 0)
        {
            Debug.LogWarning("ClimbingTreeArranger: Keine Cubes zum Anordnen gefunden.", this);
            return;
        }

        if (tree == null)
        {
            Debug.LogError("ClimbingTreeArranger: Bitte den Baum-Transform zuweisen!", this);
            return;
        }

        Vector3 trunkBase = tree.position;
        Random.InitState(randomSeed);

        for (int i = 0; i < cubes.Count; i++)
        {
            float t = cubes.Count > 1 ? (float)i / (cubes.Count - 1) : 0.5f;
            float h = Mathf.Lerp(startHeight, topHeight, t)
                      + Random.Range(-verticalJitter, verticalJitter);

            float angleDeg = i * baseAngleStep + Random.Range(-angleJitter, angleJitter);
            float angle = angleDeg * Mathf.Deg2Rad;

            Vector3 outward = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
            Vector3 worldPos = trunkBase + outward * trunkRadius + Vector3.up * h;

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(cubes[i], "Arrange Climbing Cubes");
#endif

            cubes[i].position = worldPos;
            if (faceOutward)
                cubes[i].rotation = Quaternion.LookRotation(outward, Vector3.up);
        }

        Debug.Log("ClimbingTreeArranger: " + cubes.Count + " Objekte neu angeordnet (Seed " + randomSeed + ").", this);
    }

    private void CollectChildren(Transform parent, List<Transform> into)
    {
        for (int i = 0; i < parent.childCount; i++)
        {
            var c = parent.GetChild(i);
            if (!string.IsNullOrEmpty(skipNameContains) && c.name.Contains(skipNameContains))
                continue;
            into.Add(c);
        }
    }
}
