using System.Collections;
using UnityEngine;

public class RandomSwanAnimation : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private string stateName = "Armature|Swan_idle";

    [Header("Random Animation Speed")]
    [SerializeField] private float minSpeed = 0.85f;
    [SerializeField] private float maxSpeed = 1.15f;

    private IEnumerator Start()
    {
        if (animator == null)
            animator = GetComponent<Animator>();

        // Einen Frame warten, damit der Animator initialisiert ist
        yield return null;

        // Leicht unterschiedliche Geschwindigkeit
        animator.speed = Random.Range(minSpeed, maxSpeed);

        // Zufälliger Startpunkt innerhalb der Animation
        float randomStartTime = Random.Range(0f, 1f);
        animator.Play(stateName, 0, randomStartTime);

        // Animator sofort aktualisieren
        animator.Update(0f);
    }
}
