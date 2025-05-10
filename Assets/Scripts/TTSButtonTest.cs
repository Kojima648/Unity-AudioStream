// TTSButtonTest.cs
using UnityEngine;
using System.Collections.Generic;
using System.Text.RegularExpressions;

public class TTSButtonTest : MonoBehaviour
{
    public AliyunTTSWebSocket tts;

    [TextArea(5, 15)]
    public string longText = @"秋日的清晨，薄雾轻轻地笼罩在山谷之间，阳光透过树梢洒下斑驳的金色光斑……";

    public void OnClickRun()
    {
        var segs = SplitToChunks(longText.Trim());
        tts.EnqueueSegments(segs);
    }

    public void OnClickStop()
    {
        tts?.StopSynthesis();
    }

    private string[] SplitToChunks(string text)
    {
        var parts = Regex.Split(text, @"(?<=[，。？！])");
        var list = new List<string>();
        foreach (var raw in parts)
        {
            var s = raw.Trim();
            if (s.Length == 0) continue;
            if (s.Length <= 20) list.Add(s);
            else
            {
                for (int i = 0; i < s.Length; i += 20)
                    list.Add(s.Substring(i, Mathf.Min(20, s.Length - i)));
            }
        }
        return list.ToArray();
    }
}
