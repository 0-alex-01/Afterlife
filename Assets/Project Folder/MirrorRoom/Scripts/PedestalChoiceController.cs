using System.Collections;
using UnityEngine;

/// <summary>
/// The choice state machine for the three pedestals:
/// Neutral -> Hovering(theme) -> Committed(theme).
/// Hover previews the mirror non-committally; grab commits, transforms the mirror (via
/// SoulMorphController), fades the objects out, dims the unchosen pedestals, and materializes
/// the chosen themed portal. Only one choice is allowed; interaction locks after commit.
/// </summary>
public class PedestalChoiceController : MonoBehaviour
{
    [System.Serializable]
    public class Pedestal
    {
        public ChoiceTheme theme = ChoiceTheme.None;
        [Tooltip("The floating grabbable object root.")]
        public GameObject floatingObject;
        [Tooltip("Light emitted by the floating object.")]
        public Light objectLight;
        [Tooltip("Idle hover/rotate driver on the object (disabled on commit).")]
        public FloatingObject floater;
        [Tooltip("Light under/around the pedestal (dimmed if not chosen).")]
        public Light pedestalLight;
        [Tooltip("The themed portal that materializes if this pedestal is chosen (starts hidden).")]
        public ChoicePortal portal;
    }

    [Tooltip("The mirror morph controller to drive on hover/commit.")]
    [SerializeField] private SoulMorphController mirror;
    [SerializeField] private Pedestal[] pedestals = new Pedestal[3];
    [Tooltip("Seconds for objects to fade out and pedestals to dim on commit.")]
    [SerializeField] private float fadeDuration = 1.8f;
    [Tooltip("Fraction of original intensity an unchosen pedestal light dims to.")]
    [Range(0f, 1f)][SerializeField] private float dimmedLightFactor = 0.1f;

    private ChoiceTheme _hover = ChoiceTheme.None;
    private bool _committed;

    public bool Committed => _committed;
    public ChoiceTheme HoverTheme => _hover;

    /// <summary>A ghost hand approached this theme's object — preview the mirror.</summary>
    public void OnHoverEnter(ChoiceTheme theme)
    {
        if (_committed || theme == ChoiceTheme.None) return;
        _hover = theme;
        if (mirror != null) mirror.SetChoicePreview(theme);
    }

    /// <summary>The ghost hand withdrew — revert the preview (only if it owns the current hover).</summary>
    public void OnHoverExit(ChoiceTheme theme)
    {
        if (_committed) return;
        if (_hover == theme)
        {
            _hover = ChoiceTheme.None;
            if (mirror != null) mirror.ClearChoicePreview();
        }
    }

    /// <summary>The player grabbed this theme's object — finalize the choice.</summary>
    public void OnCommit(ChoiceTheme theme)
    {
        if (_committed || theme == ChoiceTheme.None) return;
        _committed = true;

        if (mirror != null) mirror.CommitChoice(theme);

        foreach (var p in pedestals)
        {
            if (p == null) continue;
            if (p.floater != null) p.floater.enabled = false;

            bool chosen = p.theme == theme;
            if (chosen && p.portal != null)
            {
                p.portal.gameObject.SetActive(true);
                p.portal.Materialize();
            }
            StartCoroutine(FadeOutPedestal(p, chosen));
        }
    }

    private IEnumerator FadeOutPedestal(Pedestal p, bool chosen)
    {
        // Lock interaction on this object.
        if (p.floatingObject != null)
        {
            var relay = p.floatingObject.GetComponent<ChoiceObjectRelay>();
            if (relay != null) relay.enabled = false;
        }

        Vector3 startScale = p.floatingObject != null ? p.floatingObject.transform.localScale : Vector3.one;
        Vector3 endScale = startScale * 0.01f;
        float objLight0 = p.objectLight != null ? p.objectLight.intensity : 0f;
        float pedLight0 = p.pedestalLight != null ? p.pedestalLight.intensity : 0f;
        float pedTarget = chosen ? pedLight0 : pedLight0 * dimmedLightFactor;

        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            float k = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / fadeDuration));
            if (p.floatingObject != null)
                p.floatingObject.transform.localScale = Vector3.Lerp(startScale, endScale, k);
            if (p.objectLight != null)
                p.objectLight.intensity = Mathf.Lerp(objLight0, 0f, k);
            if (p.pedestalLight != null)
                p.pedestalLight.intensity = Mathf.Lerp(pedLight0, pedTarget, k);
            yield return null;
        }

        if (p.floatingObject != null) p.floatingObject.SetActive(false);
    }
}
