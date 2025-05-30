using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.LowLevel;
using UnityEngine.PBD;

public class PBDUpdateSystem
{
    
    [RuntimeInitializeOnLoadMethod()]
    private static void Initialize()
    {
        var playerLoop = PlayerLoop.GetDefaultPlayerLoop();
        if(CheckRegist(ref playerLoop))
            return;
        
        PlayerLoopSystem afterEarlyUpdate = new PlayerLoopSystem()
        {
            type = typeof(PBDUpdateSystem),
            updateDelegate = () =>
            {
                if (InteractionOfLeavesManager.IsInstance()) 
                {
                    InteractionOfLeavesManager.Instance.AfterEarlyUpdate();
                }
            }
        };
        
        PlayerLoopSystem afterFixedUpdate = new PlayerLoopSystem()
        {
            type = typeof(PBDUpdateSystem),
            updateDelegate = () =>
            {
                if (InteractionOfLeavesManager.IsInstance()) 
                {
                    InteractionOfLeavesManager.Instance.AfterFixedUpdate();
                }
            }
        };

        PlayerLoopSystem afterUpdate = new PlayerLoopSystem()
        {
            type = typeof(PBDUpdateSystem),
            updateDelegate = () =>
            {
                if (InteractionOfLeavesManager.IsInstance()) 
                {
                    InteractionOfLeavesManager.Instance.AfterUpdate();
                }
            }
        };
        
        PlayerLoopSystem beforeLateUpdate = new PlayerLoopSystem()
        {
            type = typeof(PBDUpdateSystem),
            updateDelegate = () =>
            {
                if (InteractionOfLeavesManager.IsInstance()) 
                {
                    InteractionOfLeavesManager.Instance.BeforeLateUpdate();
                }
            }
        };
        
        PlayerLoopSystem afterLateUpdate = new PlayerLoopSystem()
        {
            type = typeof(PBDUpdateSystem),
            updateDelegate = () =>
            {
                if (InteractionOfLeavesManager.IsInstance()) 
                    InteractionOfLeavesManager.Instance.AfterLateUpdate();
            },
        };
        
        PlayerLoopSystem postLateUpdate = new PlayerLoopSystem()
        {
            type = typeof(PBDUpdateSystem),
            updateDelegate = () =>
            {
                if (InteractionOfLeavesManager.IsInstance()) 
                {
                    InteractionOfLeavesManager.Instance.PostLateUpdate();
                }
            }
        };

        PlayerLoopSystem afterRendering = new PlayerLoopSystem()
        {
            type = typeof(PBDUpdateSystem),
            updateDelegate = () =>
            {
                if (InteractionOfLeavesManager.IsInstance()) 
                {
                    InteractionOfLeavesManager.Instance.AfterRendering();
                }
            }
        };
        
        
        int sysIndex = 0;
        int index = 0;
        
        // // early update
        // sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == "EarlyUpdate");
        // PlayerLoopSystem earlyUpdateSystem = playerLoop.subSystemList[sysIndex];
        // var earlyUpdateSubsystemList = new List<PlayerLoopSystem>(earlyUpdateSystem.subSystemList);
        // earlyUpdateSubsystemList.Add(afterEarlyUpdate);
        // earlyUpdateSystem.subSystemList = earlyUpdateSubsystemList.ToArray();
        // playerLoop.subSystemList[sysIndex] = earlyUpdateSystem;
        //
        // // after fixed update
        // sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == "FixedUpdate");
        // PlayerLoopSystem fixedUpdateSystem = playerLoop.subSystemList[sysIndex];
        // var fixedUpdateSubsystemList = new List<PlayerLoopSystem>(fixedUpdateSystem.subSystemList);
        // index = fixedUpdateSubsystemList.FindIndex(h => h.type.Name.Contains("ScriptRunBehaviourFixedUpdate"));
        // fixedUpdateSubsystemList.Insert(index + 1, afterFixedUpdate); // FixedUpdate() after
        // fixedUpdateSystem.subSystemList = fixedUpdateSubsystemList.ToArray();
        // playerLoop.subSystemList[sysIndex] = fixedUpdateSystem;
        //
        //
        // // update
        // sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == "Update");
        // PlayerLoopSystem updateSystem = playerLoop.subSystemList[sysIndex];
        // var updateSubsystemList = new List<PlayerLoopSystem>(updateSystem.subSystemList);
        // index = updateSubsystemList.FindIndex(h => h.type.Name.Contains("ScriptRunDelayedDynamicFrameRate"));
        // updateSubsystemList.Insert(index + 1, afterUpdate); // Update() after
        // updateSystem.subSystemList = updateSubsystemList.ToArray();
        // playerLoop.subSystemList[sysIndex] = updateSystem;
        //
        // // late update
        // sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == "PreLateUpdate");
        // PlayerLoopSystem lateUpdateSystem = playerLoop.subSystemList[sysIndex];
        // var lateUpdateSubsystemList = new List<PlayerLoopSystem>(lateUpdateSystem.subSystemList);
        // index = lateUpdateSubsystemList.FindIndex(h => h.type.Name.Contains("ScriptRunBehaviourLateUpdate"));
        // lateUpdateSubsystemList.Insert(index, beforeLateUpdate); // LateUpdate() before
        // lateUpdateSubsystemList.Insert(index + 2, afterLateUpdate); // LateUpdate() after
        // //lateUpdateSubsystemList.Insert(index + 1, afterLateUpdate); // LateUpdate() after
        // lateUpdateSystem.subSystemList = lateUpdateSubsystemList.ToArray();
        // playerLoop.subSystemList[sysIndex] = lateUpdateSystem;
        //
        // // post late update
        // sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == "PostLateUpdate");
        // PlayerLoopSystem postLateUpdateSystem = playerLoop.subSystemList[sysIndex];
        // var postLateUpdateSubsystemList = new List<PlayerLoopSystem>(postLateUpdateSystem.subSystemList);
        // index = postLateUpdateSubsystemList.FindIndex(h => h.type.Name.Contains("ScriptRunDelayedDynamicFrameRate"));
        // postLateUpdateSubsystemList.Insert(index + 1, postLateUpdate); // postLateUpdate()
        // postLateUpdateSystem.subSystemList = postLateUpdateSubsystemList.ToArray();
        // playerLoop.subSystemList[sysIndex] = postLateUpdateSystem;
        
        // rendering
        sysIndex = Array.FindIndex(playerLoop.subSystemList, (s) => s.type.Name == "PostLateUpdate");
        PlayerLoopSystem postLateSystem = playerLoop.subSystemList[sysIndex];
        var postLateSubsystemList = new List<PlayerLoopSystem>(postLateSystem.subSystemList);
        index = postLateSubsystemList.FindIndex(h => h.type.Name.Contains("FinishFrameRendering"));
        postLateSubsystemList.Insert(index + 1, afterRendering); // rendering after
        postLateSystem.subSystemList = postLateSubsystemList.ToArray();
        playerLoop.subSystemList[sysIndex] = postLateSystem;
        
        
        PlayerLoop.SetPlayerLoop(playerLoop);
    }
    
    private static bool CheckRegist(ref PlayerLoopSystem playerLoop)
    {
        var t = typeof(PBDUpdateSystem);
        foreach (var subloop in playerLoop.subSystemList)
        {
            if (subloop.subSystemList != null && subloop.subSystemList.Any(x => x.type == t))
            {
                return true;
            }
        }
        return false;
    }
}