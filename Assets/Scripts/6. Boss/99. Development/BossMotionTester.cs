using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Sirenix.OdinInspector;
using Study.Utilities;
using UnityEngine.InputSystem;

namespace TeamOdd.Boss.Development
{
    public class BossMotionTester : MonoBehaviour
    {
        [Title("Components")]
        [SerializeField] private Animator animator;

        [Title("Motion")]
        [SerializeField] private int currentMotionIndex;

        [ShowInInspector, ReadOnly] private int maxMotionIndex;
        [ShowInInspector, ReadOnly] private string currentMotionName;
        
        [Title("Motion Info")]
        [ShowInInspector, ReadOnly] private string[] motionNames;

        private Vector3 resetPosition;
        private Vector3 resetRotation;

        private void Awake()
        {
            animator ??= GetComponent<Animator>();
            InitMotionNames();
            
            resetPosition = transform.position;
            resetRotation = transform.eulerAngles;
        }

        private void Update()
        {
            if (Key.LeftCtrl.GetKeyDown())
            {
                SwitchMotion();
            }
            
            if (Key.Space.GetKeyDown())
            {
                PlayMotion();
            }

            if (Key.R.GetKeyDown())
            {
                transform.position = resetPosition;
                transform.eulerAngles = resetRotation;
            }
        }

        private void InitMotionNames()
        {
            motionNames = animator.runtimeAnimatorController.animationClips.Select(x => x.name).ToArray();
            currentMotionName = motionNames[currentMotionIndex];
            maxMotionIndex = motionNames.Length - 1;
        }

        private void SwitchMotion()
        {
            currentMotionIndex = (currentMotionIndex + 1) % motionNames.Length;
            currentMotionName = motionNames[currentMotionIndex];
        }

        private void PlayMotion()
        {
            animator.Play(currentMotionName);
        }
    }
}