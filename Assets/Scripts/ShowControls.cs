using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ShowControls : MonoBehaviour
{
    [SerializeField] private GameObject text;

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

    private void OnButtonClicked()
    {
        Debug.Log("Show Controls clicked");

        if (text == null)
        {
            Debug.LogError("Controls Text is not assigned!");
            return;
        }

        text.SetActive(true);
    }
}