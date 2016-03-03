﻿using System;
using System.Collections.Generic;
using System.ComponentModel.Design;
using ColossalFramework;
using ColossalFramework.UI;
using UnityEngine;

namespace TrafficManager_ImprovedAI
{
    public class UITrafficManager : UIPanel
    {
        public enum UIState
        {
            None,
            SwitchTrafficLight,
            AddStopSign,
            ManualSwitch,
            TimedControlNodes,
            TimedControlLights,
            LaneChange,
            LaneChangeAlt,
            LaneRestrictions,
            Crosswalk
        }

        private static UIState _uistate = UIState.None;

        private static bool inited = false;

        public static UIState uistate {
            set {
                if (value == UIState.None && inited) {
                    buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";
                    buttonPrioritySigns.focusedBgSprite = "ButtonMenu";
                    buttonManualControl.focusedBgSprite = "ButtonMenu";
                    buttonTimedMain.focusedBgSprite = "ButtonMenu";
                    buttonCrosswalk.focusedBgSprite = "ButtonMenu";
                    buttonClearTraffic.focusedBgSprite = "ButtonMenu";
                    buttonLaneChange.focusedBgSprite = "ButtonMenu";
                    buttonToggleDespawn.focusedBgSprite = "ButtonMenu";
                }

                _uistate = value;
            }
            get { return _uistate; }
        }

        private static UIButton buttonSwitchTraffic;
        private static UIButton buttonPrioritySigns;
        private static UIButton buttonManualControl;
        private static UIButton buttonTimedMain;
        private static UIButton buttonLaneChange;
        private static UIButton buttonCrosswalk;
        private static UIButton buttonClearTraffic;
        private static UIButton buttonToggleDespawn;

        public static TrafficLightTool trafficLightTool;

        public override void Start()
        {
            inited = true;

            trafficLightTool = LoadingExtension.Instance.TrafficLightTool;

            this.backgroundSprite = "GenericPanel";
            this.color = new Color32(75, 75, 135, 255);
            this.width = 250;
            this.height = 350;
            this.relativePosition = new Vector3(10.48f, 80f);

            UILabel title = this.AddUIComponent<UILabel>();
            title.text = "Traffic Manager";
            title.relativePosition = new Vector3(65.0f, 5.0f);

            buttonSwitchTraffic = _createButton("信号の付け外し", new Vector3(35f, 30f), clickSwitchTraffic);
            buttonPrioritySigns = _createButton("優先関係標識の追加", new Vector3(35f, 70f), clickAddPrioritySigns);
            buttonManualControl = _createButton("信号の手動切替", new Vector3(35f, 110f), clickManualControl);
            buttonTimedMain = _createButton("時間設定付き信号", new Vector3(35f, 150f), clickTimedAdd);
            buttonLaneChange = _createButton("車線矢印の変更", new Vector3(35f, 190f), clickChangeLanes);
            buttonCrosswalk = _createButton("横断歩道の付け外し", new Vector3(35f, 230f), clickCrosswalk);
            buttonClearTraffic = _createButton("移動中車両の除去", new Vector3(35f, 270f), clickClearTraffic);
            buttonToggleDespawn = _createButton(LoadingExtension.Instance.despawnEnabled ? "スタック除去無効化" : "スタック除去有効化", new Vector3(35f, 310f), clickToggleDespawn);
        }

        private UIButton _createButton(string text, Vector3 pos, MouseEventHandler eventClick)
        {
            var button = this.AddUIComponent<UIButton>();
            button.width = 190;
            button.height = 30;
            button.normalBgSprite = "ButtonMenu";
            button.disabledBgSprite = "ButtonMenuDisabled";
            button.hoveredBgSprite = "ButtonMenuHovered";
            button.focusedBgSprite = "ButtonMenu";
            button.pressedBgSprite = "ButtonMenuPressed";
            button.textColor = new Color32(255, 255, 255, 255);
            button.playAudioEvents = true;
            button.text = text;
            button.relativePosition = pos;
            button.eventClick += eventClick;

            return button;
        }

