﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Timers;
using System.Xml.Serialization;
using ColossalFramework;
using ColossalFramework.IO;
using ICities;
using UnityEngine;

namespace TrafficManager_ImprovedAI
{
    public class SerializableDataExtension : ISerializableDataExtension
    {
        public static string legacyDataID = "TrafficManager_v0.9";
        public static string saveDataID = "TrafficManager-SaveData";
        public static UInt32 uniqueID;
        public const ushort CONTROL_BIT = 4096;

        public static ISerializableData SerializableData;

        public static bool configLoaded = false;

        private static byte[] saveData = null;
        private static Timer _timer;

        public void OnCreated(ISerializableData serializableData)
        {
            uniqueID = 0u;
            SerializableData = serializableData;
        }

        public void OnReleased()
        {
        }

        public static void GenerateUniqueID()
        {
            uniqueID = (uint)UnityEngine.Random.Range(1000000f, 2000000f);

            while (File.Exists(Path.Combine(Application.dataPath, "trafficManagerSave_" + uniqueID + ".xml"))) {
                uniqueID = (uint)UnityEngine.Random.Range(1000000f, 2000000f);
            }
        }

        public void OnLoadData()
        {
            configLoaded = false;
            byte[] data = SerializableData.LoadData(legacyDataID);
            saveData = SerializableData.LoadData(saveDataID);

            if ((data == null && saveData == null) || LoadingExtension.ignoreSavedData) {
                LoadingExtension.ignoreSavedData = false;
                GenerateUniqueID();
            } else {
                _timer = new System.Timers.Timer(2000);
                // Hook up the Elapsed event for the timer. 
                _timer.Elapsed += OnLoadDataTimed;
                _timer.Enabled = true;
                _timer.AutoReset = false;
            }
        }

        public static void OnLoadDataTimed(System.Object source, ElapsedEventArgs e)
        {
            _timer.Enabled = false;
            Configuration configuration;
            var i = 0;

            if (saveData != null) {
                configuration = Configuration.Deserialize(saveData);
            } else {
                byte[] data = SerializableData.LoadData(legacyDataID);

                uniqueID = 0u;

                for (i = 0; i < data.Length - 3; i++) {
                    uniqueID = BitConverter.ToUInt32(data, i);
                }

                var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + uniqueID + ".xml");

                if (!File.Exists(filepath)) {
                    Debug.Log("Traffic manager save file " + filepath + " not found!");
                    return;
                }
                configuration = Configuration.Deserialize(filepath);
            }

            try {
                for (i = 0; i < configuration.prioritySegments.Count; i++) {
                    if (!TrafficPriority.isPrioritySegment((ushort)configuration.prioritySegments[i][0],
                        configuration.prioritySegments[i][1])) {
                        TrafficPriority.addPrioritySegment((ushort)configuration.prioritySegments[i][0],
                            configuration.prioritySegments[i][1],
                            (PrioritySegment.PriorityType)configuration.prioritySegments[i][2]);
                    }
                }
            } catch (Exception ex) {
                Debug.Log("prio segments exception at " + i + " - " + ex);
            }

            try {
                for (i = 0; i < configuration.nodeDictionary.Count; i++) {
                    if (CustomRoadAI.GetNodeSimulation((ushort)configuration.nodeDictionary[i][0]) == null) {
                        CustomRoadAI.AddNodeToSimulation((ushort)configuration.nodeDictionary[i][0]);
                        var nodeDict = CustomRoadAI.GetNodeSimulation((ushort)configuration.nodeDictionary[i][0]);

                        nodeDict._manualTrafficLights = Convert.ToBoolean(configuration.nodeDictionary[i][1]);
                        nodeDict._timedTrafficLights = Convert.ToBoolean(configuration.nodeDictionary[i][2]);
                        nodeDict.TimedTrafficLightsActive = Convert.ToBoolean(configuration.nodeDictionary[i][3]);
                    }
                }
            } catch (Exception ex) {
                Debug.Log("nodes exception at " + i + " - " + ex);
            }

