using UnityEngine;

public class XRHandVisualReplacer : MonoBehaviour
{
    [Header("Hand")]
    public GameObject handPrefab;

    [Header("Controller Visual To Hide")]
    public GameObject controllerVisual;

    [Header("Hand Offset")]
    public Vector3 localPosition = Vector3.zero;
    public Vector3 localRotation = Vector3.zero;
    public Vector3 localScale = Vector3.one;

    private GameObject spawnedHand;

    void Start()
    {
        if (controllerVisual != null)
            controllerVisual.SetActive(false);

        if (handPrefab == null)
        {
            Debug.LogWarning("No hand prefab assigned.", this);
            return;
        }

        spawnedHand = Instantiate(handPrefab, transform);
        spawnedHand.name = handPrefab.name + "_Attached";

        spawnedHand.transform.localPosition = localPosition;
        spawnedHand.transform.localEulerAngles = localRotation;
        spawnedHand.transform.localScale = localScale;
    }
}