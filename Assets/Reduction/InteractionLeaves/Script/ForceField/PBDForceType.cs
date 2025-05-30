namespace UnityEngine.PBD
{
    
    public enum PBDForceType
    {
        None,
        // Gravity,
        Vortex,//旋涡
        Repulsion,//斥力
        BernoulliForce,//空气动力？
        DelayBernoulliForce,
        Viscosity,//减速
        ViscosityHeart,//减速
    }
    
    public enum PBDForceApplicationOrder
    {
        PreDynamics,    // 动力学前（如风力重力）
        Dynamics,       // 动力学中（如涡旋力）
        PostDynamics    // 动力学后（如阻尼力）
    }
}