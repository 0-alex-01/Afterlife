using UnityEngine;
using Autohand;

/// <summary>
/// Bridges a grabbable pedestal object's AutoHand events to the PedestalChoiceController.
/// onHighlight -> hover preview, onUnhighlight -> revert preview, onGrab -> commit choice.
/// Subscribes to the C# events so no inspector wiring of UnityEvents is required.
/// </summary>
[RequireComponent(typeof(Grabbable))]
public class ChoiceObjectRelay : MonoBehaviour
{
    [Tooltip("Which afterlife choice this object represents.")]
    public ChoiceTheme theme = ChoiceTheme.None;
    [Tooltip("The controller that owns the choice state machine.")]
    public PedestalChoiceController controller;

    private Grabbable _grab;

    private void Awake()
    {
        _grab = GetComponent<Grabbable>();
    }

    private void OnEnable()
    {
        if (_grab == null) return;
        _grab.OnHighlightEvent += HandleHighlight;
        _grab.OnUnhighlightEvent += HandleUnhighlight;
        _grab.OnGrabEvent += HandleGrab;
    }

    private void OnDisable()
    {
        if (_grab == null) return;
        _grab.OnHighlightEvent -= HandleHighlight;
        _grab.OnUnhighlightEvent -= HandleUnhighlight;
        _grab.OnGrabEvent -= HandleGrab;
    }

    private void HandleHighlight(Hand hand, Grabbable grabbable)
    {
        if (controller != null) controller.OnHoverEnter(theme);
    }

    private void HandleUnhighlight(Hand hand, Grabbable grabbable)
    {
        if (controller != null) controller.OnHoverExit(theme);
    }

    private void HandleGrab(Hand hand, Grabbable grabbable)
    {
        if (controller != null) controller.OnCommit(theme);
    }
}