            try {
                for (i = 0; i < configuration.manualSegments.Count; i++) {
                    var segmentData = configuration.manualSegments[i];

                    if (!TrafficLightsManual.IsSegmentLight((ushort)segmentData[0], segmentData[1])) {
                        TrafficLightsManual.AddSegmentLight((ushort)segmentData[0], segmentData[1],
                            RoadBaseAI.TrafficLightState.Green);
                        var segment = TrafficLightsManual.GetSegmentLight((ushort)segmentData[0], segmentData[1]);
                        segment.currentMode = (ManualSegmentLight.Mode)segmentData[2];
                        segment.lightLeft = (RoadBaseAI.TrafficLightState)segmentData[3];
                        segment.lightMain = (RoadBaseAI.TrafficLightState)segmentData[4];
                        segment.lightRight = (RoadBaseAI.TrafficLightState)segmentData[5];
                        segment.lightPedestrian = (RoadBaseAI.TrafficLightState)segmentData[6];
                        segment.lastChange = (uint)segmentData[7];
                        segment.lastChangeFrame = (uint)segmentData[8];
                        segment.pedestrianEnabled = Convert.ToBoolean(segmentData[9]);
                    }
                }
            } catch (Exception ex) {
                Debug.Log("traf lights manual exception at " + i + " - " + ex);
            }

            var timedStepCount = 0;
            var timedStepSegmentCount = 0;

            try {
                for (i = 0; i < configuration.timedNodes.Count; i++) {
                    var nodeid = (ushort)configuration.timedNodes[i][0];

                    var nodeGroup = new List<ushort>();
                    for (var j = 0; j < configuration.timedNodeGroups[i].Length; j++) {
                        nodeGroup.Add(configuration.timedNodeGroups[i][j]);
                    }

                    if (!TrafficLightsTimed.IsTimedLight(nodeid)) {
                        TrafficLightsTimed.AddTimedLight(nodeid, nodeGroup);
                        var timedNode = TrafficLightsTimed.GetTimedLight(nodeid);

                        timedNode.currentStep = configuration.timedNodes[i][1];

                        for (var j = 0; j < configuration.timedNodes[i][2]; j++) {
                            var cfgstep = configuration.timedNodeSteps[timedStepCount];

                            timedNode.addStep(cfgstep[0]);

                            var step = timedNode.steps[j];

                            for (var k = 0; k < cfgstep[1]; k++) {
                                step.lightLeft[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][0];
                                step.lightMain[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][1];
                                step.lightRight[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][2];
                                step.lightPedestrian[k] = (RoadBaseAI.TrafficLightState)configuration.timedNodeStepSegments[timedStepSegmentCount][3];

                                timedStepSegmentCount++;
                            }

                            timedStepCount++;
                        }

                        if (Convert.ToBoolean(configuration.timedNodes[i][3])) {
                            timedNode.start();
                        }
                    }
                }
            } catch (Exception ex) {
                Debug.Log("timed traf lights exception at " + i + " - " + ex);
            }


            if (configuration.configVersion < 2.0f) {
                var j1 = 0;
                var i1 = 0;
                try {
                    for (i1 = 0; i1 < NetManager.MAX_NODE_COUNT; i1++) {
                        if (Singleton<NetManager>.instance.m_nodes.m_buffer[i1].Info.m_class.m_service ==
                            ItemClass.Service.Road && Singleton<NetManager>.instance.m_nodes.m_buffer[i1].m_flags != 0) {
                            var trafficLight = configuration.nodeTrafficLights[j1];
                
                            if (trafficLight == '1') {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i1].m_flags |= NetNode.Flags.TrafficLights;
                            } else {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i1].m_flags &= ~NetNode.Flags.TrafficLights;
                            }
                
                            j1++;
                        }
                    }
                } catch (Exception ex) {
                    Debug.Log("traf lights exception at i1 = " + i1 + " j1 = " + j1 + " - " + ex);
                }
                
