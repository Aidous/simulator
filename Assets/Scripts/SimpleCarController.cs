/**
 * Copyright (c) 2018 LG Electronics, Inc.
 *
 * This software contains code licensed as described in LICENSE.
 *
 */


﻿using UnityEngine;
using System.Collections.Generic;

public class SimpleCarController : RobotController, Ros.IRosClient
{
    static readonly string UNITY_TIME_TOPIC = "/unity_time";
    static readonly string CAR_INFO_TOPIC = "/car_info";
    static readonly string SIM_CUR_VELOCITY_TOPIC = "/simulator/current_velocity";

    [System.Serializable]
    public class AxleInfo
    {
        public WheelCollider leftWheel;
        public WheelCollider rightWheel;
        public bool motor;
        public bool steering;
    }

    public List<AxleInfo> axleInfos;
    public float maxMotorTorque;

    public float maxSteeringDiff;

    public float ROSWheelForceScaler = 350.0f; //use when being controled by ROS wheel command

    public InputController input;
    public Transform mainTransform;
    private Vector3 initialMainTransformPos;
    private Quaternion initialMainTransformRot;
    public List<Rigidbody> allRigidBodies;
    public Rigidbody mainRigidbody;

    [System.NonSerialized]
    public Vector3 linearVelocity;
    [System.NonSerialized]
    public Vector3 angularVelocity;
    [System.NonSerialized]
    public float linearSpeed;
    [System.NonSerialized]
    public float angularSpeed;

    Ros.Bridge Bridge;

    private void Start()
    {
        if (input == null)
        {
            input = GetComponent<InputController>();
        }

        initialMainTransformPos = mainTransform.position;
        initialMainTransformRot = mainTransform.rotation;
    }

    public void OnRosBridgeAvailable(Ros.Bridge bridge)
    {
        Bridge = bridge;
        Bridge.AddPublisher(this);
    }

    public void OnRosConnected()
    {
        Bridge.AddPublisher<float>(UNITY_TIME_TOPIC);
        Bridge.AddPublisher<Ros.Pose>(CAR_INFO_TOPIC);
        Bridge.AddPublisher<Ros.TwistStamped>(SIM_CUR_VELOCITY_TOPIC);
    }

    public void OnRosConnected(Ros.Bridge bridge)
    {
    }

    public override void SetWheelScale(float value)
    {
        ROSWheelForceScaler = value;
    }

    public override void ResetPosition()
    {
        Vector3 posOffset = initialMainTransformPos - mainTransform.position;
        Quaternion rotOffset = initialMainTransformRot * Quaternion.Inverse(mainTransform.rotation);
        foreach (var rb in allRigidBodies)
        {
            //rb.isKinematic = true;
            rb.position += posOffset;
            rb.rotation = rotOffset * rb.rotation;
            //rb.isKinematic = false;
        }
    }

	public override void ResetSavedPosition(Vector3 pos, Quaternion rot)
	{
		// TODO Eric check duckie maps
		Vector3 posOffset = pos - mainTransform.position;
		Quaternion rotOffset = rot * Quaternion.Inverse(mainTransform.rotation);
		foreach (var rb in allRigidBodies)
		{
			//rb.isKinematic = true;
			rb.position += posOffset;
			rb.rotation = rotOffset * rb.rotation;
			//rb.isKinematic = false;
		}
	}


	public void Update()
    {
        //Deal with wheel geo
        foreach (AxleInfo axleInfo in axleInfos)
        {
            ApplyLocalPositionToVisuals(axleInfo.leftWheel);
            ApplyLocalPositionToVisuals(axleInfo.rightWheel);
        }
        linearVelocity = mainRigidbody.velocity;
        angularVelocity = mainRigidbody.angularVelocity;
    }

