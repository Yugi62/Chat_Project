using TMPro;
using TMPro.SpriteAssetUtilities;
using UnityEngine;
using UnityEngine.UI;

public class ChatInput : MonoBehaviour
{
    [SerializeField] private TMP_Text textUI;
    [SerializeField] private TMP_InputField nameInput;
    [SerializeField] private ClientSystem clientSystem;

    private TMP_InputField inputField;

    private void Awake()
    {
        inputField = GetComponent<TMP_InputField>();
    }

    public void SetTextInUI()
    {
        textUI.text += nameInput.text + " : " + inputField.text + '\n';
        clientSystem.SendData(inputField.text);

        inputField.text = null;
    }
}
