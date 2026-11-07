using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]

public class PlayGame : MonoBehaviour
{
    private Button button;

    private void Awake()
    {
        button = GetComponent<Button>();
        button.onClick.AddListener(OnButtonClicked);
    }

    private void OnDestroy()
    {
        if (button != null)
            button.onClick.RemoveListener(OnButtonClicked);
    }
    public void OnButtonClicked()
    {
        Debug.Log("Play button clicked");
        SceneManager.LoadScene("Game");
    }
}