    // finds the corresponding visual wheel
    // correctly applies the transform
    public void ApplyLocalPositionToVisuals(WheelCollider collider)
    {
        if (collider.transform.childCount == 0)
        {
            return;
        }

        Transform visualWheel = collider.transform.GetChild(0); // Can be improved

        Vector3 position;
        Quaternion rotation;
        collider.GetWorldPose(out position, out rotation);

        visualWheel.transform.position = position;
        visualWheel.transform.rotation = rotation;
    }

    public void LateUpdate()
    {
        if (Bridge == null || Bridge.Status != Ros.Status.Connected)
        {
            return;
        }

        Bridge.Publish(CAR_INFO_TOPIC, new Ros.Pose()
        {
            position = new Ros.Point()
            {
                x = mainTransform.position.x,
                y = mainTransform.position.y,
                z = mainTransform.position.z,
            },
            orientation = new Ros.Quaternion()
            {
                x = mainTransform.rotation.x,
                y = mainTransform.rotation.y,
                z = mainTransform.rotation.z,
                w = mainTransform.rotation.w,
            },
        });
        Bridge.Publish(UNITY_TIME_TOPIC, Time.time);

        linearVelocity = mainRigidbody.velocity;
        angularVelocity = mainRigidbody.angularVelocity;
        linearSpeed = linearVelocity.magnitude;
        angularSpeed = angularVelocity.magnitude;
        Bridge.Publish(SIM_CUR_VELOCITY_TOPIC, new Ros.TwistStamped()
        {
            twist = new Ros.Twist()
            {
                linear = new Ros.Vector3()
                {
                    x = linearSpeed,
                    y = 0.0,
                    z = 0.0,
                },
                angular = new Ros.Vector3()
                {
                    x = 0.0,
                    y = 0.0,
                    z = angularSpeed,
                }
            }
        });
    }

    public void FixedUpdate()
    {
        if (input.controlMethod == InputController.ControlMethod.ROS)
        {
            float leftMotor = input.wheelLeftVel * ROSWheelForceScaler;
            float rightMotor = input.wheelRightVel * ROSWheelForceScaler;

            foreach (AxleInfo axleInfo in axleInfos)
            {
                if (leftMotor == 0)
                {
                    if (axleInfo.leftWheel.motorTorque != 0)
                    {
                        axleInfo.leftWheel.motorTorque = 0f;
                    }
                }
                else
                {
                    axleInfo.leftWheel.motorTorque = leftMotor;
                }

                if (rightMotor == 0)
                {
                    if (axleInfo.rightWheel.motorTorque != 0)
                    {
                        axleInfo.rightWheel.motorTorque = 0f;
                    }
                }
                else
                {
                    axleInfo.rightWheel.motorTorque = rightMotor;
                }
            }            
        }
        else
        {
            float motor = maxMotorTorque * input.vertical;
          
            float steeringDif = maxSteeringDiff * input.horizontal;

            float leftMotor = 0f;
            float rightMotor = 0f;

            foreach (AxleInfo axleInfo in axleInfos)
            {
                if (axleInfo.motor)
                {
                    if (motor == 0f)
                    {
                        leftMotor = 0f;
                        rightMotor = 0f;
                    }
                    else
                    {
                        leftMotor = motor;
                        rightMotor = motor;
                    }
                }

                if (axleInfo.steering)
                {
                    if (steeringDif != 0f)
                    {
                        leftMotor += steeringDif;
                        rightMotor -= steeringDif;
                    }
                }

                if (leftMotor == 0)
                {
                    if (axleInfo.leftWheel.motorTorque != 0)
                    {
                        axleInfo.leftWheel.motorTorque = 0f;
                    }
                }
                else
                {
                    axleInfo.leftWheel.motorTorque = leftMotor;
                }

                if (rightMotor == 0)
                {
                    if (axleInfo.rightWheel.motorTorque != 0)
                    {
                        axleInfo.rightWheel.motorTorque = 0f;
                    }
                }
                else
                {
                    axleInfo.rightWheel.motorTorque = rightMotor;
                }
            }
        }
    }
}
