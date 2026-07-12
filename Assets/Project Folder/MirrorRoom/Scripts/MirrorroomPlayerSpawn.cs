using System.Collections;
using UnityEngine;

/// <summary>
/// Reapplies the authored Mirrorroom spawn after XR tracking and AutoHand initialized.
/// This prevents the tracking origin from visually pulling the player back to the old spawn.
/// </summary>
public sealed class MirrorroomPlayerSpawn : MonoBehaviour
{
    [SerializeField] private Vector3 spawnPosition = new Vector3(0f, 1.6f, -24f);
    [SerializeField] private Transform physicsPlayer;
    [SerializeField] private Transform leftHand;
    [SerializeField] private Transform rightHand;

    private IEnumerator Start()
    {
        yield return new WaitForEndOfFrame();
        yield return new WaitForFixedUpdate();
        yield return new WaitForEndOfFrame();

        ApplySpawn();
        yield return new WaitForSeconds(0.25f);
        ApplySpawn();
        yield return new WaitForSeconds(0.75f);
        ApplySpawn();
    }

    private void ApplySpawn()
    {
        transform.position = spawnPosition;
        if (physicsPlayer != null)
        {
            physicsPlayer.localPosition = Vector3.zero;
            var body = physicsPlayer.GetComponent<Rigidbody>();
            if (body != null)
            {
                body.position = physicsPlayer.position;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
        }

        SnapHandToRig(leftHand);
        SnapHandToRig(rightHand);
        Physics.SyncTransforms();
    }

    private static void SnapHandToRig(Transform hand)
    {
        if (hand == null) return;
        hand.localPosition = Vector3.zero;
        var body = hand.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.position = hand.position;
            body.linearVelocity = Vector3.zero;
            body.angularVelocity = Vector3.zero;
        }
    }
}
