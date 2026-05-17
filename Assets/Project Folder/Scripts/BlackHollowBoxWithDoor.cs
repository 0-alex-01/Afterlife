using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BlackHollowBoxWithDoor : MonoBehaviour
{
    [Header("Room Size")]
    public float width = 10f;
    public float height = 4f;
    public float depth = 10f;
    public float wallThickness = 0.25f;

    [Header("Door")]
    public float doorWidth = 2f;
    public float doorHeight = 2.5f;

    [Header("Material")]
    public Color roomColor = Color.black;

    [Header("Generate Settings")]
    public bool deleteOldBoxBeforeGenerate = true;

    public void GenerateBox()
    {
        if (deleteOldBoxBeforeGenerate)
        {
            ClearOldBox();
        }

        Material blackMaterial = CreateMaterial();

        CreateRoom(blackMaterial);
    }

    public void ClearOldBox()
    {
        for (int i = transform.childCount - 1; i >= 0; i--)
        {
#if UNITY_EDITOR
            if (!Application.isPlaying)
                DestroyImmediate(transform.GetChild(i).gameObject);
            else
                Destroy(transform.GetChild(i).gameObject);
#else
            Destroy(transform.GetChild(i).gameObject);
#endif
        }
    }

    private Material CreateMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");

        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material mat = new Material(shader);
        mat.name = "Generated_Black_Room_Material";
        mat.color = roomColor;

        return mat;
    }

    private void CreateRoom(Material mat)
    {
        // Boden
        CreatePart(
            "Floor",
            new Vector3(0, 0, 0),
            new Vector3(width, wallThickness, depth),
            mat
        );

        // Decke
        CreatePart(
            "Ceiling",
            new Vector3(0, height, 0),
            new Vector3(width, wallThickness, depth),
            mat
        );

        // Linke Wand
        CreatePart(
            "Left Wall",
            new Vector3(-width / 2f, height / 2f, 0),
            new Vector3(wallThickness, height, depth),
            mat
        );

        // Rechte Wand
        CreatePart(
            "Right Wall",
            new Vector3(width / 2f, height / 2f, 0),
            new Vector3(wallThickness, height, depth),
            mat
        );

        // Hintere Wand
        CreatePart(
            "Back Wall",
            new Vector3(0, height / 2f, depth / 2f),
            new Vector3(width, height, wallThickness),
            mat
        );

        // Vordere Wand mit Türöffnung
        float sideWallWidth = (width - doorWidth) / 2f;

        if (sideWallWidth > 0)
        {
            CreatePart(
                "Front Wall Left",
                new Vector3(-(doorWidth / 2f + sideWallWidth / 2f), height / 2f, -depth / 2f),
                new Vector3(sideWallWidth, height, wallThickness),
                mat
            );

            CreatePart(
                "Front Wall Right",
                new Vector3(doorWidth / 2f + sideWallWidth / 2f, height / 2f, -depth / 2f),
                new Vector3(sideWallWidth, height, wallThickness),
                mat
            );
        }

        float topWallHeight = height - doorHeight;

        if (topWallHeight > 0)
        {
            CreatePart(
                "Front Wall Above Door",
                new Vector3(0, doorHeight + topWallHeight / 2f, -depth / 2f),
                new Vector3(doorWidth, topWallHeight, wallThickness),
                mat
            );
        }

        CreateDoorFrame(mat);
    }

    private void CreateDoorFrame(Material mat)
    {
        float frameThickness = 0.15f;
        float z = -depth / 2f - 0.05f;

        CreatePart(
            "Door Frame Left",
            new Vector3(-doorWidth / 2f, doorHeight / 2f, z),
            new Vector3(frameThickness, doorHeight, frameThickness),
            mat
        );

        CreatePart(
            "Door Frame Right",
            new Vector3(doorWidth / 2f, doorHeight / 2f, z),
            new Vector3(frameThickness, doorHeight, frameThickness),
            mat
        );

        CreatePart(
            "Door Frame Top",
            new Vector3(0, doorHeight, z),
            new Vector3(doorWidth + frameThickness, frameThickness, frameThickness),
            mat
        );
    }

    private GameObject CreatePart(string partName, Vector3 localPosition, Vector3 scale, Material mat)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;

        part.transform.SetParent(transform);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = Quaternion.identity;
        part.transform.localScale = scale;

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = mat;

        return part;
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BlackHollowBoxWithDoor))]
public class BlackHollowBoxWithDoorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        BlackHollowBoxWithDoor generator = (BlackHollowBoxWithDoor)target;

        GUILayout.Space(10);

        if (GUILayout.Button("Generate Box"))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Generate Black Hollow Box");
            generator.GenerateBox();
            EditorUtility.SetDirty(generator.gameObject);
        }

        if (GUILayout.Button("Clear Box"))
        {
            Undo.RegisterFullObjectHierarchyUndo(generator.gameObject, "Clear Black Hollow Box");
            generator.ClearOldBox();
            EditorUtility.SetDirty(generator.gameObject);
        }
    }
}
#endif