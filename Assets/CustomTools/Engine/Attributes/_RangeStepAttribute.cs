namespace UnityEngine
{
    public class _RangeStepAttribute : PropertyAttribute
    {
        public readonly float min;
        public readonly float max;
        public readonly float step;
    
        /// <param name="min">最小值</param>
        /// <param name="max">最大值</param>
        /// <param name="step">变化步长</param>
        public _RangeStepAttribute(float min, float max, float step = 1)
        {
            this.min = min;
            this.max = max;
            this.step = Mathf.Max(step, 0.001f); // 最小步长限制
        }
    }
}