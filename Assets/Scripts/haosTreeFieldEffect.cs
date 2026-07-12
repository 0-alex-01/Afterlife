using System.Collections.Generic;
using UnityEngine;

public class ChaosTreeFieldEffect : MonoBehaviour
{
    [Header("Player View")]
    [SerializeField] private Camera playerCamera;

    [Header("Detection Settings")]
    [SerializeField] private float detectionRadius = 15f;
    [SerializeField] private float viewAngle = 80f;
    [SerializeField] private float scanInterval = 0.25f;

    private float nextScanTime = 0f;

    private void Awake()
    {
        if (playerCamera == null)
        {
            playerCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (playerCamera == null) return;

        if (Time.time >= nextScanTime)
        {
            nextScanTime = Time.time + scanInterval;
            ScanForTrees();
        }
    }

    private void ScanForTrees()
    {
        Collider[] hits = Physics.OverlapSphere(
            playerCamera.transform.position,
            detectionRadius
        );

        HashSet<ChaosTree> foundTrees = new HashSet<ChaosTree>();

        foreach (Collider hit in hits)
        {
            ChaosTree tree = hit.GetComponentInParent<ChaosTree>();

            if (tree == null) continue;
            if (foundTrees.Contains(tree)) continue;

            Vector3 directionToTree = tree.transform.position - playerCamera.transform.position;
            float angleToTree = Vector3.Angle(playerCamera.transform.forward, directionToTree);

            bool isInView = angleToTree <= viewAngle / 2f;

            if (isInView)
            {
                tree.TriggerChaos();
                foundTrees.Add(tree);
            }
        }
    }
}