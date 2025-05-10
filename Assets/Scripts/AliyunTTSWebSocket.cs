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
    private bool sessionReady = false;    // 握手完成后设为 true
    private Queue<string> textQueue = new Queue<string>();
    private PCMAudioStreamPlayer audioPlayer;

    public bool IsConnected => ws != null && ws.IsOpen;

    void Awake()
    {
        audioPlayer = GetComponent<PCMAudioStreamPlayer>();
    }

    /// <summary>
    /// 一次性把所有段落入队，然后开始或继续播放
    /// </summary>
    public void EnqueueSegments(IEnumerable<string> segments)
    {
        textQueue.Clear();
        foreach (var s in segments)
            textQueue.Enqueue(s.Trim());

        // 首段播放前清空一次缓存
        audioPlayer.ClearQueue();

        Debug.Log($"[TTS] EnqueueSegments → 共 {textQueue.Count} 段");

        if (!IsConnected)
            Connect();
        else if (sessionReady)
            SendNext();
    }

    /// <summary>中断当前合成</summary>
    // AliyunTTSWebSocket.cs
    public void StopSynthesis()
    {
        if (!IsConnected) return;
        Debug.Log("[TTS] ⏹ StopSynthesis called – 强制中断");

        // 1) 发 StopSynthesis 给服务端
        SendControl("StopSynthesis");

        // 2) 立即清空本地所有未播放的 PCM
        audioPlayer.ClearQueue();

        // 3) 立即暂停 AudioSource 播放
        audioPlayer.StopPlayback();

        // 4) 重置会话状态，下一次要继续还需重新握手
        sessionReady = false;
    }


    /// <summary>建立 WS 连接并 StartSynthesis</summary>
    public void Connect()
    {
        if (IsConnected) return;
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
        Debug.Log("🟢 WS Opened → StartSynthesis");
        taskId = Guid.NewGuid().ToString("N");
        SendControl("StartSynthesis", new Dictionary<string, object>{
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

        if (msg.Contains("SynthesisStarted"))
        {
            sessionReady = true;
            Debug.Log("[TTS] ✅ SynthesisStarted");
            SendNext();
        }
        else if (msg.Contains("SentenceEnd") || msg.Contains("SynthesisCompleted"))
        {
            Debug.Log("[TTS] 🔸 段落播放完毕");
            if (textQueue.Count > 0)
            {
                // 先终止当前会话，然后触发 OnClosed → 重连 → 再发下一段
                SendControl("StopSynthesis");
                ws.Close();
            }
            else
            {
                Debug.Log("[TTS] ☑ 所有段落已完成，等待服务器空闲断开");
            }
        }
        else if (msg.Contains("TaskFailed"))
        {
            Debug.LogWarning("[TTS] ❌ TaskFailed – 尝试重连");
            sessionReady = false;
            ws.Close();
        }
    }

    private void SendNext()
    {
        if (!sessionReady || textQueue.Count == 0) return;

        string next = textQueue.Dequeue();
        Debug.Log($"[TTS] ▶ RunSynthesis → “{next}” (剩 {textQueue.Count})");
        SendControl("RunSynthesis", new Dictionary<string, object> { { "text", next } });
    }

    private void OnBinary(WebSocket w, BufferSegment bs)
    {
        if (bs.Count == 0) return;
        var pcm = new byte[bs.Count];
        Array.Copy(bs.Data, bs.Offset, pcm, 0, bs.Count);
        audioPlayer.PushPCM(pcm);
    }

    private void OnClosed(WebSocket w, WebSocketStatusCodes code, string reason)
    {
        Debug.LogError($"🔴 WS Closed ({(int)code}) {reason}");
        sessionReady = false;

        // 会话关闭后，若还有剩余段，则自动重连，继续播放下一段
        if (textQueue.Count > 0)
        {
            Debug.Log("[TTS] ▶ 会话关闭，重连继续播放下一段");
            Connect();
        }
    }

    private void SendControl(string name, Dictionary<string, object> payload = null)
    {
        var header = new Dictionary<string, object>{
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
