﻿using UnityEngine;
using System;
using System.Collections.Concurrent;

[RequireComponent(typeof(AudioSource))]
public class PCMAudioStreamPlayer : MonoBehaviour
{
    public int sampleRate = 24000;
    public int channels = 1;
    public int maxDurationSeconds = 300;

    public Action OnPlaybackComplete;

    private ConcurrentQueue<float> audioBuffer = new ConcurrentQueue<float>();
    private AudioSource audioSource;

    private int totalSamplesReceived = 0;
    private int totalSamplesPlayed = 0;
    private bool waitingForComplete = false;

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
        int sampleCount = pcmBytes.Length / 2;

        for (int i = 0; i < pcmBytes.Length; i += 2)
        {
            short s = (short)(pcmBytes[i] | (pcmBytes[i + 1] << 8));
            audioBuffer.Enqueue(s / 32768f);
        }

        totalSamplesReceived += sampleCount;
        waitingForComplete = true;
    }

    private void OnAudioRead(float[] data)
    {
        for (int i = 0; i < data.Length; i++)
        {
            data[i] = audioBuffer.TryDequeue(out var v) ? v : 0f;
            if (waitingForComplete)
                totalSamplesPlayed++;
        }

        if (waitingForComplete && totalSamplesPlayed >= totalSamplesReceived)
        {
            waitingForComplete = false;
            Debug.Log("[AudioPlayer] ✅ 播放完成，触发回调");
            OnPlaybackComplete?.Invoke();
        }
    }

    public void ClearQueue()
    {
        while (audioBuffer.TryDequeue(out _)) { }

        totalSamplesReceived = 0;
        totalSamplesPlayed = 0;
        waitingForComplete = false;

        Debug.Log("[AudioPlayer] 🔄 PCM 缓冲已清空");
    }

    public void PausePlayback()
    {
        if (audioSource.isPlaying)
        {
            audioSource.Pause();
            Debug.Log("[AudioPlayer] ⏸ Playback paused");
        }
    }

    public void ResumePlayback()
    {
        if (!audioSource.isPlaying)
        {
            audioSource.Play();
            Debug.Log("[AudioPlayer] ▶️ Resume playback");
        }
    }

    public void ResetClip()
    {
        audioSource.Stop();
        int samples = sampleRate * maxDurationSeconds;
        var clip = AudioClip.Create("StreamClip", samples, channels, sampleRate, true, OnAudioRead);
        audioSource.clip = clip;
        audioSource.loop = true;
        audioSource.Play();

        totalSamplesReceived = 0;
        totalSamplesPlayed = 0;
        waitingForComplete = false;

        Debug.Log("[AudioPlayer] 🔄 AudioClip 已重置");
    }

    public bool IsQueueEmpty()
    {
        return audioBuffer.IsEmpty;
    }

    public bool IsPlaying()
    {
        return audioSource != null && audioSource.isPlaying;
    }
}
