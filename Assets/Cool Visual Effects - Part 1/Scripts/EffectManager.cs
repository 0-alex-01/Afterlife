using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class EffectManager : MonoBehaviour
{
    public GameObject[] effects;
    private VisualEffect[] vfxs;
    private int currentEffect = 0;

    private void Start()
    {
        if (effects == null || effects.Length == 0)
        {
            Debug.LogWarning($"[EffectManager] '{gameObject.name}' has no effects assigned in the inspector.", this);
            return;
        }

        vfxs = new VisualEffect[effects.Length];
        for (int i = 0; i < effects.Length; ++i)
        {
            if (effects[i] == null) continue;
            effects[i].SetActive(false);
            vfxs[i] = effects[i].GetComponent<VisualEffect>();
        }

        if (currentEffect >= 0 && currentEffect < effects.Length && effects[currentEffect] != null)
        {
            effects[currentEffect].SetActive(true);
        }
    }

    private void Update()
    {
        if (effects == null || effects.Length == 0 || vfxs == null) return;

        if (Input.GetKeyDown(KeyCode.LeftArrow))
        {
            if(currentEffect - 1 >= 0)
            {
                if (vfxs[currentEffect] != null) vfxs[currentEffect].Stop();
                if (effects[currentEffect] != null) effects[currentEffect].SetActive(false);
                --currentEffect;
                if (effects[currentEffect] != null) effects[currentEffect].SetActive(true);
                if (vfxs[currentEffect] != null) vfxs[currentEffect].Play();
            }
        }

        if (Input.GetKeyDown(KeyCode.RightArrow))
        {
            if (currentEffect + 1 < effects.Length)
            {
                if (vfxs[currentEffect] != null) vfxs[currentEffect].Stop();
                if (effects[currentEffect] != null) effects[currentEffect].SetActive(false);
                ++currentEffect;
                if (effects[currentEffect] != null) effects[currentEffect].SetActive(true);
                if (vfxs[currentEffect] != null) vfxs[currentEffect].Play();
            }
        }
    }
}
