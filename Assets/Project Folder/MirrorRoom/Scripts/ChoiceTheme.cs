/// <summary>
/// The three afterlife choices offered by the pedestals in the MirrorRoom.
/// Shared by the pedestal/choice state machine, the mirror state changes,
/// and the themed portals so all systems speak the same language.
/// </summary>
public enum ChoiceTheme
{
    None = 0,
    Void = 1,       // extinguishing candle / dark sphere -> black mirror -> "Leere" scene
    Rebirth = 2,    // sprouting seed / egg -> green bloom mirror -> "Wiedergeburt" scene
    Judgement = 3   // split crystal / scale -> vertical split mirror -> "WaageScene" scene
}
