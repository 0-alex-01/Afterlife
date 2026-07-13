using UnityEngine;

/// <summary>
/// Einheitlicher, dezenter Jenseits-Hall fuer alle Erzaehlerstimmen.
/// Entfernt bewusst Echo-Filter, damit die Sprache klar verstaendlich bleibt.
/// </summary>
public static class VoiceLineReverb
{
    public static void Apply(AudioSource source)
    {
        if (source == null) return;

        foreach (var echo in source.GetComponents<AudioEchoFilter>())
        {
            echo.enabled = false;
            Object.Destroy(echo);
        }

        var reverb = source.GetComponent<AudioReverbFilter>();
        if (reverb == null)
            reverb = source.gameObject.AddComponent<AudioReverbFilter>();

        reverb.enabled = true;
        reverb.reverbPreset = AudioReverbPreset.User;
        reverb.dryLevel = -100f;
        reverb.room = -900f;
        reverb.roomHF = -1800f;
        reverb.roomLF = -500f;
        reverb.decayTime = 3.5f;
        reverb.decayHFRatio = 0.55f;
        reverb.reflectionsLevel = -1200f;
        reverb.reflectionsDelay = 0.04f;
        reverb.reverbLevel = -350f;
        reverb.reverbDelay = 0.07f;
        reverb.hfReference = 4500f;
        reverb.lfReference = 250f;
        reverb.diffusion = 80f;
        reverb.density = 80f;
    }
}