        private void clickSwitchTraffic(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.SwitchTrafficLight) {
                _uistate = UIState.SwitchTrafficLight;

                buttonSwitchTraffic.focusedBgSprite = "ButtonMenuFocused";
                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.SwitchTrafficLight);
            } else {
                _uistate = UIState.None;

                buttonSwitchTraffic.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickAddPrioritySigns(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.AddStopSign) {
                _uistate = UIState.AddStopSign;

                buttonPrioritySigns.focusedBgSprite = "ButtonMenuFocused";
                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.AddPrioritySigns);
            } else {
                _uistate = UIState.None;

                buttonPrioritySigns.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickManualControl(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.ManualSwitch) {
                _uistate = UIState.ManualSwitch;

                buttonManualControl.focusedBgSprite = "ButtonMenuFocused";
                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.ManualSwitch);
            } else {
                _uistate = UIState.None;

                buttonManualControl.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickTimedAdd(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.TimedControlNodes) {
                _uistate = UIState.TimedControlNodes;

                buttonTimedMain.focusedBgSprite = "ButtonMenuFocused";
                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.TimedLightsSelectNode);
            } else {
                _uistate = UIState.None;

                buttonTimedMain.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private void clickClearTraffic(UIComponent component, UIMouseEventParameter eventParam)
        {
            try {
                List<ushort> vehicleList = new List<ushort>();

                foreach (var vehicleID in TrafficPriority.vehicleList.Keys) {
                    vehicleList.Add(vehicleID);
                }

                lock (Singleton<VehicleManager>.instance) {
                    for (var i = 0; i < vehicleList.Count; i++) {
                        var vehicleData = Singleton<VehicleManager>.instance.m_vehicles.m_buffer[vehicleList[i]];

                        if (vehicleData.Info.m_vehicleType == VehicleInfo.VehicleType.Car) {
                            Singleton<VehicleManager>.instance.ReleaseVehicle(vehicleList[i]);
                        }
                    }
                }
            } catch (Exception e) {
                Debug.LogWarning("Exception while clearing traffic: " + e);
            }
        }

        private void clickChangeLanes(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)) {
                if (_uistate != UIState.LaneChangeAlt) {
                    _uistate = UIState.LaneChangeAlt;
                    buttonLaneChange.focusedBgSprite = "ButtonMenuFocused";
                    //LoadingExtension.Instance.SetToolMode(TrafficManagerMode.TrafficLight);
                    TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
                    ToolsModifierControl.toolController.CurrentTool = LoadingExtension.Instance.RoadCustomizerTool;
                    ToolsModifierControl.SetTool<CSL_Traffic.RoadCustomizerTool>();
                } else {
                    _uistate = UIState.None;
                    buttonLaneChange.focusedBgSprite = "ButtonMenu";
                    TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
                }
            } else {
                if (_uistate != UIState.LaneChange) {
                    _uistate = UIState.LaneChange;
                    buttonLaneChange.focusedBgSprite = "ButtonMenuFocused";
                    TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.LaneChange);
                } else {
                    _uistate = UIState.None;
                    buttonLaneChange.focusedBgSprite = "ButtonMenu";
                    TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
                }
            }
        }

        private void clickCrosswalk(UIComponent component, UIMouseEventParameter eventParam)
        {
            if (_uistate != UIState.Crosswalk) {
                _uistate = UIState.Crosswalk;

                buttonCrosswalk.focusedBgSprite = "ButtonMenuFocused";
                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.Crosswalk);
            } else {
                _uistate = UIState.None;

                buttonCrosswalk.focusedBgSprite = "ButtonMenu";

                TrafficLightTool.setToolMode(TrafficLightTool.ToolMode.None);
            }
        }

        private static void clickToggleDespawn(UIComponent component, UIMouseEventParameter eventParam)
        {
            LoadingExtension.Instance.despawnEnabled = !LoadingExtension.Instance.despawnEnabled;
            buttonToggleDespawn.text = LoadingExtension.Instance.despawnEnabled ? "スタック除去無効化" : "スタック除去有効化";
        }
    }
}
