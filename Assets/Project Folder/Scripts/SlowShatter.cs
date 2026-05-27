using System.Collections;
using UnityEngine;

public class SlowShatter : MonoBehaviour
{
    [Header("Shatter Settings")]
    public float spreadDistance = 2.5f;
    public float duration = 4f;
    public float delayBetweenPieces = 0.03f;
    public float rotationSpeed = 90f;

    [Header("Direction")]
    public bool explodeFromCenter = true;
    public Vector3 customDirection = Vector3.up;

    private Transform[] pieces;
    private Vector3[] startPositions;
    private Quaternion[] startRotations;
    private Vector3[] targetPositions;
    private bool isShattering;

    void Awake()
    {
        int count = transform.childCount;

        pieces = new Transform[count];
        startPositions = new Vector3[count];
        startRotations = new Quaternion[count];
        targetPositions = new Vector3[count];

        Vector3 center = transform.position;

        for (int i = 0; i < count; i++)
        {
            Transform piece = transform.GetChild(i);

            pieces[i] = piece;
            startPositions[i] = piece.localPosition;
            startRotations[i] = piece.localRotation;

            Vector3 direction;

            if (explodeFromCenter)
            {
                direction = (piece.position - center).normalized;

                if (direction == Vector3.zero)
                    direction = Random.onUnitSphere;
            }
            else
            {
                direction = customDirection.normalized;
            }

            direction += Random.insideUnitSphere * 0.35f;
            direction.Normalize();

            float randomDistance = spreadDistance * Random.Range(0.6f, 1.25f);
            targetPositions[i] = startPositions[i] + transform.InverseTransformDirection(direction) * randomDistance;
        }
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space) && !isShattering)
        {
            StartCoroutine(ShatterRoutine());
        }
    }

    IEnumerator ShatterRoutine()
    {
        isShattering = true;

        for (int i = 0; i < pieces.Length; i++)
        {
            StartCoroutine(MovePiece(i));
            yield return new WaitForSeconds(delayBetweenPieces);
        }
    }

    IEnumerator MovePiece(int index)
    {
        Transform piece = pieces[index];

        float timer = 0f;
        Vector3 start = startPositions[index];
        Vector3 end = targetPositions[index];

        Quaternion rotStart = startRotations[index];
        Quaternion rotEnd = Random.rotation;

        while (timer < duration)
        {
            timer += Time.deltaTime;
            float t = timer / duration;

            // weiches langsames Zersplittern
            t = Mathf.SmoothStep(0f, 1f, t);

            piece.localPosition = Vector3.Lerp(start, end, t);
            piece.localRotation = Quaternion.Slerp(
                rotStart,
                rotEnd,
                t * rotationSpeed / 90f
            );

            yield return null;
        }

        piece.localPosition = end;
        piece.localRotation = rotEnd;
    }
}