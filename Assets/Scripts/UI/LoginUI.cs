using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginUI : MonoBehaviour
{
    [Header("Panel Login")]
    public GameObject loginPanel;
    public TMP_InputField usernameInput;
    public TMP_InputField passwordInput;
    public UnityEngine.UI.Button loginButton;
    public UnityEngine.UI.Button registerButton;
    public TMP_Text errorText;

    [Header("Panel Registro")]
    public GameObject registerPanel;
    public TMP_InputField usernameRegInput;
    public TMP_InputField passwordRegInput;
    public TMP_InputField passwordConfirmInput;
    public UnityEngine.UI.Button confirmRegisterButton;
    public UnityEngine.UI.Button backToLoginButton;
    public TMP_Text errorRegText;

    void Start()
    {
        // Caracteres de contraseña
        passwordInput.asteriskChar = '●';
        passwordRegInput.asteriskChar = '●';
        passwordConfirmInput.asteriskChar = '●';

        // Estado inicial — solo login visible
        loginPanel.SetActive(true);
        registerPanel.SetActive(false);

        // Limpiar errores
        errorText.text = "";
        errorRegText.text = "";

        // Botones login
        loginButton.onClick.AddListener(OnLoginClick);
        registerButton.onClick.AddListener(OnRegisterPanelOpen);

        // Botones registro
        confirmRegisterButton.onClick.AddListener(OnRegisterClick);
        backToLoginButton.onClick.AddListener(OnBackToLogin);

        // Eventos AuthManager
        AuthManager.OnLoginSuccess += OnLoginSuccess;
        AuthManager.OnLoginFailed += OnLoginFailed;
        AuthManager.OnRegisterSuccess += OnRegisterSuccess;
        AuthManager.OnRegisterFailed += OnRegisterFailed;
    }

    void OnDestroy()
    {
        AuthManager.OnLoginSuccess -= OnLoginSuccess;
        AuthManager.OnLoginFailed -= OnLoginFailed;
        AuthManager.OnRegisterSuccess -= OnRegisterSuccess;
        AuthManager.OnRegisterFailed -= OnRegisterFailed;
    }

    // ── LOGIN ──────────────────────────────────────────────
    private void OnLoginClick()
    {
        string username = usernameInput.text.Trim();
        string password = passwordInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError(errorText, "Rellena todos los campos.");
            return;
        }

        SetLoginButtons(false);
        errorText.text = "";
        AuthManager.Instance.Login(username, password);
    }

    private void OnLoginSuccess(string username)
    {
        SetLoginButtons(true);
        SceneManager.LoadScene("LobbyScene");
    }

    private void OnLoginFailed(string error)
    {
        SetLoginButtons(true);
        ShowError(errorText, error);
    }

    // ── REGISTRO ───────────────────────────────────────────
    private void OnRegisterPanelOpen()
    {
        ClearRegisterFields();
        loginPanel.SetActive(false);
        registerPanel.SetActive(true);
    }

    private void OnBackToLogin()
    {
        ClearLoginFields();
        registerPanel.SetActive(false);
        loginPanel.SetActive(true);
    }

    private void OnRegisterClick()
    {
        string username = usernameRegInput.text.Trim();
        string password = passwordRegInput.text;
        string confirm = passwordConfirmInput.text;

        if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
        {
            ShowError(errorRegText, "Rellena todos los campos.");
            return;
        }

        if (password != confirm)
        {
            ShowError(errorRegText, "Las contraseñas no coinciden.");
            return;
        }

        if (password.Length < 6)
        {
            ShowError(errorRegText, "La contraseña debe tener al menos 6 caracteres.");
            return;
        }

        SetRegisterButtons(false);
        errorRegText.text = "";
        AuthManager.Instance.Register(username, password);
    }

    private void OnRegisterSuccess(string message)
    {
        SetRegisterButtons(true);
        ClearLoginFields();
        // Volvemos al login con mensaje de éxito
        registerPanel.SetActive(false);
        loginPanel.SetActive(true);
        errorText.color = Color.green;
        errorText.text = "¡Guerrero creado! Ya puedes entrar.";
    }

    private void OnRegisterFailed(string error)
    {
        SetRegisterButtons(true);
        ShowError(errorRegText, error);
    }

    // ── HELPERS ────────────────────────────────────────────
    private void ShowError(TMP_Text target, string message)
    {
        target.color = new Color(0.784f, 0.298f, 0.165f); // #C84C2A
        target.text = message;
    }

    private void SetLoginButtons(bool value)
    {
        loginButton.interactable = value;
        registerButton.interactable = value;
    }

    private void SetRegisterButtons(bool value)
    {
        confirmRegisterButton.interactable = value;
        backToLoginButton.interactable = value;
    }

    private void ClearLoginFields()
    {
        usernameInput.text = "";
        passwordInput.text = "";
        errorText.text = "";
    }

    private void ClearRegisterFields()
    {
        usernameRegInput.text = "";
        passwordRegInput.text = "";
        passwordConfirmInput.text = "";
        errorRegText.text = "";
    }
}