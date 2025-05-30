namespace UnityEngine
{
    public class _ExponentialSliderAttribute : PropertyAttribute
    {
        public float min;
        public float max;
        public float power;
        public bool isExponential;
        public bool isLinearPower; // 是否使用线性幂数模式
        public int decimalPlaces;

        // 指数级滑块构造函数
        public _ExponentialSliderAttribute(float min, float max, int decimalPlaces = 3)
        {
            this.min = min;
            this.max = max;
            this.isExponential = true;
            this.isLinearPower = false;
            this.decimalPlaces = decimalPlaces;
        }

        // 幂数级滑块构造函数
        public _ExponentialSliderAttribute(float min, float max, float power, bool isLinearPower = false, int decimalPlaces = 3)
        {
            this.min = min;
            this.max = max;
            this.power = power;
            this.isExponential = false;
            this.isLinearPower = isLinearPower;
            this.decimalPlaces = decimalPlaces;
        }
    }
}