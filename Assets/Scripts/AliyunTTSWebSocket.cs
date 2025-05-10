using UnityEngine;
using System;
using System.Collections.Generic;
using Best.WebSockets;
using Best.HTTP.Shared.PlatformSupport.Memory;

[RequireComponent(typeof(PCMAudioStreamPlayer))]
public class AliyunTTSWebSocket : MonoBehaviour
{
    [Header("Aliyun AppKey & Token")]
    public string appKey = "Y6QL45Jf8ow3n7sx";
    public string token = "7d7afc19d7c344e592a58b006f8245c7";

    private WebSocket ws;
    private string taskId;
    private bool manualStopped = false;
    private Queue<string> textQueue = new();
    private PCMAudioStreamPlayer audioPlayer;

    public Action OnPlaybackComplete;

    public bool IsConnected => ws != null && ws.IsOpen;

    void Awake()
    {
        audioPlayer = GetComponent<PCMAudioStreamPlayer>();

        // 注册音频播放完成的回调
        audioPlayer.OnPlaybackComplete = () =>
        {
            if (!manualStopped)
            {
                Debug.Log("[TTS] ✅ 播放全部完成");
                OnPlaybackComplete?.Invoke();
            }
            else
            {
                Debug.Log("[TTS] ⏹ 播放中断，不触发完成回调");
            }
        };
    }

    public void EnqueueSegments(IEnumerable<string> segments)
    {
        ResetState();
        manualStopped = false;

        foreach (var s in segments)
            textQueue.Enqueue(s.Trim());

        audioPlayer.ClearQueue();
        audioPlayer.ResetClip();

        Debug.Log($"[TTS] EnqueueSegments → {textQueue.Count} 段入队");

        if (!IsConnected)
            Connect();
        else
            StartSession();
    }

    public void StopSynthesis()
    {
        if (!IsConnected) return;

        manualStopped = true;

        textQueue.Clear();
        audioPlayer.ClearQueue();
        audioPlayer.PausePlayback();

        Debug.Log("[TTS] ⏹ Manual Stop");

        SendControl("StopSynthesis");

        ws.Close();
        ws = null;
    }

    public void ResetState()
    {
        manualStopped = false;
        textQueue.Clear();
        audioPlayer.ClearQueue();

        if (ws != null)
        {
            ws.OnOpen -= OnOpen;
            ws.OnMessage -= OnMsg;
            ws.OnBinary -= OnBinary;
            ws.OnClosed -= OnClosed;
            ws.Close();
            ws = null;
        }
    }

    private void Connect()
    {
        Debug.Log("[TTS] ▶ Connecting WebSocket...");
        ws = new WebSocket(new Uri($"wss://nls-gateway-cn-beijing.aliyuncs.com/ws/v1?token={token}"));
        ws.OnOpen += OnOpen;
        ws.OnMessage += OnMsg;
        ws.OnBinary += OnBinary;
        ws.OnClosed += OnClosed;
        ws.Open();
    }

    private void OnOpen(WebSocket w)
    {
        Debug.Log("🟢 WS Opened");
        StartSession();
    }

    private void StartSession()
    {
        if (manualStopped || textQueue.Count == 0) return;

        taskId = Guid.NewGuid().ToString("N");
        Debug.Log("[TTS] ▶ StartSynthesis");

        SendControl("StartSynthesis", new Dictionary<string, object> {
            {"voice","zhixiaoxia"},
            {"format","PCM"},
            {"sample_rate",24000},
            {"volume",100},
            {"speech_rate",0},
            {"pitch_rate",0},
            {"enable_subtitle",false},
            {"platform","unity"}
        });
    }

    private void OnMsg(WebSocket w, string msg)
    {
        if (manualStopped) return;

        Debug.Log("[WS Msg] " + msg);

        if (msg.Contains("\"name\":\"SynthesisStarted\""))
        {
            if (textQueue.Count > 0)
            {
                var next = textQueue.Dequeue();
                Debug.Log($"[TTS] ▶ RunSynthesis “{next}” ({textQueue.Count} 段剩余)");
                SendControl("RunSynthesis", new Dictionary<string, object> { { "text", next } });
            }
        }
        else if (msg.Contains("\"name\":\"SentenceEnd\""))
        {
            Debug.Log("[TTS] 🔸 SentenceEnd – segment done, StopSynthesis");
            SendControl("StopSynthesis");
        }
        else if (msg.Contains("\"name\":\"SynthesisCompleted\""))
        {
            Debug.Log("[TTS] ✅ SynthesisCompleted");

            if (textQueue.Count > 0)
                StartSession();
            // 否则等待 audioPlayer 自动触发 OnPlaybackComplete
        }
    }

    private void OnBinary(WebSocket w, BufferSegment bs)
    {
        if (manualStopped || bs.Count == 0 || !IsConnected) return;

        var pcm = new byte[bs.Count];
        Array.Copy(bs.Data, bs.Offset, pcm, 0, bs.Count);
        audioPlayer.PushPCM(pcm);
        audioPlayer.ResumePlayback();
    }

    private void OnClosed(WebSocket w, WebSocketStatusCodes code, string reason)
    {
        Debug.LogWarning($"🔴 WS Closed ({(int)code}) {reason}");
        ws = null;
    }

    private void SendControl(string name, Dictionary<string, object> payload = null)
    {
        if (!IsConnected) return;

        var header = new Dictionary<string, object> {
            {"message_id", Guid.NewGuid().ToString("N")},
            {"task_id", taskId},
            {"namespace","FlowingSpeechSynthesizer"},
            {"name", name},
            {"appkey", appKey}
        };
        var msg = new Dictionary<string, object> { { "header", header } };
        if (payload != null) msg["payload"] = payload;
        ws.Send(MiniJSON.Json.Serialize(msg));
        Debug.Log($"[TTS] 📤 {name}");
    }

    void OnDestroy()
    {
        if (IsConnected) ws.Close();
    }
}
