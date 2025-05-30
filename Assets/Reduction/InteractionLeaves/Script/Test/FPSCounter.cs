using UnityEngine;
using UnityEngine.UI;

public class FPSCounter : MonoBehaviour
{
    [Header("显示设置")]
    public Text fpsText;            // 用于显示FPS的UI Text组件
    public float updateInterval = 0.5f; // 更新频率（秒）
    public bool showMinMax = true;  // 是否显示最低/最高FPS
    public Color goodFpsColor = Color.green;    // >60 FPS颜色
    public Color normalFpsColor = Color.yellow;// 30-60 FPS颜色
    public Color badFpsColor = Color.red;      // <30 FPS颜色

    private float accum = 0f;       // FPS累计值
    private int frames = 0;         // 帧数计数器
    private float timeLeft;         // 下次更新时间
    private float fps = 0f;         // 当前FPS
    private float minFps = float.MaxValue; // 最低FPS
    private float maxFps = 0f;      // 最高FPS

    void Start()
    {
        // 如果没有指定Text组件，尝试自动查找
        if (fpsText == null)
        {
            fpsText = GetComponent<Text>();
            if (fpsText == null)
            {
                // 如果没有Text组件，创建一个新的
                GameObject textObj = new GameObject("FPS Display");
                textObj.transform.SetParent(transform);
                fpsText = textObj.AddComponent<Text>();
                
                // 设置默认字体（如果项目中包含Arial）
                Font font = Resources.GetBuiltinResource<Font>("Arial.ttf");
                if (font != null)
                {
                    fpsText.font = font;
                }
                
                fpsText.fontSize = 24;
                fpsText.alignment = TextAnchor.UpperLeft;
                fpsText.color = Color.white;
                
                // 添加RectTransform设置
                RectTransform rect = textObj.GetComponent<RectTransform>();
                rect.anchorMin = new Vector2(0, 1);
                rect.anchorMax = new Vector2(0, 1);
                rect.pivot = new Vector2(0, 1);
                rect.anchoredPosition = new Vector2(10, -10);
                rect.sizeDelta = new Vector2(200, 30);
            }
        }

        timeLeft = updateInterval;
    }

    void Update()
    {
        timeLeft -= Time.deltaTime;
        accum += Time.timeScale / Time.deltaTime;
        frames++;

        // 更新FPS显示
        if (timeLeft <= 0f)
        {
            fps = accum / frames;
            
            // 更新最低/最高FPS
            if (fps < minFps) minFps = fps;
            if (fps > maxFps) maxFps = fps;

            // 根据FPS设置颜色
            if (fps > 60) fpsText.color = goodFpsColor;
            else if (fps > 30) fpsText.color = normalFpsColor;
            else fpsText.color = badFpsColor;

            // 更新显示文本
            if (showMinMax)
            {
                fpsText.text = string.Format("FPS: {0:F1}\nMin: {1:F1}\nMax: {2:F1}", 
                    fps, minFps, maxFps);
            }
            else
            {
                fpsText.text = string.Format("FPS: {0:F1}", fps);
            }

            // 重置计数器
            timeLeft = updateInterval;
            accum = 0f;
            frames = 0;
        }
    }

    // 可选：在游戏结束时重置最低/最高FPS
    public void ResetMinMax()
    {
        minFps = float.MaxValue;
        maxFps = 0f;
    }
}