                var j2 = 0;
                var i2 = 0;
                try {
                    for (i2 = 0; i2 < NetManager.MAX_NODE_COUNT; i2++) {
                        if (Singleton<NetManager>.instance.m_nodes.m_buffer[i2].Info.m_class.m_service ==
                            ItemClass.Service.Road && Singleton<NetManager>.instance.m_nodes.m_buffer[i2].m_flags != 0) {
                            var crossWalk = configuration.nodeCrosswalk[j2];
                
                            if (crossWalk == '1') {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i2].m_flags |= NetNode.Flags.Junction;
                            } else {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[i2].m_flags &= ~NetNode.Flags.Junction;
                            }
                
                            j2++;
                        }
                    }
                } catch (Exception ex) {
                    Debug.Log("crosswalk exception at i2 = " + i2 + " j2 = " + j2 + " - " + ex);
                }
            } else {
                var nodeIds = configuration.nodeIds;
                if (nodeIds != null && nodeIds.Length > 0) {
                    var ids = nodeIds.Split(',');
                    for (i = 0; i < ids.Length; i++) {
                        var id = Convert.ToUInt32(ids[i]);
                        var trafficLight = configuration.nodeTrafficLights[i];
                        var crossWalk = configuration.nodeCrosswalk[i];

                        if (Singleton<NetManager>.instance.m_nodes.m_buffer[id].Info.m_class.m_service ==
                            ItemClass.Service.Road && Singleton<NetManager>.instance.m_nodes.m_buffer[id].m_flags != 0) {

                            if (trafficLight == '1') {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[id].m_flags |= NetNode.Flags.TrafficLights;
                            } else {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[id].m_flags &= ~NetNode.Flags.TrafficLights;
                            }

                            if (crossWalk == '1') {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[id].m_flags |= NetNode.Flags.Junction;
                            } else {
                                Singleton<NetManager>.instance.m_nodes.m_buffer[id].m_flags &= ~NetNode.Flags.Junction;
                            }
                        }
                    }
                }
            }

            var lanes = configuration.laneFlags.TrimEnd(',').Split(',');
            try {
                for (i = 0; i < lanes.Length; i++) {
                    var split = lanes[i].Split(':');
                    uint laneId = Convert.ToUInt32(split[0]);
                    //NetLane lane = Singleton<NetManager>.instance.m_lanes.m_buffer [laneId];
                    //ushort segmentId = lane.m_segment;
                    //NetSegment segment = Singleton<NetManager>.instance.m_segments.m_buffer [segmentId];
                    //segment.Info.m_netAI.UpdateLanes(segmentId, ref segment, false);

                    Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags = Convert.ToUInt16(split[1]);
                    Singleton<NetManager>.instance.m_lanes.m_buffer[laneId].m_flags |= CONTROL_BIT;
                }
            } catch {
                // Empty config, ignore exception.
                //Debug.Log("exception reading lane flags at lane " + i + " lanes[] = [" + lanes[i] + "] - " + ex);
            }

            if (configuration.aiConfig != null && configuration.aiConfig.congestionCostFactor > 0) {
                Debug.Log("setting AI values from config");
                CustomPathFind.LoadAIParameters(configuration.aiConfig);
            } else {
                Debug.Log("AI parameters not found in config, using default values");
                CustomPathFind.ResetAIParameters();
            }

            CSL_Traffic.RoadManager.sm_lanes = new CSL_Traffic.RoadManager.Lane[NetManager.MAX_LANE_COUNT];
            int dupes = 0, zero = 0;
            if (configuration.laneMarkers == null || configuration.laneMarkers.Count == 0) {
                Debug.Log("no lane markers found");
            } else {
                Debug.Log("found " + configuration.laneMarkers.Count + " lane markers");
                foreach (var lane in configuration.laneMarkers.ToArray()) {
                    if (lane != null) {
                        if (CSL_Traffic.RoadManager.sm_lanes[lane.m_laneId] == null) {
                            if (lane.ConnectionCount() > 0) {
                                CSL_Traffic.RoadManager.sm_lanes[lane.m_laneId] = lane;
                            } else {
                                zero++;
                            }
                        } else {
                            dupes++;
                        }
                    } else {
                        Debug.Log("null lane marker!");
                    }
                }
                Debug.Log("loaded lane markers - " + dupes + " duplicates, " + zero + " unconnected");
            }

            configLoaded = true;
        }

        public void OnSaveData()
        {
            /*
            FastList<byte> data = new FastList<byte>();
//            Debug.Log("OnSaveData() 1");    
            GenerateUniqueID(); 

            byte[] uniqueIdBytes = BitConverter.GetBytes(uniqueID);
            foreach (byte uniqueIdByte in uniqueIdBytes) {
                data.Add(uniqueIdByte);
            }
//            Debug.Log("OnSaveData() 2");

            byte[] dataToSave = data.ToArray();
            SerializableData.SaveData(legacyDataID, dataToSave);
            
//            Debug.Log("OnSaveData() 3");
            var filepath = Path.Combine(Application.dataPath, "trafficManagerSave_" + uniqueID + ".xml");
//            Debug.Log("OnSaveData()");
            */

            var configuration = new Configuration();
            configuration.configVersion = 2.0f;

//            Debug.Log("OnSaveData() 4");

            configuration.laneFlags = "";
            configuration.nodeCrosswalk = "";
            configuration.nodeTrafficLights = "";

            for (var i = 0; i < NetManager.MAX_SEGMENT_COUNT; i++) {
                if (TrafficPriority.prioritySegments.ContainsKey(i)) {
                    if (TrafficPriority.prioritySegments[i].node_1 != 0) {
                        configuration.prioritySegments.Add(new int[3] {
                            TrafficPriority.prioritySegments[i].node_1,
                            i,
                            (int)TrafficPriority.prioritySegments[i].instance_1.type
                        });
                    } 
                    //Debug.Log("OnSaveData() 5");

                    if (TrafficPriority.prioritySegments[i].node_2 != 0) {
                        configuration.prioritySegments.Add(new int[3] {
                            TrafficPriority.prioritySegments[i].node_2,
                            i,
                            (int)TrafficPriority.prioritySegments[i].instance_2.type
                        });
                    }
                }
                //Debug.Log("OnSaveData() 6");

                if (CustomRoadAI.nodeDictionary.ContainsKey((ushort)i)) {
                    var nodeDict = CustomRoadAI.nodeDictionary[(ushort)i];

                    configuration.nodeDictionary.Add(new int[4] {
                        nodeDict.NodeId,
                        Convert.ToInt32(nodeDict._manualTrafficLights),
                        Convert.ToInt32(nodeDict._timedTrafficLights),
                        Convert.ToInt32(nodeDict.TimedTrafficLightsActive)
                    });
                }
//                Debug.Log("OnSaveData() 7");

                if (TrafficLightsManual.ManualSegments.ContainsKey(i)) {
                    if (TrafficLightsManual.ManualSegments[i].node_1 != 0) {
                        var manualSegment = TrafficLightsManual.ManualSegments[i].instance_1;

                        configuration.manualSegments.Add(new int[10] {
                            (int)manualSegment.node,
                            manualSegment.segment,
                            (int)manualSegment.currentMode,
                            (int)manualSegment.lightLeft,
                            (int)manualSegment.lightMain,
                            (int)manualSegment.lightRight,
                            (int)manualSegment.lightPedestrian,
                            (int)manualSegment.lastChange,
                            (int)manualSegment.lastChangeFrame,
                            Convert.ToInt32(manualSegment.pedestrianEnabled)
                        });
                    }
                    //Debug.Log("OnSaveData() 8");

                    if (TrafficLightsManual.ManualSegments[i].node_2 != 0) {
                        var manualSegment = TrafficLightsManual.ManualSegments[i].instance_2;
                        //Debug.Log("OnSaveData() 9");

                        configuration.manualSegments.Add(new int[10] {
                            (int)manualSegment.node,
                            manualSegment.segment,
                            (int)manualSegment.currentMode,
                            (int)manualSegment.lightLeft,
                            (int)manualSegment.lightMain,
                            (int)manualSegment.lightRight,
                            (int)manualSegment.lightPedestrian,
                            (int)manualSegment.lastChange,
                            (int)manualSegment.lastChangeFrame,
                            Convert.ToInt32(manualSegment.pedestrianEnabled)
                        });
                    }
                }
//                Debug.Log("OnSaveData() 10");

                if (TrafficLightsTimed.timedScripts.ContainsKey((ushort)i)) {
                    var timedNode = TrafficLightsTimed.GetTimedLight((ushort)i);

                    configuration.timedNodes.Add(new int[4] {
                        timedNode.nodeID,
                        timedNode.currentStep,
                        timedNode.NumSteps(),
                        Convert.ToInt32(timedNode.isStarted())
                    });

                    var nodeGroup = new ushort[timedNode.nodeGroup.Count];

                    for (var j = 0; j < timedNode.nodeGroup.Count; j++) {
                        nodeGroup[j] = timedNode.nodeGroup[j];
                    }

                    configuration.timedNodeGroups.Add(nodeGroup);

                    for (var j = 0; j < timedNode.NumSteps(); j++) {
                        configuration.timedNodeSteps.Add(new int[2] {
                            timedNode.steps[j].numSteps,
                            timedNode.steps[j].segments.Count
                        });

                        for (var k = 0; k < timedNode.steps[j].segments.Count; k++) {
                            configuration.timedNodeStepSegments.Add(new int[4] {
                                (int)timedNode.steps[j].lightLeft[k],
                                (int)timedNode.steps[j].lightMain[k],
                                (int)timedNode.steps[j].lightRight[k],
                                (int)timedNode.steps[j].lightPedestrian[k],
                            });
                        }
                    }
                }
            }
            //Debug.Log("OnSaveData() 11");

            for (var i = 0; i < Singleton<NetManager>.instance.m_nodes.m_buffer.Length; i++) {
                var nodeFlags = Singleton<NetManager>.instance.m_nodes.m_buffer[i].m_flags;

                if (nodeFlags != 0) {
                    if (Singleton<NetManager>.instance.m_nodes.m_buffer[i].Info.m_class.m_service ==
                        ItemClass.Service.Road) {
                        configuration.nodeIds += i + ",";
                        configuration.nodeTrafficLights +=
                            Convert.ToInt16((nodeFlags & NetNode.Flags.TrafficLights) != NetNode.Flags.None);
                        configuration.nodeCrosswalk +=
                            Convert.ToInt16((nodeFlags & NetNode.Flags.Junction) != NetNode.Flags.None);
                    }
                }
            }
            if (configuration.nodeIds != null && configuration.nodeIds.Length > 0) {
                configuration.nodeIds = configuration.nodeIds.TrimEnd(',');
            }
            //Debug.Log("OnSaveData() 12");

            var laneCount = 0;
            for (var i = 0; i < Singleton<NetManager>.instance.m_lanes.m_buffer.Length; i++) {
                var laneSegment = Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_segment;

                if (TrafficPriority.prioritySegments.ContainsKey(laneSegment)) {
                    configuration.laneFlags += i + ":" + Singleton<NetManager>.instance.m_lanes.m_buffer[i].m_flags + ",";
                    laneCount++;
                }
            }

            if (configuration.laneFlags != null && configuration.laneFlags.Length > 0) {
                configuration.laneFlags = configuration.laneFlags.TrimEnd(',');
            }

            configuration.aiConfig.congestionCostFactor = CustomPathFind.congestionCostFactor;
            configuration.aiConfig.minLaneSpace = CustomPathFind.minLaneSpace;
            configuration.aiConfig.lookaheadLanes = CustomPathFind.lookaheadLanes;
            configuration.aiConfig.congestedLaneThreshold = CustomPathFind.congestedLaneThreshold;
//            configuration.aiConfig.obeyTMLaneFlags = CustomPathFind.obeyTMLaneFlags;

            for (var i = 0; i < CSL_Traffic.RoadManager.sm_lanes.Length; i++) {
                var lane = CSL_Traffic.RoadManager.sm_lanes[i];
                if (lane != null && lane.ConnectionCount() > 0) {
                    configuration.laneMarkers.Add(lane);
                }
            }

            //Configuration.Serialize(filepath, configuration);
            Configuration.Serialize(SerializableData, saveDataID, configuration);

            /*
            GenerateUniqueID();
            Configuration.Serialize(Path.Combine(Application.dataPath, "trafficManagerSave_" + uniqueID + ".xml"), configuration);
            */
        }
    }

    public class Configuration
    {
        [Serializable]
        public class ImprovedAIConfig
        {
            public float congestionCostFactor;
            public float minLaneSpace;
            public int lookaheadLanes;
            public int congestedLaneThreshold;
//            public bool obeyTMLaneFlags;
        }

        public float configVersion = 0.0f;

        public string nodeTrafficLights;
        public string nodeCrosswalk;
        public string nodeIds;
        public string laneFlags;

        public List<int[]> prioritySegments = new List<int[]>();
        public List<int[]> nodeDictionary = new List<int[]>();
        public List<int[]> manualSegments = new List<int[]>();
        public List<int[]> timedNodes = new List<int[]>();
        public List<ushort[]> timedNodeGroups = new List<ushort[]>();
        public List<int[]> timedNodeSteps = new List<int[]>();
        public List<int[]> timedNodeStepSegments = new List<int[]>();

        public ImprovedAIConfig aiConfig = new ImprovedAIConfig();
        public List<CSL_Traffic.RoadManager.Lane> laneMarkers = new List<CSL_Traffic.RoadManager.Lane>();

        public string[] md5Sums = new string[12];

        public override string ToString()
        {
            var s = "\n";
            try {
                s += "TM + AI configuration version: " + configVersion + "\n\n";
                s += "Traffic Manager\n";
                s += "---------------\n";
                s += "priority segments: " + prioritySegments.Count + "\n";
                s += "nodes: " + nodeDictionary.Count + "\n";
                s += "manual segments: " + manualSegments.Count + "\n";
                s += "timed nodes: " + timedNodes.Count + "\n";
                s += "timed node groups: " + timedNodeGroups.Count + "\n";
                s += "timed node steps: " + timedNodeSteps.Count + "\n";
                s += "timed node step segments: " + timedNodeStepSegments.Count + "\n";
                s += "traffic lights: " + nodeTrafficLights.Length + "\n";
                s += "crosswalks: " + nodeCrosswalk.Length + "\n";
                s += "lane flags: " + laneFlags.TrimEnd(',').Split(',').Length + "\n";
                s += "lane markers (T++): " + laneMarkers.Count + "\n\n";

                s += "Improved AI\n";
                s += "-----------\n";
                s += "minimum lane space = " + aiConfig.minLaneSpace + "\n";
                s += "congestion cost factor = " + aiConfig.congestionCostFactor + "\n";
                s += "lookahead lanes = " + aiConfig.lookaheadLanes + "\n";
                s += "congested lane threshold = " + aiConfig.congestedLaneThreshold;
                //s += "obey traffic manager lane flags = " + aiConfig.obeyTMLaneFlags;
            } catch (Exception e) {
                Debug.Log("error constructing string representation of configuration data, probable corruption! - " + e);
            }
            return s;
        }

        private void ComputeHashCodes()
        {
            md5Sums[0] = MD5HashGenerator.GenerateKey(nodeTrafficLights);
            md5Sums[1] = MD5HashGenerator.GenerateKey(nodeCrosswalk);
            md5Sums[2] = MD5HashGenerator.GenerateKey(laneFlags);
            md5Sums[3] = MD5HashGenerator.GenerateKey(prioritySegments);
            md5Sums[4] = MD5HashGenerator.GenerateKey(nodeDictionary);
            md5Sums[5] = MD5HashGenerator.GenerateKey(manualSegments);
            md5Sums[6] = MD5HashGenerator.GenerateKey(timedNodes);
            md5Sums[7] = MD5HashGenerator.GenerateKey(timedNodeGroups);
            md5Sums[8] = MD5HashGenerator.GenerateKey(timedNodeSteps);
            md5Sums[9] = MD5HashGenerator.GenerateKey(timedNodeStepSegments);
            md5Sums[10] = MD5HashGenerator.GenerateKey(aiConfig);
            md5Sums[11] = MD5HashGenerator.GenerateKey(laneMarkers);
        }

        private bool CheckHashCodes()
        {
            try {
                var i = 0;
                return (
                    (md5Sums[0] == MD5HashGenerator.GenerateKey(nodeTrafficLights) || LogHashcodeMismatch(0, i++)) &&
                    (md5Sums[1] == MD5HashGenerator.GenerateKey(nodeCrosswalk) || LogHashcodeMismatch(1, i++)) &&
                    (md5Sums[2] == MD5HashGenerator.GenerateKey(laneFlags) || LogHashcodeMismatch(2, i++)) &&
                    (md5Sums[3] == MD5HashGenerator.GenerateKey(prioritySegments) || LogHashcodeMismatch(3, i++)) &&
                    (md5Sums[4] == MD5HashGenerator.GenerateKey(nodeDictionary) || LogHashcodeMismatch(4, i++)) &&
                    (md5Sums[5] == MD5HashGenerator.GenerateKey(manualSegments) || LogHashcodeMismatch(5, i++)) &&
                    (md5Sums[6] == MD5HashGenerator.GenerateKey(timedNodes) || LogHashcodeMismatch(6, i++)) &&
                    (md5Sums[7] == MD5HashGenerator.GenerateKey(timedNodeGroups) || LogHashcodeMismatch(7, i++)) &&
                    (md5Sums[8] == MD5HashGenerator.GenerateKey(timedNodeSteps) || LogHashcodeMismatch(8, i++)) &&
                    (md5Sums[9] == MD5HashGenerator.GenerateKey(timedNodeStepSegments) || LogHashcodeMismatch(9, i++)) &&
                    (md5Sums[10] == MD5HashGenerator.GenerateKey(aiConfig) || LogHashcodeMismatch(10, i++)) &&
                    (md5Sums[11] == MD5HashGenerator.GenerateKey(laneMarkers) || LogHashcodeMismatch(11, i++)) &&
                    i == 0);
            } catch(Exception e) {
                Debug.Log("missing or invalid hash code data triggered exception: " + e);
                return false;
            }
        }

        private bool LogHashcodeMismatch(int i, int dummy)
        {
            Debug.Log("md5sum[" + i + "] mismatch");
            return true;
        }

        public void OnPreSerialize()
        {
        }

        public void OnPostDeserialize()
        {
        }

        private static void UnknownAttribute(object sender, XmlAttributeEventArgs e)
        {
            Debug.Log("unknown attribute " + sender.ToString() + " " + e.ToString());
        }

        private static void UnknownElement(object sender, XmlElementEventArgs e)
        {
            Debug.Log("unknown element " + sender.ToString() + " " + e.ToString());
        }

        private static void UnknownNode(object sender, XmlNodeEventArgs e)
        {
            Debug.Log("unknown node " + sender.ToString() + " " + e.ToString());
        }

        private static void UnreferencedObject(object sender, UnreferencedObjectEventArgs e)
        {
            Debug.Log("unreferenced object " + sender.ToString() + " " + e.ToString());
        }

        private static void RegisterEvents(ref XmlSerializer s)
        {
            s.UnknownAttribute += new XmlAttributeEventHandler(UnknownAttribute);
            s.UnknownElement += new XmlElementEventHandler(UnknownElement);
            s.UnknownNode += new XmlNodeEventHandler(UnknownNode);
            s.UnreferencedObject += new UnreferencedObjectEventHandler(UnreferencedObject);
        }

        public static void Serialize(string filename, Configuration config)
        {
            config.ComputeHashCodes();
            Debug.Log("serializing to " + filename);
            Debug.Log(config.ToString());

            var serializer = new XmlSerializer(typeof(Configuration));
            RegisterEvents(ref serializer);    

            using (var writer = new StreamWriter(filename)) {
                config.OnPreSerialize();
                serializer.Serialize(writer, config);
            }
        }

        public static void Serialize(ISerializableData serializableData, string dataID, Configuration config)
        {
            config.ComputeHashCodes();
            Debug.Log("serializing to save data");
            Debug.Log(config.ToString());

            var serializer = new XmlSerializer(typeof(Configuration));
            XmlSerializerNamespaces ns = new XmlSerializerNamespaces();
            ns.Add("", "");
            RegisterEvents(ref serializer);    
            byte[] configData;

            using (var memoryStream = new MemoryStream()) {
                config.OnPreSerialize();
                serializer.Serialize(memoryStream, config, ns);
                configData = memoryStream.ToArray();
            }

            serializableData.SaveData(dataID, configData);
        }

        public static Configuration Deserialize(byte[] saveData)
        {
            Debug.Log("deserializing from save data");

            var serializer = new XmlSerializer(typeof(Configuration));
            RegisterEvents(ref serializer);    

            try {
                using (var memoryStream = new MemoryStream(saveData)) {
                    var config = (Configuration)serializer.Deserialize(memoryStream);
                    config.OnPostDeserialize();
                    Debug.Log(config.ToString());
                    if (config.CheckHashCodes()) {
                        Debug.Log("configuration hash codes verified!");
                    } else {
                        Debug.LogWarning("configuration hash code mismatch, probable data corruption! (ignore if loading data from previous version of this mod)");
                    }
                    return config;
                }
            } catch (Exception e) {
                Debug.Log("deserialize exception " + e);
                return null;
            }
        }

        public static Configuration Deserialize(string filename)
        {
            Debug.Log("deserializing from " + filename);

            var serializer = new XmlSerializer(typeof(Configuration));
            RegisterEvents(ref serializer);    

            try {
                using (var reader = new StreamReader(filename)) {
                    var config = (Configuration)serializer.Deserialize(reader);
                    config.OnPostDeserialize();
                    Debug.Log(config.ToString());
                    if (config.CheckHashCodes()) {
                        Debug.Log("configuration hash codes verified!");
                    } else {
                        Debug.LogWarning("configuration hash code mismatch, probable data corruption! (ignore if loading data from previous version of this mod)");
                    }
                    return config;
                }
            } catch (Exception e) {
                Debug.Log("deserialize exception " + e);
                return null;
            }
        }
    }
}
