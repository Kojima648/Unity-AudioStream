// PCMAudioStreamPlayer.cs
using UnityEngine;
using System.Collections.Concurrent;

[RequireComponent(typeof(AudioSource))]
public class PCMAudioStreamPlayer : MonoBehaviour
{
    public int sampleRate = 24000;
    public int channels = 1;
    public int maxDurationSeconds = 300;

    private ConcurrentQueue<float> audioBuffer = new ConcurrentQueue<float>();
    private AudioSource audioSource;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = true;

        int samples = sampleRate * maxDurationSeconds;
        var clip = AudioClip.Create("StreamClip", samples, channels, sampleRate, true, OnAudioRead);
        audioSource.clip = clip;
        audioSource.Play();
    }

    public void PushPCM(byte[] pcmBytes)
    {
        for (int i = 0; i < pcmBytes.Length; i += 2)
        {
            short s = (short)(pcmBytes[i] | (pcmBytes[i + 1] << 8));
            audioBuffer.Enqueue(s / 32768f);
        }
    }

    private void OnAudioRead(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
            data[i] = audioBuffer.TryDequeue(out var v) ? v : 0f;
    }

    /// <summary>清空所有未播放的 PCM（每次新一轮播放前调用）</summary>
    public void ClearQueue()
    {
        while (audioBuffer.TryDequeue(out _)) { }
        Debug.Log("[AudioPlayer] 🔄 PCM 缓冲已清空");
    }

    /// <summary>暂停播放（保留已缓冲 PCM）</summary>
    public void PausePlayback()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            Debug.Log("[AudioPlayer] ⏸ Playback paused");
        }
    }
}
