using UnityEngine;

[RequireComponent(typeof(BoxCollider2D))]
public class Checkpoint : MonoBehaviour
{
    public Sprite offSprite;
    public Sprite onSprite;
    public bool unlocksDoubleJump;
    public bool isGoal;

    SpriteRenderer sr;
    bool activated;

    void Awake()
    {
        sr = GetComponent<SpriteRenderer>();
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (activated) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (player == null) return;

        activated = true;
        sr.sprite = onSprite;
        // respawn just above the flag base
        GameManager.Instance.SetCheckpoint(transform.position + Vector3.up * 0.5f, isGoal);

        if (unlocksDoubleJump && !player.doubleJumpUnlocked)
        {
            player.doubleJumpUnlocked = true;
            GameManager.Instance.ShowMessage("DOUBLE JUMP unlocked!\nPress SPACE again in mid-air", new Color(0.62f, 0.78f, 0.57f), 3f);
        }
    }
}
