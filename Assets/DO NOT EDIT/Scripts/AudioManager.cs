using System.Collections;
using System.Collections.Generic;
using UnityEngine;

using UnityEngine;
using System.Collections;

public class AudioManager : MonoBehaviour
{
    public AudioSource musicSource;
    public AudioSource sfxSource;

    public AudioClip[] musicClips;
    public AudioClip[] sfxClips;

    public Vector2 musicDelayRange = new Vector2(60f, 180f);  // 1 to 3 minutes
    public Vector2 sfxDelayRange = new Vector2(5f, 20f);      // 5 to 20 seconds

    private void Start()
    {
        StartCoroutine(PlayMusicLoop());
        StartCoroutine(PlayRandomSFX());
    }

    private IEnumerator PlayMusicLoop()
    {
        while (true)
        {
            // Pick a random track and play it
            AudioClip clip = musicClips[Random.Range(0, musicClips.Length)];
            musicSource.clip = clip;
            musicSource.Play();

            // Wait for the song to end
            yield return new WaitForSeconds(clip.length);

            // Wait random time before next song
            float delay = Random.Range(musicDelayRange.x, musicDelayRange.y);
            yield return new WaitForSeconds(delay);
        }
    }

    private IEnumerator PlayRandomSFX()
    {
        while (true)
        {
            float delay = Random.Range(sfxDelayRange.x, sfxDelayRange.y);
            yield return new WaitForSeconds(delay);

            AudioClip sfx = sfxClips[Random.Range(0, sfxClips.Length)];
            sfxSource.PlayOneShot(sfx);
        }
    }
}
