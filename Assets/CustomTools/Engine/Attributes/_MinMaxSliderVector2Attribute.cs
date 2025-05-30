namespace UnityEngine
{
    public class _MinMaxSliderVector2Attribute : PropertyAttribute
    {
        public float minLimit;
        public float maxLimit;

        public _MinMaxSliderVector2Attribute(float minLimit, float maxLimit)
        {
            this.minLimit = minLimit;
            this.maxLimit = maxLimit;
        }
    }
}