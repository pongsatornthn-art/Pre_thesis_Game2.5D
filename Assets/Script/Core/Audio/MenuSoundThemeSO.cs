using UnityEngine;

[CreateAssetMenu(fileName = "NewMenuSoundTheme", menuName = "Audio/Menu Sound Theme")]
public class MenuSoundThemeSO : ScriptableObject
{
    [Header("Menu Button Sounds")]
    public AudioClip hoverSound;
    public AudioClip clickSound;
    public AudioClip backSound;
    public AudioClip errorSound;

    public AudioClip GetSound(MenuSoundType type)
    {
        switch (type)
        {
            case MenuSoundType.Hover: return hoverSound;
            case MenuSoundType.Click: return clickSound;
            case MenuSoundType.Back: return backSound;
            case MenuSoundType.Error: return errorSound;
            default: return null;
        }
    }
}
