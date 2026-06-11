using System.Collections;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public PlayerController player;
    public TextMesh messageText;     // child of the camera, used for respawn/checkpoint messages
    public float globalKillY = -26f;

    Vector3 respawnPoint;
    bool dying;
    Coroutine messageRoutine;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        respawnPoint = player.transform.position;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.R) && !dying)
        {
            player.ResetAt(respawnPoint);
        }

        if (!dying && player.transform.position.y < globalKillY)
            Kill("Careful! It's a long way down...");
    }

    public void SetCheckpoint(Vector3 point, bool isGoal)
    {
        respawnPoint = point;
        ShowMessage(isGoal ? "You made it! ♥" : "Checkpoint!", isGoal ? new Color(0.96f, 0.77f, 0.36f) : new Color(0.62f, 0.78f, 0.57f), isGoal ? 6f : 1.5f);
    }

    public void Kill(string message)
    {
        if (dying) return;
        StartCoroutine(KillRoutine(message));
    }

    IEnumerator KillRoutine(string message)
    {
        dying = true;
        yield return StartCoroutine(player.DeathCrumple());
        player.ResetAt(respawnPoint);
        dying = false;
        if (!string.IsNullOrEmpty(message))
            ShowMessage(message, new Color(0.88f, 0.19f, 0.19f), 2f);
    }

    public void ShowMessage(string text, Color color, float duration)
    {
        if (messageText == null) return;
        if (messageRoutine != null) StopCoroutine(messageRoutine);
        messageRoutine = StartCoroutine(MessageRoutine(text, color, duration));
    }

    IEnumerator MessageRoutine(string text, Color color, float duration)
    {
        messageText.text = text;
        messageText.color = color;
        yield return new WaitForSeconds(duration);
        float t = 0f;
        while (t < 0.4f)
        {
            t += Time.deltaTime;
            var c = color; c.a = 1f - t / 0.4f;
            messageText.color = c;
            yield return null;
        }
        messageText.text = "";
    }
}
