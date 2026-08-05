using UnityEngine;

public class JournalManager : MonoBehaviour
{
    public static JournalManager Instance { get; private set; }

    public GameObject journalUI;
    public GameObject[] notePages;

    bool isOpen;

    void Awake() => Instance = this;

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.J))
        {
            isOpen = !isOpen;
            if (journalUI != null) journalUI.SetActive(isOpen);
        }
    }

    public void UnlockPage(int index)
    {
        if (index >= 0 && index < notePages.Length && notePages[index] != null)
            notePages[index].SetActive(true);
    }
}