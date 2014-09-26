﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using System.IO;
using System.Xml.Serialization;

using UnityEngine;

namespace KSPAdvancedFlyByWire
{

    public class ControllerConfiguration
    {
        public InputWrapper wrapper = InputWrapper.XInput;
        public int controllerIndex = 0;
        public List<ControllerPreset> presets = new List<ControllerPreset>();
        public int currentPreset = 0;
        public CurveType analogInputCurve = CurveType.XSquared;
        public float discreteActionStep = 0.15f;
        public float incrementalThrottleSensitivity = 0.05f;

        public List<float> axisPositiveDeadZones = null;
        public List<float> axisNegativeDeadZones = null;

        public List<float> axisLeft = null;
        public List<float> axisIdentity = null;
        public List<float> axisRight = null;

        [XmlIgnore()]
        public IController iface;

        [XmlIgnore()]
        public HashSet<Bitset> evaluatedDiscreteActionMasks = new HashSet<Bitset>();

        public ControllerPreset GetCurrentPreset()
        {
            if (currentPreset >= presets.Count)
            {
                currentPreset = 0;
                if (presets.Count == 0)
                {
                    presets.Add(new ControllerPreset());
                }
            }

            var preset = presets[currentPreset];

            if(preset == null)
            {
                MonoBehaviour.print("KSPAdvancedFlyByWire: null preset error");
            }

            return preset;
        }

        public void SetAnalogInputCurveType(CurveType type)
        {
            analogInputCurve = type;
            if (iface != null)
            {
                iface.analogEvaluationCurve = CurveFactory.Instantiate(type);
            }
        }
    }

    public class Configuration
    {

        public List<ControllerConfiguration> controllers = new List<ControllerConfiguration>();

        public Configuration() {}

        public void ActivateController(InputWrapper wrapper, int controllerIndex, IController.ButtonPressedCallback pressedCallback, IController.ButtonReleasedCallback releasedCallback)
        {
            foreach (var config in controllers)
            {
                if (config.wrapper == wrapper && config.controllerIndex == controllerIndex)
                {
                    return;
                }
            }

            ControllerConfiguration controller = new ControllerConfiguration();

            controller.wrapper = wrapper;
            controller.controllerIndex = controllerIndex;

            if (wrapper == InputWrapper.XInput)
            {
                controller.iface = new XInputController(controller.controllerIndex);
            }
            else if (wrapper == InputWrapper.SDL)
            {
                controller.iface = new SDLController(controller.controllerIndex);
            }
            else if (wrapper == InputWrapper.KeyboardMouse)
            {
                controller.iface = new KeyboardMouseController();
            }

            controller.iface.analogEvaluationCurve = CurveFactory.Instantiate(controller.analogInputCurve);
            controller.iface.buttonPressedCallback = new IController.ButtonPressedCallback(pressedCallback);
            controller.iface.buttonReleasedCallback = new IController.ButtonReleasedCallback(releasedCallback);

            controller.presets = DefaultControllerPresets.GetDefaultPresets(controller.iface);
            controller.currentPreset = 0;

            controllers.Add(controller);

            ScreenMessages.PostScreenMessage("CONTROLLER: " + controller.iface.GetControllerName(), 1.0f, ScreenMessageStyle.UPPER_CENTER);
        }

        public void DeactivateController(InputWrapper wrapper, int controllerIndex)
        {
            for (int i = 0; i < controllers.Count; i++)
            {
                var config = controllers[i];

                if (config.wrapper == wrapper && config.controllerIndex == controllerIndex)
                {
                    controllers[i].iface = null;
                    controllers.RemoveAt(i);
                    return;
                }
            }
        }

        public void OnPreSerialize()
        {
            foreach (ControllerConfiguration config in controllers)
            {
                foreach (var preset in config.presets)
                {
                    preset.OnPreSerialize();
                }

                config.axisPositiveDeadZones = new List<float>();
                config.axisNegativeDeadZones = new List<float>();

                config.axisLeft = new List<float>();
                config.axisIdentity = new List<float>();
                config.axisRight = new List<float>();

                for (int i = 0; i < config.iface.GetAxesCount(); i++)
                {
                    config.axisPositiveDeadZones.Add(config.iface.axisPositiveDeadZones[i]);
                    config.axisNegativeDeadZones.Add(config.iface.axisNegativeDeadZones[i]);
                    config.axisLeft.Add(config.iface.axisLeft[i]);
                    config.axisIdentity.Add(config.iface.axisIdentity[i]);
                    config.axisRight.Add(config.iface.axisRight[i]);
                }
            }
        }

        public void OnPostDeserialize()
        {
            foreach (ControllerConfiguration config in controllers)
            {
                if (config.wrapper == InputWrapper.XInput)
                {
                    config.iface = new XInputController(config.controllerIndex);
                }
                else if (config.wrapper == InputWrapper.SDL)
                {
                    config.iface = new SDLController(config.controllerIndex);
                }
                else if (config.wrapper == InputWrapper.KeyboardMouse)
                {
                    config.iface = new KeyboardMouseController();
                }

                config.evaluatedDiscreteActionMasks = new HashSet<Bitset>();

                foreach (var preset in config.presets)
                {
                    preset.OnPostDeserialize();
                }

                for (int i = 0; i < config.iface.GetAxesCount(); i++)
                {
                    config.iface.axisPositiveDeadZones[i] = config.iface.axisPositiveDeadZones[i];
                    config.iface.axisNegativeDeadZones[i] = config.iface.axisNegativeDeadZones[i];
                    config.iface.axisLeft[i] = config.iface.axisLeft[i];
                    config.iface.axisIdentity[i] = config.iface.axisIdentity[i];
                    config.iface.axisRight[i] = config.iface.axisRight[i];
                }

                config.axisPositiveDeadZones = null;
                config.axisNegativeDeadZones = null;

                config.axisLeft = null;
                config.axisIdentity = null;
                config.axisRight = null;
            }
        }

        public ControllerConfiguration GetConfigurationByIController(IController controller)
        {
            foreach (ControllerConfiguration config in controllers)
            {
                if(config.iface == controller)
                {
                    return config;
                }
            }

            return null;
        }

        public ControllerConfiguration GetConfigurationByControllerType(InputWrapper wrapper, int controllerIndex)
        {
            foreach (ControllerConfiguration config in controllers)
            {
                if (config.wrapper == wrapper && config.controllerIndex == controllerIndex)
                {
                    return config;
                }
            }

            return null;
        }

        public static void Serialize(string filename, Configuration config)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            using (var writer = new StreamWriter(filename))
            {
                config.OnPreSerialize();
                serializer.Serialize(writer, config);
            }
        }

        public static Configuration Deserialize(string filename)
        {
            var serializer = new XmlSerializer(typeof(Configuration));

            try
            {
                using (var reader = new StreamReader(filename))
                {
                    Configuration config = (Configuration)serializer.Deserialize(reader);
                    config.OnPostDeserialize();
                    return config;
                }
            } catch {}

            return null;
        }
    }

}