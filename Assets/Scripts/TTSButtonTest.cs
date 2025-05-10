// TTSButtonTest.cs
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TTSButtonTest : MonoBehaviour
{
    public AliyunTTSWebSocket tts;

    [TextArea(5, 15)]
    public string longText = @"阳光透过树叶洒在青石小道上，远处传来孩子们的欢笑声。...";

    public void OnClickRun()
    {
        if (tts == null) return;
        // 拆分成 ≤20 字小段
        string[] segs = SplitToChunks(longText.Trim());
        tts.EnqueueSegments(segs);
    }

    private string[] SplitToChunks(string text)
    {
        var parts = Regex.Split(text, @"(?<=[，。？！])");
        var list = new List<string>();
        foreach (var raw in parts)
        {
            string s = raw.Trim();
            if (s.Length == 0) continue;
            if (s.Length <= 20) list.Add(s);
            else
            {
                int i = 0;
                while (i < s.Length)
                {
                    int len = Mathf.Min(20, s.Length - i);
                    list.Add(s.Substring(i, len));
                    i += len;
                }
            }
        }
        return list.ToArray();
    }
}
