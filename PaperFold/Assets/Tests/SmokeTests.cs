using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SmokeTests
{
    [UnityTest]
    public IEnumerator LevelLoads_PlayerSimulates_AndKillRespawnWorks()
    {
        SceneManager.LoadScene("Main");
        yield return null; // let the scene load and Awake/Start run

        var gm = Object.FindFirstObjectByType<GameManager>();
        Assert.IsNotNull(gm, "GameManager missing from scene");
        var player = Object.FindFirstObjectByType<PlayerController>();
        Assert.IsNotNull(player, "Player missing from scene");

        Vector3 start = player.transform.position;

        // simulate ~2 seconds of physics; the player should settle on the ground, not fall through
        yield return new WaitForSeconds(2f);
        Assert.Greater(player.transform.position.y, gm.globalKillY,
            "Player fell through the starting ground");
        Assert.Less(Mathf.Abs(player.transform.position.x - start.x), 2f,
            "Player drifted with no input");

        // form switching: paper -> ball -> paper round trip
        Assert.IsTrue(player.TrySetForm(PaperForm.Ball), "Could not fold into ball");
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(PaperForm.Ball, player.form);
        Assert.IsTrue(player.TrySetForm(PaperForm.Paper), "Could not unfold back to paper");
        yield return new WaitForFixedUpdate();
        Assert.AreEqual(PaperForm.Paper, player.form);

        // kill/respawn round trip: drop the player below the global kill line
        Vector3 respawnReference = player.transform.position;
        player.Body.position = new Vector2(respawnReference.x, gm.globalKillY - 5f);
        yield return new WaitForSeconds(1.5f); // death crumple + respawn

        Assert.Greater(player.transform.position.y, gm.globalKillY,
            "Player was not respawned after falling below the kill line");
        Assert.Less(Vector3.Distance(player.transform.position, respawnReference), 5f,
            "Player did not respawn near the start checkpoint");
    }

    // The chute is the one mandatory ball move: even with NO input, a ball
    // dropped into the chute mouth must launch off the kicker and clear the
    // 12.5-wide shredder pit (landing platform starts at x=154, top -29.5).
    [UnityTest]
    public IEnumerator BallChute_NoInputLaunchClearsThePit()
    {
        SceneManager.LoadScene("Main");
        yield return null;

        var player = Object.FindFirstObjectByType<PlayerController>();
        player.Body.position = new Vector2(92f, 5.5f); // just inside the chute mouth
        player.Body.linearVelocity = Vector2.zero;
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(player.TrySetForm(PaperForm.Ball), "Could not fold into ball");

        float deadline = Time.time + 12f;
        float maxX = -99f;
        Vector2 lipVelocity = Vector2.zero;
        while (Time.time < deadline && player.transform.position.x < 155.5f &&
               player.transform.position.x > 0f) // death respawns near x=-12
        {
            if (player.transform.position.x > maxX)
            {
                maxX = player.transform.position.x;
                if (lipVelocity == Vector2.zero && maxX > 141.5f)
                    lipVelocity = player.Body.linearVelocity;
            }
            yield return null;
        }

        Assert.Greater(player.transform.position.x, 155f,
            $"Ball did not clear the pit (max x {maxX:F2}, lip velocity {lipVelocity})");
        Assert.Greater(player.transform.position.y, -31f, "Ball ended up inside the pit");
    }

    // The glide home must be makeable at minimum airspeed: fold into a plane
    // just past the runway edge with no steering input and reach the final
    // platform (x[428..470], top -38).
    [UnityTest]
    public IEnumerator PlaneGlide_NoInputReachesTheFinalPlatform()
    {
        SceneManager.LoadScene("Main");
        yield return null;

        var player = Object.FindFirstObjectByType<PlayerController>();
        player.Body.position = new Vector2(319f, -9f); // airborne, just off the runway
        player.Body.linearVelocity = new Vector2(8f, 0f);
        yield return new WaitForFixedUpdate();
        Assert.IsTrue(player.TrySetForm(PaperForm.Plane), "Could not fold into plane");

        float deadline = Time.time + 25f;
        while (Time.time < deadline && player.form == PaperForm.Plane &&
               player.transform.position.y > -45f)
            yield return null; // landing auto-unfolds back to paper

        Assert.Greater(player.transform.position.x, 428f, "Plane fell short of the final platform");
        Assert.Greater(player.transform.position.y, -39f, "Plane sank below the final platform");
    }
}
