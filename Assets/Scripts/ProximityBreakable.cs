using System.Collections;
using UnityEngine;

public class ProximityBreakable : MonoBehaviour
{
    [SerializeField] private GameObject intactObject;
    [SerializeField] private GameObject brokenObject;

    [SerializeField] private float minDelay = 3f;
    [SerializeField] private float maxDelay = 5f;

    [SerializeField] private float force = 2.5f;
    [SerializeField] private float upwardForce = 1f;
    [SerializeField] private float torqueForce = 2f;

    private bool playerInside = false;
    private bool isBroken = false;
    private Coroutine breakCoroutine;

    private void Awake()
    {
        intactObject.SetActive(true);
        brokenObject.SetActive(false);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (isBroken) return;

        if (other.CompareTag("Player"))
        {
            playerInside = true;
            breakCoroutine = StartCoroutine(BreakAfterDelay());
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            playerInside = false;

            if (breakCoroutine != null)
                StopCoroutine(breakCoroutine);
        }
    }

    private IEnumerator BreakAfterDelay()
    {
        float delay = Random.Range(minDelay, maxDelay);
        yield return new WaitForSeconds(delay);

        if (playerInside && !isBroken)
        {
            Break();
        }
    }

    private void Break()
    {
        isBroken = true;

        intactObject.SetActive(false);
        brokenObject.SetActive(true);

        Rigidbody[] rigidbodies = brokenObject.GetComponentsInChildren<Rigidbody>();
        Vector3 center = intactObject.transform.position;

        foreach (Rigidbody rb in rigidbodies)
        {
            rb.isKinematic = false;
            rb.useGravity = true;
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            Vector3 dir = (rb.worldCenterOfMass - center).normalized;
            Vector3 finalForce = dir * force + Vector3.up * upwardForce;

            rb.AddForce(finalForce, ForceMode.Impulse);
            rb.AddTorque(Random.insideUnitSphere * torqueForce, ForceMode.Impulse);
        }
    }
}