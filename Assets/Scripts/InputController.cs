/**
 * Copyright (c) 2018 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */


﻿using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class InputController : MonoBehaviour, Ros.IRosClient
{
    static readonly string WHEEL_CMD_TOPIC = "/simulator/wheels_driver_node/wheels_cmd";
    static readonly string JOYSTICK_OVERRIDE_TOPIC_ROS1 = "/simulator/joy_mapper_node/joystick_override";
    static readonly string JOYSTICK_OVERRIDE_TOPIC_ROS2 = "/joystick_override";
    static readonly string JOYSTICK_ROS1 = "/simulator/joy";
    static readonly string AUTOWARE_CMD_TOPIC = "/vehicle_cmd";

    public enum ControlMethod
    {
        UnityKeyboard,
        ROS,
    }

    public ControlMethod controlMethod;

    public Camera MainCamera;
    public Camera centerCam;
    public List<Camera> sideCams;

    public float vertical;

    public float horizontal;

    public float UnityVerticalSensitivity = 100f;
    public float UnityHorizontalSensitivity = 100f;

    public float ROSVerticalSensitivity = 3f;
    public float ROSHorizontalSensitivity = 1.5f;

    public float wheelLeftVel;
    public float wheelRightVel;

    bool ManualControl = true;

    bool FirstConnection = true;

    Ros.Bridge Bridge;

#pragma warning disable 0649
    [Ros.MessageType("duckietown_msgs/BoolStamped")]
    struct BoolStamped
    {
        public Ros.Header header;
        public bool data;
    }

    [Ros.MessageType("duckietown_msgs/WheelsCmdStamped")]
    struct WheelsCmdStampedMsg
    {
        public Ros.Header header;
        public float vel_left;
        public float vel_right;
    }
#pragma warning  restore 0649

    void Start()
    {
        foreach (var sc in sideCams)
        {
            sc.enabled = true;
        }
    }

    public void OnRosBridgeAvailable(Ros.Bridge bridge)
    {
        Bridge = bridge;
        Bridge.AddPublisher(this);
    }

    public void OnRosConnected()
    {
        Bridge.Subscribe(WHEEL_CMD_TOPIC,
            (WheelsCmdStampedMsg msg) =>
            {
                wheelLeftVel = msg.vel_left;
                wheelRightVel = msg.vel_right;
            });

        // Autoware vehicle command
        Bridge.Subscribe(AUTOWARE_CMD_TOPIC,
            (Ros.VehicleCmd msg) =>
            {
                float WHEEL_SEPARATION = 0.1044197f;
                //float WHEEL_DIAMETER = 0.065f;
                float L_GAIN = 0.25f;
                float A_GAIN = 8.0f;
                // Assuming that we only get linear in x and angular in z
                double v = msg.twist_cmd.twist.linear.x;
                double w = msg.twist_cmd.twist.angular.z;

                wheelLeftVel = (float)(L_GAIN * v - A_GAIN * w * 0.5 * WHEEL_SEPARATION);
                wheelRightVel = (float)(L_GAIN * v + A_GAIN * w * 0.5 * WHEEL_SEPARATION);
            });

        string override_topic;
        if (Bridge.Version == 1)
        {
            Bridge.AddPublisher<Ros.Joy>(JOYSTICK_ROS1);
            override_topic = JOYSTICK_OVERRIDE_TOPIC_ROS1;
        }
        else
        {
            override_topic = JOYSTICK_OVERRIDE_TOPIC_ROS2;
        }
        Bridge.AddPublisher<BoolStamped>(override_topic);
        Bridge.Subscribe<BoolStamped>(override_topic, stamped =>
        {
            ManualControl = stamped.data;
        });

        if (FirstConnection)
        {
            var stamp = new BoolStamped()
            {
                header = new Ros.Header()
                {
                    stamp = Ros.Time.Now(),
                    seq = seq++,
                    frame_id = "joystick",
                },
                data = true,
            };

            var topic = (Bridge.Version == 1) ? JOYSTICK_OVERRIDE_TOPIC_ROS1 : JOYSTICK_OVERRIDE_TOPIC_ROS2;
            Bridge.Publish(topic, stamp);

            FirstConnection = false;
        }

        ManualControl = true;
    }

    uint seq = 0;

    void Update()
    {
        bool allowRobotControl = EventSystem.current.currentSelectedGameObject == null;

        if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.DownArrow) ||
            Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.RightArrow))
        {
            if (MainCamera.gameObject.activeSelf && allowRobotControl)
            {
                controlMethod = ControlMethod.UnityKeyboard;

                if (Input.GetKey(KeyCode.UpArrow))
                {
                    vertical += Time.deltaTime * UnityVerticalSensitivity;
                }
                else if (Input.GetKey(KeyCode.DownArrow))
                {
                    vertical -= Time.deltaTime * UnityVerticalSensitivity;
                }
                else
                {
                    if (vertical > 0f)
                    {
                        vertical -= Time.deltaTime * UnityVerticalSensitivity;
                        if (vertical < 0f)
                        {
                            vertical = 0f;
                        }
                    }
                    else if (vertical < 0f)
                    {
                        vertical += Time.deltaTime * UnityVerticalSensitivity;
                        if (vertical > 0f)
                        {
                            vertical = 0f;
                        }
                    }
                }

                if (Input.GetKey(KeyCode.RightArrow))
                {
                    horizontal += Time.deltaTime * UnityHorizontalSensitivity;
                }
                else if (Input.GetKey(KeyCode.LeftArrow))
                {
                    horizontal -= Time.deltaTime * UnityHorizontalSensitivity;
                }
                else
                {
                    if (horizontal > 0f)
                    {
                        horizontal -= Time.deltaTime * UnityHorizontalSensitivity;
                        if (horizontal < 0f)
                        {
                            horizontal = 0f;
                        }
                    }
                    else if (horizontal < 0f)
                    {
                        horizontal += Time.deltaTime * UnityHorizontalSensitivity;
                        if (vertical > 0f)
                        {
                            horizontal = 0f;
                        }
                    }
                }
            }
        }
        else
        {
            controlMethod = ControlMethod.ROS;
        }

        if (MainCamera.gameObject.activeSelf && allowRobotControl)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1))
            {
                ManualControl = !ManualControl;
                if (ManualControl)
                {
                    wheelLeftVel = wheelRightVel = 0.0f;
                }
                var stamp = new BoolStamped()
                {
                    header = new Ros.Header()
                    {
                        stamp = Ros.Time.Now(),
                        seq = seq++,
                        frame_id = "joystick",
                    },
                    data = ManualControl,
                };

                if (Bridge.Version == 1)
                {
                    var joy = new Ros.Joy()
                    {
                        header = new Ros.Header()
                        {
                            stamp = Ros.Time.Now(),
                            seq = seq++,
                            frame_id = "joystick",
                        },
                        axes = new float[6],
                        buttons = new int[15],
                    };
                    joy.buttons[5] = 1;
                    Bridge.Publish(JOYSTICK_ROS1, joy);
                    Bridge.Publish(JOYSTICK_OVERRIDE_TOPIC_ROS1, stamp);
                }
                else
                {
                    Bridge.Publish(JOYSTICK_OVERRIDE_TOPIC_ROS2, stamp);
                }
            }
        }

        if (Bridge != null && Bridge.Status != Ros.Status.Connected)
        {
            wheelLeftVel = wheelRightVel = 0.0f;
        }

        vertical = Mathf.Clamp(vertical, -1f, 1f);
        horizontal = Mathf.Clamp(horizontal, -1f, 1f);
    }
}
