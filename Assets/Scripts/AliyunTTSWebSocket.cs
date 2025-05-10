// AliyunTTSWebSocket.cs
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
    private Queue<string> textQueue = new Queue<string>();
    private PCMAudioStreamPlayer audioPlayer;

    public bool IsConnected => ws != null && ws.IsOpen;

    void Awake()
    {
        audioPlayer = GetComponent<PCMAudioStreamPlayer>();
    }

    /// <summary>
    /// 入队所有段落并启动循环会话
    /// </summary>
    public void EnqueueSegments(IEnumerable<string> segments)
    {
        manualStopped = false;
        textQueue.Clear();
        foreach (var s in segments) textQueue.Enqueue(s.Trim());

        // 首次清空旧缓冲
        audioPlayer.ClearQueue();

        Debug.Log($"[TTS] EnqueueSegments → {textQueue.Count} 段入队");
        if (!IsConnected)
            Connect();      // 建立连接后 OnOpen 会触发 StartSession
        else
            StartSession(); // 已连则直接启动第一段
    }

    /// <summary>
    /// 手动中断：立即暂停、发送 StopSynthesis 并阻止后续
    /// </summary>
    public void StopSynthesis()
    {
        if (!IsConnected) return;
        manualStopped = true;
        Debug.Log("[TTS] ⏹ Manual StopSynthesis");
        SendControl("StopSynthesis");
        audioPlayer.PausePlayback();
    }

    /// <summary>
    /// 建立 WS 并在 OnOpen 后启动会话
    /// </summary>
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

    /// <summary>
    /// 为当前队头段落创建新会话
    /// </summary>
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
        Debug.Log("[WS Msg] " + msg);
        if (manualStopped) return;

        if (msg.Contains("\"name\":\"SynthesisStarted\""))
        {
            // 握手完成，发本段
            if (textQueue.Count > 0)
            {
                var next = textQueue.Dequeue();
                Debug.Log($"[TTS] ▶ RunSynthesis “{next}” ({textQueue.Count} 段剩余)");
                SendControl("RunSynthesis", new Dictionary<string, object> { { "text", next } });
            }
        }
        else if (msg.Contains("\"name\":\"SentenceEnd\""))
        {
            // 本段结束，发 StopSynthesis
            Debug.Log("[TTS] 🔸 SentenceEnd – segment done, StopSynthesis");
            SendControl("StopSynthesis");
        }
        else if (msg.Contains("\"name\":\"SynthesisCompleted\""))
        {
            // 服务端确认本段全结束，立即新一轮
            Debug.Log("[TTS] ✅ SynthesisCompleted – starting next");
            StartSession();
        }
    }

    private void OnBinary(WebSocket w, BufferSegment bs)
    {
        if (manualStopped || bs.Count == 0) return;
        var pcm = new byte[bs.Count];
        Array.Copy(bs.Data, bs.Offset, pcm, 0, bs.Count);
        audioPlayer.PushPCM(pcm);
    }

    private void OnClosed(WebSocket w, WebSocketStatusCodes code, string reason)
    {
        Debug.LogError($"🔴 WS Closed ({(int)code}) {reason}");
    }

    private void SendControl(string name, Dictionary<string, object> payload = null)
    {
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
