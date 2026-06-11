using UnityEngine;

// Fades its TextMesh in when the player is near, so the world stays quiet
// until you walk up to a sign — curiosity first, instructions second.
public class TutorialSign : MonoBehaviour
{
    public TextMesh label;
    public float fullDistance = 7f;
    public float fadeDistance = 12f;

    Transform player;
    Color baseColor;

    void Start()
    {
        baseColor = label.color;
        if (GameManager.Instance != null && GameManager.Instance.player != null)
            player = GameManager.Instance.player.transform;
    }

    void Update()
    {
        if (player == null || label == null) return;
        float d = Vector2.Distance(player.position, transform.position);
        float a = 1f - Mathf.InverseLerp(fullDistance, fadeDistance, d);
        var c = baseColor; c.a = a;
        label.color = c;
    }
}
